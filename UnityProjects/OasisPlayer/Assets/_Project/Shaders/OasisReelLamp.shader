Shader "Oasis/ReelLamp"
{
    Properties
    {
        [MainTexture] _MainTex ("Reel Band", 2D) = "white" {}
        _OasisReelTransmissionMaskTex ("Transmission Mask", 2D) = "white" {}
        _OasisReelTransmissionMaskEnabled ("Transmission Mask Enabled", Float) = 0
        _OasisReelLampBrightness ("Lamp Brightness", Vector) = (0,0,0,0)
        _OasisReelLampVerticalCenters ("Lamp Vertical Centers", Vector) = (0.1667,0.5,0.8333,0)
        _OasisReelLampRadii ("Lamp Radii", Vector) = (0.42,0.42,0.42,0)
        _OasisReelLampIntensities ("Lamp Intensities", Vector) = (1,1,1,0)
        _OasisReelLampColor ("Lamp Color", Color) = (1.0,0.82,0.55,1)
        _OasisReelWindowUvOffset ("Window UV Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
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
            float2 bandUv : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_OasisReelTransmissionMaskTex);
        SAMPLER(sampler_OasisReelTransmissionMaskTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _OasisReelTransmissionMaskEnabled;
            float4 _OasisReelLampBrightness;
            float4 _OasisReelLampVerticalCenters;
            float4 _OasisReelLampRadii;
            float4 _OasisReelLampIntensities;
            float4 _OasisReelLampColor;
            float _OasisReelWindowUvOffset;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionHCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
            output.bandUv = TRANSFORM_TEX(input.uv, _MainTex);
            output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
            return output;
        }

        float3 EvaluateDiffuseLight(Light light, half3 normalWS)
        {
            float ndotl = saturate(dot(normalWS, light.direction));
            return light.color * ndotl * light.distanceAttenuation * light.shadowAttenuation;
        }

        float3 EvaluateBaseLighting(Varyings input)
        {
            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            float3 lighting = SampleSH(normalWS);
            Light mainLight = GetMainLight(input.shadowCoord);
            lighting += EvaluateDiffuseLight(mainLight, normalWS);

            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
            inputData.shadowCoord = input.shadowCoord;

            uint additionalLightsCount = GetAdditionalLightsCount();
            LIGHT_LOOP_BEGIN(additionalLightsCount)
                Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                lighting += EvaluateDiffuseLight(additionalLight, normalWS);
            LIGHT_LOOP_END

            return max(lighting, 0.0);
        }

        float SmoothField(float2 windowUv, float verticalCenter, float radius)
        {
            float2 center = float2(0.5, verticalCenter);
            float d = distance(windowUv, center);
            return 1.0 - smoothstep(radius * 0.35, max(radius, 0.0001), d);
        }

        float2 FixedWindowUv(float2 bandUv)
        {
            // The reel-band UV is attached to the generated cylinder and therefore rotates with the symbols.
            // The renderer supplies the current band offset separately, letting the shader derive aperture UVs
            // that stay fixed relative to the reel window instead of cabinet world-space translation/rotation.
            return float2(bandUv.x, frac(bandUv.y - _OasisReelWindowUvOffset + 1.0));
        }
        ENDHLSL

        Pass
        {
            Name "OasisReelLampForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
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
                half4 band = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.bandUv);
                float2 windowUv = FixedWindowUv(input.bandUv);
                float mask = lerp(1.0, SAMPLE_TEXTURE2D(_OasisReelTransmissionMaskTex, sampler_OasisReelTransmissionMaskTex, input.bandUv).r, saturate(_OasisReelTransmissionMaskEnabled));

                float lamp = 0.0;
                lamp += SmoothField(windowUv, _OasisReelLampVerticalCenters.x, _OasisReelLampRadii.x) * _OasisReelLampBrightness.x * _OasisReelLampIntensities.x;
                lamp += SmoothField(windowUv, _OasisReelLampVerticalCenters.y, _OasisReelLampRadii.y) * _OasisReelLampBrightness.y * _OasisReelLampIntensities.y;
                lamp += SmoothField(windowUv, _OasisReelLampVerticalCenters.z, _OasisReelLampRadii.z) * _OasisReelLampBrightness.z * _OasisReelLampIntensities.z;

                float3 lit = band.rgb * EvaluateBaseLighting(input);
                float3 emission = band.rgb * _OasisReelLampColor.rgb * saturate(lamp) * mask;
                return half4(lit + emission, band.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
