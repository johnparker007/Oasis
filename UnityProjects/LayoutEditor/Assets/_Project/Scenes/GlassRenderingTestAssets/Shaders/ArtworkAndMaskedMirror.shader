Shader "Oasis/ArtworkAndMaskedMirror"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _AltTex("Texture", 2D) = "white" {}
        _ArtworkTex("Artwork", 2D) = "white" {}
        _MirrorMaskTex("Mirror Mask", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        _SpecColor("Specular Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvArtwork : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_AltTex);
            SAMPLER(sampler_AltTex);

            TEXTURE2D(_ArtworkTex);
            SAMPLER(sampler_ArtworkTex);
            float4 _ArtworkTex_ST;

            TEXTURE2D(_MirrorMaskTex);
            SAMPLER(sampler_MirrorMaskTex);
            float4 _MirrorMaskTex_ST;

            float4 _BaseColor;
            float _Smoothness;
            float4 _SpecColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uvArtwork = TRANSFORM_TEX(input.uv, _ArtworkTex);
                output.uvMask = TRANSFORM_TEX(input.uv, _MirrorMaskTex);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half3 CalculateDirectLighting(float3 normalWS, float3 viewDirWS, float3 positionWS, half3 albedo)
            {
                half3 lighting = 0;

                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                if (NdotL > 0)
                {
                    half3 diffuse = albedo * mainLight.color.rgb * NdotL * mainLight.distanceAttenuation;
                    lighting += diffuse * mainLight.shadowAttenuation;

                    half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                    half spec = pow(saturate(dot(normalWS, halfDir)), max(1e-4, (1.0h - _Smoothness) * 64.0h));
                    lighting += spec * _SpecColor.rgb * mainLight.color.rgb * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                }

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    half addNdotL = saturate(dot(normalWS, light.direction));
                    if (addNdotL <= 0)
                        continue;

                    half3 addDiffuse = albedo * light.color.rgb * addNdotL * light.distanceAttenuation;
                    addDiffuse *= light.shadowAttenuation;
                    lighting += addDiffuse;

                    half3 addHalfDir = SafeNormalize(light.direction + viewDirWS);
                    half addSpec = pow(saturate(dot(normalWS, addHalfDir)), max(1e-4, (1.0h - _Smoothness) * 64.0h));
                    lighting += addSpec * _SpecColor.rgb * light.color.rgb * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                return lighting;
            }

            half3 CalculateIndirectLighting(float3 normalWS, half3 albedo)
            {
                half3 sh = SampleSH(normalWS);
                return sh * albedo;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = SafeNormalize(input.viewDirWS);

                float2 artworkUV = input.uvArtwork;
                half4 artworkSample = SAMPLE_TEXTURE2D(_ArtworkTex, sampler_ArtworkTex, artworkUV) * _BaseColor;
                half mask = SAMPLE_TEXTURE2D(_MirrorMaskTex, sampler_MirrorMaskTex, input.uvMask).r;

                half3 direct = CalculateDirectLighting(normalWS, viewDirWS, input.positionWS, artworkSample.rgb);
                half3 indirect = CalculateIndirectLighting(normalWS, artworkSample.rgb);
                half3 litArtwork = saturate(direct + indirect);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV.x *= -1;

                half4 mirrorColor = half4(0, 0, 0, 0);
                if (unity_StereoEyeIndex == 1)
                {
                    mirrorColor = SAMPLE_TEXTURE2D(_AltTex, sampler_AltTex, screenUV);
                }
                else
                {
                    mirrorColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                }

                half3 finalColor = saturate(litArtwork + mirrorColor.rgb * mask);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
