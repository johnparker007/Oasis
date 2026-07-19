Shader "Oasis/Face"
{
    Properties
    {
        [MainTexture] _OasisArtworkTex ("Artwork", 2D) = "white" {}
        _OasisMaskTex ("Mask", 2D) = "white" {}
        _OasisTrayIdTex ("Tray ID", 2D) = "black" {}
        _OasisLampIds0Tex ("Lamp IDs 0", 2D) = "black" {}
        _OasisLampWeights0Tex ("Lamp Weights 0", 2D) = "black" {}
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

            CBUFFER_START(UnityPerMaterial)
                float4 _OasisArtworkTex_ST;
                float _OasisStaticBrightness;
                float _OasisMaskStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _OasisArtworkTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 artwork = SAMPLE_TEXTURE2D(_OasisArtworkTex, sampler_OasisArtworkTex, input.uv);
                return half4(artwork.rgb * _OasisStaticBrightness, artwork.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
