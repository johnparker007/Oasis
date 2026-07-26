Shader "Oasis/Face"
{
    Properties
    {
        [MainTexture] _OasisArtworkTex ("Artwork", 2D) = "white" {}
        _OasisMaskTex ("Mask", 2D) = "white" {}
        _OasisTrayIdTex ("Tray ID", 2D) = "black" {}
        _OasisLampIds0Tex ("Lamp IDs 0", 2D) = "black" {}
        _OasisLampWeights0Tex ("Lamp Weights 0", 2D) = "black" {}
        _OasisLampStateTex ("Lamp State", 2D) = "black" {}
        _OasisLampExposureStops ("Lamp Exposure Stops", Range(0, 8)) = 2.5
        _OasisStaticBrightness ("Static Brightness", Range(0, 2)) = 1
        _OasisBaseAmbientStrength ("Base Ambient Strength", Range(0, 2)) = 1
        _OasisBaseMainLightStrength ("Base Main Light Strength", Range(0, 2)) = 1
        _OasisBaseAdditionalLightStrength ("Base Additional Light Strength", Range(0, 2)) = 1
        _OasisMaskStrength ("Mask Strength", Range(0, 4)) = 1
        _OasisNormalSign ("Normal Sign", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
        _OasisFaceRotationQuarterTurns ("Face Rotation Quarter Turns", Float) = 0
        _OasisFaceFlipHorizontal ("Face Flip Horizontal", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
            float4 screenPos : TEXCOORD4;
        };

        TEXTURE2D(_OasisArtworkTex);
        SAMPLER(sampler_OasisArtworkTex);
        TEXTURE2D(_OasisTrayIdTex);
        SAMPLER(sampler_OasisTrayIdTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _OasisArtworkTex_ST;
            float _OasisStaticBrightness;
            float _OasisMaskStrength;
            float _OasisLampExposureStops;
            float _OasisBaseAmbientStrength;
            float _OasisBaseMainLightStrength;
            float _OasisBaseAdditionalLightStrength;
            float _OasisNormalSign;
            float _OasisFaceRotationQuarterTurns;
            float _OasisFaceFlipHorizontal;
        CBUFFER_END

        #include "Includes/OasisFaceLampCommon.hlsl"

        Varyings vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionHCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS) * _OasisNormalSign;
            output.uv = TransformFaceUv(TRANSFORM_TEX(input.uv, _OasisArtworkTex));
            output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
            output.screenPos = ComputeScreenPos(positionInputs.positionCS);
            return output;
        }

        float3 EvaluateDiffuseLight(Light light, half3 normalWS, float strength)
        {
            float ndotl = saturate(dot(normalWS, light.direction));
            return light.color * ndotl * light.distanceAttenuation * light.shadowAttenuation * strength;
        }

        float3 EvaluateBaseLighting(Varyings input)
        {
            half3 normalizedNormal = NormalizeNormalPerPixel(input.normalWS);
            float3 lighting = SampleSH(normalizedNormal) * _OasisBaseAmbientStrength;

            Light mainLight = GetMainLight(input.shadowCoord);
            lighting += EvaluateDiffuseLight(mainLight, normalizedNormal, _OasisBaseMainLightStrength);

            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = normalizedNormal;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
            inputData.shadowCoord = input.shadowCoord;

            uint additionalLightsCount = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(additionalLightsCount)
                Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                lighting += EvaluateDiffuseLight(additionalLight, normalizedNormal, _OasisBaseAdditionalLightStrength);
            LIGHT_LOOP_END

            return max(lighting, 0.0);
        }

        ENDHLSL

        Pass
        {
            Name "OasisFaceForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS

            half4 frag(Varyings input) : SV_Target
            {
                half4 artwork = SAMPLE_TEXTURE2D(_OasisArtworkTex, sampler_OasisArtworkTex, input.uv);
                float lampAmount = AccumulateLampAmount(input.uv);
                float3 baseRgb = artwork.rgb * _OasisStaticBrightness * EvaluateBaseLighting(input);
                float3 exposedRgb = EvaluateLampExposure(artwork.rgb, lampAmount);
                float3 lampContribution = max(exposedRgb - artwork.rgb, 0.0);
                return half4((baseRgb + lampContribution) * artwork.a, artwork.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
