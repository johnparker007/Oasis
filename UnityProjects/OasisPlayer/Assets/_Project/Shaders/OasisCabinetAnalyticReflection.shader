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

        _OasisReflectionSourceCount ("Source Count", Float) = 0
        [NoScaleOffset] _OasisArtworkTex0 ("Face 0 Artwork", 2D) = "white" {}
        [NoScaleOffset] _OasisMaskTex0 ("Face 0 Mask", 2D) = "white" {}
        [NoScaleOffset] _OasisLampIds0Tex0 ("Face 0 Lamp IDs", 2D) = "black" {}
        [NoScaleOffset] _OasisLampWeights0Tex0 ("Face 0 Lamp Weights", 2D) = "black" {}
        _OasisLampExposureStops0 ("Face 0 Exposure", Float) = 2.5
        _OasisMaskStrength0 ("Face 0 Mask Strength", Float) = 1
        _OasisFaceRotationQuarterTurns0 ("Face 0 Rotation", Float) = 0
        _OasisFaceFlipHorizontal0 ("Face 0 Flip", Float) = 0
        _OasisFacePlaneOriginWS0 ("Face 0 Origin", Vector) = (0,0,0,0)
        _OasisFacePlaneRightWS0 ("Face 0 Right", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS0 ("Face 0 Up", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS0 ("Face 0 Normal", Vector) = (0,0,1,0)
        _OasisFacePlaneSize0 ("Face 0 Size", Vector) = (1,1,0,0)
        [NoScaleOffset] _OasisArtworkTex1 ("Face 1 Artwork", 2D) = "white" {}
        [NoScaleOffset] _OasisMaskTex1 ("Face 1 Mask", 2D) = "white" {}
        [NoScaleOffset] _OasisLampIds0Tex1 ("Face 1 Lamp IDs", 2D) = "black" {}
        [NoScaleOffset] _OasisLampWeights0Tex1 ("Face 1 Lamp Weights", 2D) = "black" {}
        _OasisLampExposureStops1 ("Face 1 Exposure", Float) = 2.5
        _OasisMaskStrength1 ("Face 1 Mask Strength", Float) = 1
        _OasisFaceRotationQuarterTurns1 ("Face 1 Rotation", Float) = 0
        _OasisFaceFlipHorizontal1 ("Face 1 Flip", Float) = 0
        _OasisFacePlaneOriginWS1 ("Face 1 Origin", Vector) = (0,0,0,0)
        _OasisFacePlaneRightWS1 ("Face 1 Right", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS1 ("Face 1 Up", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS1 ("Face 1 Normal", Vector) = (0,0,1,0)
        _OasisFacePlaneSize1 ("Face 1 Size", Vector) = (1,1,0,0)
        [NoScaleOffset] _OasisArtworkTex2 ("Face 2 Artwork", 2D) = "white" {}
        [NoScaleOffset] _OasisMaskTex2 ("Face 2 Mask", 2D) = "white" {}
        [NoScaleOffset] _OasisLampIds0Tex2 ("Face 2 Lamp IDs", 2D) = "black" {}
        [NoScaleOffset] _OasisLampWeights0Tex2 ("Face 2 Lamp Weights", 2D) = "black" {}
        _OasisLampExposureStops2 ("Face 2 Exposure", Float) = 2.5
        _OasisMaskStrength2 ("Face 2 Mask Strength", Float) = 1
        _OasisFaceRotationQuarterTurns2 ("Face 2 Rotation", Float) = 0
        _OasisFaceFlipHorizontal2 ("Face 2 Flip", Float) = 0
        _OasisFacePlaneOriginWS2 ("Face 2 Origin", Vector) = (0,0,0,0)
        _OasisFacePlaneRightWS2 ("Face 2 Right", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS2 ("Face 2 Up", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS2 ("Face 2 Normal", Vector) = (0,0,1,0)
        _OasisFacePlaneSize2 ("Face 2 Size", Vector) = (1,1,0,0)
        [NoScaleOffset] _OasisArtworkTex3 ("Face 3 Artwork", 2D) = "white" {}
        [NoScaleOffset] _OasisMaskTex3 ("Face 3 Mask", 2D) = "white" {}
        [NoScaleOffset] _OasisLampIds0Tex3 ("Face 3 Lamp IDs", 2D) = "black" {}
        [NoScaleOffset] _OasisLampWeights0Tex3 ("Face 3 Lamp Weights", 2D) = "black" {}
        _OasisLampExposureStops3 ("Face 3 Exposure", Float) = 2.5
        _OasisMaskStrength3 ("Face 3 Mask Strength", Float) = 1
        _OasisFaceRotationQuarterTurns3 ("Face 3 Rotation", Float) = 0
        _OasisFaceFlipHorizontal3 ("Face 3 Flip", Float) = 0
        _OasisFacePlaneOriginWS3 ("Face 3 Origin", Vector) = (0,0,0,0)
        _OasisFacePlaneRightWS3 ("Face 3 Right", Vector) = (1,0,0,0)
        _OasisFacePlaneUpWS3 ("Face 3 Up", Vector) = (0,1,0,0)
        _OasisFacePlaneNormalWS3 ("Face 3 Normal", Vector) = (0,0,1,0)
        _OasisFacePlaneSize3 ("Face 3 Size", Vector) = (1,1,0,0)
        [NoScaleOffset] _OasisLampStateTex ("Lamp State", 2D) = "black" {}
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
            #include "Includes/OasisFaceUvOrientation.hlsl"

            TEXTURE2D(_OasisReflectionVisibilityMaskTex); SAMPLER(sampler_OasisReflectionVisibilityMaskTex);
            TEXTURE2D(_OasisLampStateTex); SAMPLER(sampler_OasisLampStateTex);
            // All Face colour/mask textures share one sampler, and all integer lookup textures
            // share one point sampler. The textures remain independent; sharing sampler state keeps
            // the D3D11 ps_4_0 sampler count below 16 even with four configured sources.
            TEXTURE2D(_OasisArtworkTex0); SAMPLER(sampler_OasisArtworkTex0); TEXTURE2D(_OasisMaskTex0); TEXTURE2D(_OasisLampIds0Tex0); SAMPLER(sampler_OasisLampIds0Tex0); TEXTURE2D(_OasisLampWeights0Tex0);
            TEXTURE2D(_OasisArtworkTex1); TEXTURE2D(_OasisMaskTex1); TEXTURE2D(_OasisLampIds0Tex1); TEXTURE2D(_OasisLampWeights0Tex1);
            TEXTURE2D(_OasisArtworkTex2); TEXTURE2D(_OasisMaskTex2); TEXTURE2D(_OasisLampIds0Tex2); TEXTURE2D(_OasisLampWeights0Tex2);
            TEXTURE2D(_OasisArtworkTex3); TEXTURE2D(_OasisMaskTex3); TEXTURE2D(_OasisLampIds0Tex3); TEXTURE2D(_OasisLampWeights0Tex3);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float _BumpScale; float _Smoothness; float _Metallic;
                float _OasisReflectionEnabled, _OasisReflectionSourceCount, _OasisReflectionStrength, _OasisReflectionUnlitArtworkStrength, _OasisReflectionLitLampStrength;
                float _OasisReflectionFresnelPower, _OasisReflectionFresnelStrength, _OasisReflectionRoughness, _OasisReflectionDistortion, _OasisReflectionEdgeFade;
                float _OasisLampExposureStops0, _OasisMaskStrength0, _OasisFaceRotationQuarterTurns0, _OasisFaceFlipHorizontal0; float4 _OasisFacePlaneOriginWS0, _OasisFacePlaneRightWS0, _OasisFacePlaneUpWS0, _OasisFacePlaneNormalWS0, _OasisFacePlaneSize0;
                float _OasisLampExposureStops1, _OasisMaskStrength1, _OasisFaceRotationQuarterTurns1, _OasisFaceFlipHorizontal1; float4 _OasisFacePlaneOriginWS1, _OasisFacePlaneRightWS1, _OasisFacePlaneUpWS1, _OasisFacePlaneNormalWS1, _OasisFacePlaneSize1;
                float _OasisLampExposureStops2, _OasisMaskStrength2, _OasisFaceRotationQuarterTurns2, _OasisFaceFlipHorizontal2; float4 _OasisFacePlaneOriginWS2, _OasisFacePlaneRightWS2, _OasisFacePlaneUpWS2, _OasisFacePlaneNormalWS2, _OasisFacePlaneSize2;
                float _OasisLampExposureStops3, _OasisMaskStrength3, _OasisFaceRotationQuarterTurns3, _OasisFaceFlipHorizontal3; float4 _OasisFacePlaneOriginWS3, _OasisFacePlaneRightWS3, _OasisFacePlaneUpWS3, _OasisFacePlaneNormalWS3, _OasisFacePlaneSize3;
            CBUFFER_END
            float Lamp(float id) { float d=floor(saturate(id)*255+0.5); return d<1?0:SAMPLE_TEXTURE2D(_OasisLampStateTex,sampler_OasisLampStateTex,float2((d+0.5)/256,0.5)).r; }
            float3 ReconstructFace0(float2 uv) { uv=TransformFaceUv(uv,_OasisFaceRotationQuarterTurns0,_OasisFaceFlipHorizontal0); half4 art=SAMPLE_TEXTURE2D(_OasisArtworkTex0,sampler_OasisArtworkTex0,uv); half4 mask=SAMPLE_TEXTURE2D(_OasisMaskTex0,sampler_OasisArtworkTex0,uv); half4 ids=SAMPLE_TEXTURE2D(_OasisLampIds0Tex0,sampler_OasisLampIds0Tex0,uv); half4 weights=SAMPLE_TEXTURE2D(_OasisLampWeights0Tex0,sampler_OasisLampIds0Tex0,uv); float amount=saturate(max(mask.r,max(mask.g,mask.b))*mask.a*_OasisMaskStrength0)*saturate(Lamp(ids.r)*weights.r+Lamp(ids.g)*weights.g+Lamp(ids.b)*weights.b); float3 exposed=art.rgb*exp2(max(_OasisLampExposureStops0,0)*amount); return art.rgb*_OasisReflectionUnlitArtworkStrength+max(exposed-art.rgb,0)*_OasisReflectionLitLampStrength; }
            float3 ReconstructFace1(float2 uv) { uv=TransformFaceUv(uv,_OasisFaceRotationQuarterTurns1,_OasisFaceFlipHorizontal1); half4 art=SAMPLE_TEXTURE2D(_OasisArtworkTex1,sampler_OasisArtworkTex0,uv); half4 mask=SAMPLE_TEXTURE2D(_OasisMaskTex1,sampler_OasisArtworkTex0,uv); half4 ids=SAMPLE_TEXTURE2D(_OasisLampIds0Tex1,sampler_OasisLampIds0Tex0,uv); half4 weights=SAMPLE_TEXTURE2D(_OasisLampWeights0Tex1,sampler_OasisLampIds0Tex0,uv); float amount=saturate(max(mask.r,max(mask.g,mask.b))*mask.a*_OasisMaskStrength1)*saturate(Lamp(ids.r)*weights.r+Lamp(ids.g)*weights.g+Lamp(ids.b)*weights.b); float3 exposed=art.rgb*exp2(max(_OasisLampExposureStops1,0)*amount); return art.rgb*_OasisReflectionUnlitArtworkStrength+max(exposed-art.rgb,0)*_OasisReflectionLitLampStrength; }
            float3 ReconstructFace2(float2 uv) { uv=TransformFaceUv(uv,_OasisFaceRotationQuarterTurns2,_OasisFaceFlipHorizontal2); half4 art=SAMPLE_TEXTURE2D(_OasisArtworkTex2,sampler_OasisArtworkTex0,uv); half4 mask=SAMPLE_TEXTURE2D(_OasisMaskTex2,sampler_OasisArtworkTex0,uv); half4 ids=SAMPLE_TEXTURE2D(_OasisLampIds0Tex2,sampler_OasisLampIds0Tex0,uv); half4 weights=SAMPLE_TEXTURE2D(_OasisLampWeights0Tex2,sampler_OasisLampIds0Tex0,uv); float amount=saturate(max(mask.r,max(mask.g,mask.b))*mask.a*_OasisMaskStrength2)*saturate(Lamp(ids.r)*weights.r+Lamp(ids.g)*weights.g+Lamp(ids.b)*weights.b); float3 exposed=art.rgb*exp2(max(_OasisLampExposureStops2,0)*amount); return art.rgb*_OasisReflectionUnlitArtworkStrength+max(exposed-art.rgb,0)*_OasisReflectionLitLampStrength; }
            float3 ReconstructFace3(float2 uv) { uv=TransformFaceUv(uv,_OasisFaceRotationQuarterTurns3,_OasisFaceFlipHorizontal3); half4 art=SAMPLE_TEXTURE2D(_OasisArtworkTex3,sampler_OasisArtworkTex0,uv); half4 mask=SAMPLE_TEXTURE2D(_OasisMaskTex3,sampler_OasisArtworkTex0,uv); half4 ids=SAMPLE_TEXTURE2D(_OasisLampIds0Tex3,sampler_OasisLampIds0Tex0,uv); half4 weights=SAMPLE_TEXTURE2D(_OasisLampWeights0Tex3,sampler_OasisLampIds0Tex0,uv); float amount=saturate(max(mask.r,max(mask.g,mask.b))*mask.a*_OasisMaskStrength3)*saturate(Lamp(ids.r)*weights.r+Lamp(ids.g)*weights.g+Lamp(ids.b)*weights.b); float3 exposed=art.rgb*exp2(max(_OasisLampExposureStops3,0)*amount); return art.rgb*_OasisReflectionUnlitArtworkStrength+max(exposed-art.rgb,0)*_OasisReflectionLitLampStrength; }
            float3 ReconstructFace(int source,float2 uv) { float3 colour=float3(0,0,0); if(source==0)colour=ReconstructFace0(uv);else if(source==1)colour=ReconstructFace1(uv);else if(source==2)colour=ReconstructFace2(uv);else if(source==3)colour=ReconstructFace3(uv);return colour; }
            bool HitPlane(float3 origin,float3 ray,float4 planeOrigin,float4 right,float4 up,float4 normal,float4 size,out float t,out float2 uv) { t=0;uv=0;if(size.x<=0||size.y<=0)return false;float d=dot(ray,normal.xyz);if(d>=-1e-5)return false;t=dot(planeOrigin.xyz-origin,normal.xyz)/d;if(t<=1e-5)return false;float3 relative=origin+ray*t-planeOrigin.xyz;uv=float2(dot(relative,right.xyz)/size.x,dot(relative,up.xyz)/size.y);return !any(uv<0)&&!any(uv>1); }

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float4 shadowCoord:TEXCOORD4; half fogFactor:TEXCOORD5; };
            Varyings vert(Attributes v) { Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz); VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS); o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=n.normalWS; o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale()); o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.shadowCoord=TransformWorldToShadowCoord(p.positionWS); o.fogFactor=ComputeFogFactor(p.positionCS.z); return o; }

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
                if (_OasisReflectionEnabled<0.5 || _OasisReflectionSourceCount<0.5) return cabinet;
                float3 incident=SafeNormalize(i.positionWS-_WorldSpaceCameraPos); float3 reflected=reflect(incident,normalWS); float nearest=1e30; float2 planeUv=0; int selected=-1; float t; float2 candidate;
                if (_OasisReflectionSourceCount>0.5 && HitPlane(i.positionWS,reflected,_OasisFacePlaneOriginWS0,_OasisFacePlaneRightWS0,_OasisFacePlaneUpWS0,_OasisFacePlaneNormalWS0,_OasisFacePlaneSize0,t,candidate) && t<nearest){nearest=t;planeUv=candidate;selected=0;}
                if (_OasisReflectionSourceCount>1.5 && HitPlane(i.positionWS,reflected,_OasisFacePlaneOriginWS1,_OasisFacePlaneRightWS1,_OasisFacePlaneUpWS1,_OasisFacePlaneNormalWS1,_OasisFacePlaneSize1,t,candidate) && t<nearest){nearest=t;planeUv=candidate;selected=1;}
                if (_OasisReflectionSourceCount>2.5 && HitPlane(i.positionWS,reflected,_OasisFacePlaneOriginWS2,_OasisFacePlaneRightWS2,_OasisFacePlaneUpWS2,_OasisFacePlaneNormalWS2,_OasisFacePlaneSize2,t,candidate) && t<nearest){nearest=t;planeUv=candidate;selected=2;}
                if (_OasisReflectionSourceCount>3.5 && HitPlane(i.positionWS,reflected,_OasisFacePlaneOriginWS3,_OasisFacePlaneRightWS3,_OasisFacePlaneUpWS3,_OasisFacePlaneNormalWS3,_OasisFacePlaneSize3,t,candidate) && t<nearest){nearest=t;planeUv=candidate;selected=3;}
                if(selected<0)return cabinet; float2 uv=saturate(planeUv+normalTS.xy*_OasisReflectionDistortion); float2 d=float2(_OasisReflectionRoughness*0.006,0); float3 reflectedColour=ReconstructFace(selected,uv);
                if(_OasisReflectionRoughness>0.001)reflectedColour=(reflectedColour*2+ReconstructFace(selected,saturate(uv+d))+ReconstructFace(selected,saturate(uv-d))+ReconstructFace(selected,saturate(uv+d.yx))+ReconstructFace(selected,saturate(uv-d.yx)))/6;
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
