Shader "Oasis/Cabinet Analytic Reflection"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _OasisReflectionVisibilityMask ("Reflection Visibility Mask", 2D) = "white" {}
        _OasisReflectionStrength ("Reflection Strength", Range(0, 1)) = 0

        _OasisArtworkTex ("Face Artwork", 2D) = "white" {}
        _OasisMaskTex ("Face Mask", 2D) = "white" {}
        _OasisLampIds0Tex ("Face Lamp IDs", 2D) = "black" {}
        _OasisLampWeights0Tex ("Face Lamp Weights", 2D) = "black" {}
        _OasisLampStateTex ("Lamp State", 2D) = "black" {}
        _OasisLampExposureStops ("Lamp Exposure Stops", Range(0, 8)) = 2.5
        _OasisMaskStrength ("Mask Strength", Range(0, 4)) = 1
        _OasisFaceRotationQuarterTurns ("Face Rotation Quarter Turns", Float) = 0
        _OasisFaceFlipHorizontal ("Face Flip Horizontal", Float) = 0

        _OasisFacePlaneOriginWS ("Face Plane Origin WS", Vector) = (0,0,0,1)
        _OasisFacePlaneRightWS ("Face Plane Right WS", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS ("Face Plane Up WS", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS ("Face Plane Normal WS", Vector) = (0,0,1,0)
        _OasisFacePlaneSize ("Face Plane Size", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "OasisCabinetAnalyticReflectionForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OasisReflectionVisibilityMask); SAMPLER(sampler_OasisReflectionVisibilityMask);
            TEXTURE2D(_OasisArtworkTex); SAMPLER(sampler_OasisArtworkTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                float _OasisReflectionStrength;
                float _OasisLampExposureStops;
                float _OasisMaskStrength;
                float _OasisFaceRotationQuarterTurns;
                float _OasisFaceFlipHorizontal;
                float4 _OasisFacePlaneOriginWS;
                float4 _OasisFacePlaneRightWS;
                float4 _OasisFacePlaneUpWS;
                float4 _OasisFacePlaneNormalWS;
                float4 _OasisFacePlaneSize;
            CBUFFER_END

            // Reuses the authoritative Face UV and live lamp reconstruction path from PR #579.
            #include "Includes/OasisFaceLampCommon.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positions.positionWS);
                return output;
            }

            bool TryResolveFaceUv(float3 positionWS, float3 normalWS, out float2 faceUv)
            {
                float3 incidentWS = normalize(positionWS - GetCameraPositionWS());
                float3 reflectedWS = reflect(incidentWS, normalWS);
                float3 faceNormalWS = normalize(_OasisFacePlaneNormalWS.xyz);
                float denominator = dot(reflectedWS, faceNormalWS);
                if (abs(denominator) < 1e-5) { faceUv = 0.0; return false; }

                float t = dot(_OasisFacePlaneOriginWS.xyz - positionWS, faceNormalWS) / denominator;
                float2 size = _OasisFacePlaneSize.xy;
                if (t <= 0.0 || size.x <= 0.0 || size.y <= 0.0) { faceUv = 0.0; return false; }

                float3 offset = positionWS + reflectedWS * t - _OasisFacePlaneOriginWS.xyz;
                faceUv = float2(dot(offset, normalize(_OasisFacePlaneRightWS.xyz)) / size.x,
                                dot(offset, normalize(_OasisFacePlaneUpWS.xyz)) / size.y);
                return all(faceUv >= 0.0) && all(faceUv <= 1.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                // Prototype cabinet lighting deliberately uses SH plus the shadowed main light;
                // it is not a complete replacement for URP/Lit's additional-light/IBL features.
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 diffuse = baseSample.rgb * (ambient + mainLight.color * ndotl * mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewWS);
                half specularPower = exp2(10.0 * _Smoothness + 1.0);
                half3 f0 = lerp(0.04.xxx, baseSample.rgb, _Metallic);
                half3 ordinarySurface = diffuse * (1.0 - _Metallic) + f0 * pow(saturate(dot(normalWS, halfDirection)), specularPower) * mainLight.color;

                float2 untransformedFaceUv;
                if (_OasisReflectionStrength > 0.0 && TryResolveFaceUv(input.positionWS, normalWS, untransformedFaceUv))
                {
                    float2 faceUv = TransformFaceUv(untransformedFaceUv);
                    half4 artwork = SAMPLE_TEXTURE2D(_OasisArtworkTex, sampler_OasisArtworkTex, faceUv);
                    float lampAmount = AccumulateLampAmount(faceUv);
                    half3 reflectedFace = EvaluateLampExposure(artwork.rgb, lampAmount);
                    half visibility = SAMPLE_TEXTURE2D(_OasisReflectionVisibilityMask, sampler_OasisReflectionVisibilityMask, input.uv).r;
                    half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 5.0);
                    half blend = saturate(_OasisReflectionStrength * visibility * artwork.a * lerp(0.25, 1.0, fresnel));
                    ordinarySurface = lerp(ordinarySurface, reflectedFace, blend);
                }

                return half4(ordinarySurface, baseSample.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
