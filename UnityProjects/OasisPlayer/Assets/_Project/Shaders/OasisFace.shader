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
        _OasisEmissionStrength ("Emission Strength", Range(0, 8)) = 1.75
        _OasisLampMinLuminance ("Lamp Minimum Luminance", Range(0, 1)) = 0.18
        _OasisLampMaxLuminance ("Lamp Maximum Luminance", Range(0, 8)) = 2.5
        _OasisLampCompression ("Lamp Compression", Range(0.1, 8)) = 2.25
        _OasisStaticBrightness ("Static Brightness", Range(0, 2)) = 1
        _OasisBaseAmbientStrength ("Base Ambient Strength", Range(0, 2)) = 1
        _OasisBaseMainLightStrength ("Base Main Light Strength", Range(0, 2)) = 1
        _OasisMaskStrength ("Mask Strength", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "OasisFaceForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            CBUFFER_END

            static const float3 OasisLuminanceWeights = float3(0.2126, 0.7152, 0.0722);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _OasisArtworkTex);
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

            float3 EvaluateBaseLighting(half3 normalWS)
            {
                half3 normalizedNormal = NormalizeNormalPerPixel(normalWS);
                float3 ambient = SampleSH(normalizedNormal) * _OasisBaseAmbientStrength;

                Light mainLight = GetMainLight();
                // Face targets can be double-sided or exported with either winding. Use an absolute
                // Lambert term so the printed base responds to the main light without changing culling.
                float ndotl = abs(dot(normalizedNormal, mainLight.direction));
                float3 main = mainLight.color * ndotl * mainLight.distanceAttenuation * _OasisBaseMainLightStrength;
                return max(ambient + main, 0.0);
            }

            float3 DeriveLampColourDirection(float3 artworkRgb)
            {
                float artworkLuminance = max(dot(artworkRgb, OasisLuminanceWeights), 0.001);
                float3 hueDirection = artworkRgb / artworkLuminance;
                float3 neutralFallback = float3(1.0, 0.82, 0.55);
                float darkBlend = saturate((_OasisLampMinLuminance - artworkLuminance) / max(_OasisLampMinLuminance, 0.001));
                return lerp(hueDirection, neutralFallback, darkBlend);
            }

            float CompressLampLuminance(float maskedLamp)
            {
                float compressed = 1.0 - exp2(-max(maskedLamp, 0.0) * _OasisLampCompression);
                return min(compressed * _OasisEmissionStrength, _OasisLampMaxLuminance);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 artwork = SAMPLE_TEXTURE2D(_OasisArtworkTex, sampler_OasisArtworkTex, input.uv);
                half4 mask = SAMPLE_TEXTURE2D(_OasisMaskTex, sampler_OasisMaskTex, input.uv);
                half4 lampIds = SAMPLE_TEXTURE2D(_OasisLampIds0Tex, sampler_OasisLampIds0Tex, input.uv);
                half4 weights = SAMPLE_TEXTURE2D(_OasisLampWeights0Tex, sampler_OasisLampWeights0Tex, input.uv);

                float visibleLight = 0.0;
                visibleLight += DecodeLampBrightness(lampIds.r) * DecodeWeight(weights.r);
                visibleLight += DecodeLampBrightness(lampIds.g) * DecodeWeight(weights.g);
                visibleLight += DecodeLampBrightness(lampIds.b) * DecodeWeight(weights.b);

                float maskedLamp = DecodeMask(mask) * saturate(visibleLight);
                float3 baseRgb = artwork.rgb * _OasisStaticBrightness * EvaluateBaseLighting(input.normalWS);

                // Separate colour direction from lamp luminance. This keeps saturated artwork chroma
                // useful at high intensity, while a warm fallback lets nearly black ink still glow.
                float3 lampColourDirection = DeriveLampColourDirection(artwork.rgb);
                float lampLuminance = max(dot(artwork.rgb, OasisLuminanceWeights), _OasisLampMinLuminance) * CompressLampLuminance(maskedLamp);
                float3 lampEmission = lampColourDirection * lampLuminance;

                return half4(baseRgb + lampEmission, artwork.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
