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
        _OasisEmissionStrength ("Emission Strength", Range(0, 8)) = 1
        _OasisStaticBrightness ("Static Brightness", Range(0, 2)) = 1
        _OasisMaskStrength ("Mask Strength", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "OasisFaceUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
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

                float light = DecodeMask(mask) * visibleLight * _OasisEmissionStrength;
                float multiplier = _OasisStaticBrightness + light;
                return half4(saturate(artwork.rgb * multiplier), artwork.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
