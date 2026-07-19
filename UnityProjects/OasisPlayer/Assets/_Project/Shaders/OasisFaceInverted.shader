Shader "Oasis/FaceInverted"
{
    Properties
    {
        [MainTexture] _OasisArtworkTex ("Artwork", 2D) = "white" {}
        _OasisMaskTex ("Mask", 2D) = "white" {}
        _OasisTrayIdTex ("Tray ID", 2D) = "black" {}
        _OasisLampIds0Tex ("Lamp IDs 0", 2D) = "black" {}
        _OasisLampWeights0Tex ("Lamp Weights 0", 2D) = "black" {}
        _OasisLampStateTex ("Lamp State", 2D) = "black" {}
        _OasisEmissionStrength ("Emission Strength", Range(0, 8)) = 1.75
        _OasisLampMinLuminance ("Lamp Minimum Luminance", Range(0, 1)) = 0.08
        _OasisLampMaxLuminance ("Lamp Maximum Luminance", Range(0, 8)) = 2
        _OasisLampCompression ("Lamp Compression", Range(0.1, 8)) = 2.25
        _OasisStaticBrightness ("Static Brightness", Range(0, 2)) = 1
        _OasisBaseAmbientStrength ("Base Ambient Strength", Range(0, 2)) = 1
        _OasisBaseMainLightStrength ("Base Main Light Strength", Range(0, 2)) = 1
        _OasisBaseAdditionalLightStrength ("Base Additional Light Strength", Range(0, 2)) = 1
        _OasisMaskStrength ("Mask Strength", Range(0, 4)) = 1
        _OasisNormalSign ("Normal Sign", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 1
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
        TEXTURE2D(_OasisMaskTex);
        SAMPLER(sampler_OasisMaskTex);
        TEXTURE2D(_OasisTrayIdTex);
        SAMPLER(sampler_OasisTrayIdTex);
        TEXTURE2D(_OasisLampIds0Tex);
        SAMPLER(sampler_OasisLampIds0Tex);
        TEXTURE2D(_OasisLampWeights0Tex);
        SAMPLER(sampler_OasisLampWeights0Tex);
        TEXTURE2D(_OasisLampStateTex);
        SAMPLER(sampler_OasisLampStateTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _OasisArtworkTex_ST;
            float _OasisStaticBrightness;
            float _OasisMaskStrength;
            float _OasisEmissionStrength;
            float _OasisLampMinLuminance;
            float _OasisLampMaxLuminance;
            float _OasisLampCompression;
            float _OasisBaseAmbientStrength;
            float _OasisBaseMainLightStrength;
            float _OasisBaseAdditionalLightStrength;
            float _OasisNormalSign;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
            output.positionHCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS) * _OasisNormalSign;
            output.uv = TRANSFORM_TEX(input.uv, _OasisArtworkTex);
            output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
            output.screenPos = ComputeScreenPos(positionInputs.positionCS);
            return output;
        }

        float DecodeLampBrightness(float lampId)
        {
            float decoded = floor(saturate(lampId) * 255.0 + 0.5);
            if (decoded < 1.0 || decoded > 255.0) return 0.0;
            float u = (decoded + 0.5) / 256.0;
            return SAMPLE_TEXTURE2D(_OasisLampStateTex, sampler_OasisLampStateTex, float2(u, 0.5)).r;
        }

        float DecodeWeight(float weight)
        {
            return floor(saturate(weight) * 255.0 + 0.5) / 255.0;
        }

        float DecodeMask(half4 mask)
        {
            float grayscale = max(mask.r, max(mask.g, mask.b));
            return saturate(grayscale * mask.a * _OasisMaskStrength);
        }

        float AccumulateLampAmount(float2 uv)
        {
            half4 mask = SAMPLE_TEXTURE2D(_OasisMaskTex, sampler_OasisMaskTex, uv);
            half4 lampIds = SAMPLE_TEXTURE2D(_OasisLampIds0Tex, sampler_OasisLampIds0Tex, uv);
            half4 weights = SAMPLE_TEXTURE2D(_OasisLampWeights0Tex, sampler_OasisLampWeights0Tex, uv);

            float visibleLight = 0.0;
            visibleLight += DecodeLampBrightness(lampIds.r) * DecodeWeight(weights.r);
            visibleLight += DecodeLampBrightness(lampIds.g) * DecodeWeight(weights.g);
            visibleLight += DecodeLampBrightness(lampIds.b) * DecodeWeight(weights.b);
            return DecodeMask(mask) * saturate(visibleLight);
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

        float3 DeriveLampColour(float3 artworkRgb)
        {
            float peak = max(artworkRgb.r, max(artworkRgb.g, artworkRgb.b));
            float3 chroma = artworkRgb / max(peak, 0.001);

            // Only nearly black pixels lack a reliable source hue. Blend them toward a subdued neutral
            // fallback; ordinary dark coloured ink keeps its artwork-derived chroma direction.
            float colourConfidence = saturate(peak / max(_OasisLampMinLuminance, 0.001));
            float3 fallback = float3(0.55, 0.50, 0.42);
            return lerp(fallback, chroma, colourConfidence);
        }

        float CompressLampAmount(float lampAmount)
        {
            float compressed = 1.0 - exp2(-max(lampAmount, 0.0) * _OasisLampCompression);
            return min(compressed, _OasisLampMaxLuminance);
        }

        float3 EvaluateLampEmission(float2 uv)
        {
            half4 artwork = SAMPLE_TEXTURE2D(_OasisArtworkTex, sampler_OasisArtworkTex, uv);
            float lampAmount = AccumulateLampAmount(uv);
            float3 lampColour = DeriveLampColour(artwork.rgb);
            float compressedLampAmount = CompressLampAmount(lampAmount);
            return lampColour * compressedLampAmount * _OasisEmissionStrength;
        }
        ENDHLSL

        Pass
        {
            Name "OasisFaceForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull Front
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
                float3 baseRgb = artwork.rgb * _OasisStaticBrightness * EvaluateBaseLighting(input);
                float3 basePremultiplied = baseRgb * artwork.a;
                float3 lampEmission = EvaluateLampEmission(input.uv);
                return half4(basePremultiplied + lampEmission, artwork.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
