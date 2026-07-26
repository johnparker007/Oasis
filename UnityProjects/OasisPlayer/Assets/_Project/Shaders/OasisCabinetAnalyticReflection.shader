Shader "Oasis/CabinetAnalyticReflection"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Colour", Color) = (0.35,0.35,0.35,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0,2)) = 1
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0
        _OasisReflectionVisibilityMaskTex ("Reflection Visibility", 2D) = "white" {}
        [Toggle] _OasisReflectionEnabled ("Reflection Enabled", Float) = 0
        _OasisReflectionStrength ("Reflection Strength", Range(0,2)) = 0.2
        _OasisReflectionUnlitArtworkStrength ("Unlit Artwork Strength", Range(0,2)) = 0.25
        _OasisReflectionLitLampStrength ("Lit Lamp Strength", Range(0,4)) = 1
        _OasisReflectionFresnelPower ("Fresnel Power", Range(0.1,10)) = 5
        _OasisReflectionFresnelStrength ("Fresnel Strength", Range(0,2)) = 0.5
        _OasisReflectionRoughness ("Reflection Roughness", Range(0,1)) = 0.4
        _OasisReflectionDistortion ("Normal Distortion", Range(0,0.05)) = 0.005
        _OasisReflectionEdgeFade ("Rectangle Edge Fade", Range(0,0.25)) = 0.03

        [NoScaleOffset] _OasisArtworkTex ("Face Artwork", 2D) = "white" {}
        [NoScaleOffset] _OasisMaskTex ("Face Mask", 2D) = "white" {}
        [NoScaleOffset] _OasisLampIds0Tex ("Face Lamp IDs", 2D) = "black" {}
        [NoScaleOffset] _OasisLampWeights0Tex ("Face Lamp Weights", 2D) = "black" {}
        [NoScaleOffset] _OasisLampStateTex ("Lamp State", 2D) = "black" {}
        _OasisLampExposureStops ("Lamp Exposure Stops", Range(0,8)) = 2.5
        _OasisMaskStrength ("Mask Strength", Range(0,4)) = 1
        _OasisFaceRotationQuarterTurns ("Face Rotation Quarter Turns", Float) = 0
        _OasisFaceFlipHorizontal ("Face Flip Horizontal", Float) = 0
        _OasisFacePlaneOriginWS ("Face Origin WS", Vector) = (0,0,0,0)
        _OasisFacePlaneRightWS ("Face Right WS", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS ("Face Up WS", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS ("Face Normal WS", Vector) = (0,0,1,0)
        _OasisFacePlaneSize ("Face Width Height", Vector) = (1,1,0,0)
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _AlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            TEXTURE2D(_OasisReflectionVisibilityMaskTex); SAMPLER(sampler_OasisReflectionVisibilityMaskTex);
            TEXTURE2D(_OasisArtworkTex); SAMPLER(sampler_OasisArtworkTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float _BumpScale; float _Smoothness; float _Metallic;
                float _OasisReflectionEnabled, _OasisReflectionStrength, _OasisReflectionUnlitArtworkStrength, _OasisReflectionLitLampStrength;
                float _OasisReflectionFresnelPower, _OasisReflectionFresnelStrength, _OasisReflectionRoughness, _OasisReflectionDistortion, _OasisReflectionEdgeFade;
                float _OasisLampExposureStops, _OasisMaskStrength, _OasisFaceRotationQuarterTurns, _OasisFaceFlipHorizontal;
                float4 _OasisFacePlaneOriginWS, _OasisFacePlaneRightWS, _OasisFacePlaneUpWS, _OasisFacePlaneNormalWS, _OasisFacePlaneSize;
            CBUFFER_END
            #include "Includes/OasisFaceLampCommon.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float4 shadowCoord:TEXCOORD4; half fogFactor:TEXCOORD5; };
            Varyings vert(Attributes v) { Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz); VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS); o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=n.normalWS; o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale()); o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.shadowCoord=TransformWorldToShadowCoord(p.positionWS); o.fogFactor=ComputeFogFactor(p.positionCS.z); return o; }

            float3 ReconstructFace(float2 uv)
            {
                half4 art=SAMPLE_TEXTURE2D(_OasisArtworkTex,sampler_OasisArtworkTex,uv);
                float3 exposed=EvaluateLampExposure(art.rgb,AccumulateLampAmount(uv));
                return art.rgb*_OasisReflectionUnlitArtworkStrength + max(exposed-art.rgb,0)*_OasisReflectionLitLampStrength;
            }

            half4 frag(Varyings i):SV_Target
            {
                half4 baseSample=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                half tangentValid=step(1e-4,dot(i.tangentWS.xyz,i.tangentWS.xyz));
                half3 fallbackAxis=abs(i.normalWS.y)<0.999?half3(0,1,0):half3(1,0,0);
                half3 fallbackTangent=normalize(cross(fallbackAxis,i.normalWS));
                half3 safeTangent=normalize(lerp(fallbackTangent,i.tangentWS.xyz,tangentValid));
                half3 bitangent=cross(i.normalWS,safeTangent)*i.tangentWS.w;
                half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                half3 mappedNormal=NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,half3x3(safeTangent,bitangent,i.normalWS)));
                half3 normalWS=NormalizeNormalPerPixel(lerp(i.normalWS,mappedNormal,tangentValid));
                InputData inputData=(InputData)0; inputData.positionWS=i.positionWS; inputData.normalWS=normalWS; inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); inputData.shadowCoord=i.shadowCoord; inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.bakedGI=SampleSH(normalWS);
                SurfaceData surface=(SurfaceData)0; surface.albedo=baseSample.rgb; surface.alpha=baseSample.a; surface.metallic=_Metallic; surface.smoothness=_Smoothness; surface.normalTS=half3(0,0,1); surface.occlusion=1;
                half4 cabinet=UniversalFragmentPBR(inputData,surface); cabinet.rgb=MixFog(cabinet.rgb,i.fogFactor);
                if (_OasisReflectionEnabled<0.5 || _OasisFacePlaneSize.x<=0 || _OasisFacePlaneSize.y<=0) return cabinet;

                float3 incident=SafeNormalize(i.positionWS-_WorldSpaceCameraPos);
                float3 reflected=reflect(incident,normalWS);
                float denominator=dot(reflected,_OasisFacePlaneNormalWS.xyz);
                if (abs(denominator)<1e-5) return cabinet;
                float t=dot(_OasisFacePlaneOriginWS.xyz-i.positionWS,_OasisFacePlaneNormalWS.xyz)/denominator;
                if (t<=1e-5) return cabinet;
                float3 relative=i.positionWS+reflected*t-_OasisFacePlaneOriginWS.xyz;
                float2 planeUv=float2(dot(relative,_OasisFacePlaneRightWS.xyz)/_OasisFacePlaneSize.x,dot(relative,_OasisFacePlaneUpWS.xyz)/_OasisFacePlaneSize.y);
                if (any(planeUv<0) || any(planeUv>1)) return cabinet;
                float2 uv=saturate(TransformFaceUv(planeUv)+normalTS.xy*_OasisReflectionDistortion);
                float2 d=float2(_OasisReflectionRoughness*0.006,0);
                // Each tap reconstructs IDs/weights independently; lookup textures are never prefiltered as colour.
                float3 reflectedColour=ReconstructFace(uv);
                if (_OasisReflectionRoughness>0.001) reflectedColour=(reflectedColour*2+ReconstructFace(saturate(uv+d))+ReconstructFace(saturate(uv-d))+ReconstructFace(saturate(uv+d.yx))+ReconstructFace(saturate(uv-d.yx)))/6;
                float edge=min(min(planeUv.x,planeUv.y),min(1-planeUv.x,1-planeUv.y));
                float edgeFade=_OasisReflectionEdgeFade>1e-5?saturate(edge/_OasisReflectionEdgeFade):1;
                float visibility=SAMPLE_TEXTURE2D(_OasisReflectionVisibilityMaskTex,sampler_OasisReflectionVisibilityMaskTex,i.uv).r;
                float fresnel=pow(1-saturate(dot(inputData.viewDirectionWS,normalWS)),_OasisReflectionFresnelPower)*_OasisReflectionFresnelStrength;
                float blend=saturate(_OasisReflectionStrength*visibility*edgeFade*(1+fresnel));
                return half4(lerp(cabinet.rgb,reflectedColour,blend),cabinet.a);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}
