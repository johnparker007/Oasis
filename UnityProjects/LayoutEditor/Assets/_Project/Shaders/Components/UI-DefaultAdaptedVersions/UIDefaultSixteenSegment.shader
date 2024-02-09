// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/UIDefaultSixteenSegment"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15

        _Texture1("Texture 1", 2D) = "white" {}
        _Texture2("Texture 2", 2D) = "white" {}
        _Texture3("Texture 3", 2D) = "white" {}
        _Texture4("Texture 4", 2D) = "white" {}
        _Texture5("Texture 5", 2D) = "white" {}

        _BackgroundBrightness("Background Brightness", Range(0.0, 1.0)) = 1

        _SegmentBrightness0("Segment Brightness 0", Range(0.0, 1.0)) = 1
        _SegmentBrightness1("Segment Brightness 1", Range(0.0, 1.0)) = 1
        _SegmentBrightness2("Segment Brightness 2", Range(0.0, 1.0)) = 1
        _SegmentBrightness3("Segment Brightness 3", Range(0.0, 1.0)) = 1
        _SegmentBrightness4("Segment Brightness 4", Range(0.0, 1.0)) = 1
        _SegmentBrightness5("Segment Brightness 5", Range(0.0, 1.0)) = 1
        _SegmentBrightness6("Segment Brightness 6", Range(0.0, 1.0)) = 1
        _SegmentBrightness7("Segment Brightness 7", Range(0.0, 1.0)) = 1
        _SegmentBrightness8("Segment Brightness 8", Range(0.0, 1.0)) = 1
        _SegmentBrightness9("Segment Brightness 9", Range(0.0, 1.0)) = 1
        _SegmentBrightness10("Segment Brightness 10", Range(0.0, 1.0)) = 1
        _SegmentBrightness11("Segment Brightness 11", Range(0.0, 1.0)) = 1
        _SegmentBrightness12("Segment Brightness 12", Range(0.0, 1.0)) = 1
        _SegmentBrightness13("Segment Brightness 13", Range(0.0, 1.0)) = 1
        _SegmentBrightness14("Segment Brightness 14", Range(0.0, 1.0)) = 1
        _SegmentBrightness15("Segment Brightness 15", Range(0.0, 1.0)) = 1
        _SegmentBrightness16("Segment Brightness 16", Range(0.0, 1.0)) = 1

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color : COLOR;
                half2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            sampler2D _Texture1;
            sampler2D _Texture2;
            sampler2D _Texture3;
            sampler2D _Texture4;
            sampler2D _Texture5;

            float4 _Texture1_ST;
            float4 _Texture2_ST;
            float4 _Texture3_ST;
            float4 _Texture4_ST;
            float4 _Texture5_ST;

            float _BackgroundBrightness;

            float _SegmentBrightness0;
            float _SegmentBrightness1;
            float _SegmentBrightness2;
            float _SegmentBrightness3;
            float _SegmentBrightness4;
            float _SegmentBrightness5;
            float _SegmentBrightness6;
            float _SegmentBrightness7;
            float _SegmentBrightness8;
            float _SegmentBrightness9;
            float _SegmentBrightness10;
            float _SegmentBrightness11;
            float _SegmentBrightness12;
            float _SegmentBrightness13;
            float _SegmentBrightness14;
            float _SegmentBrightness15;
            float _SegmentBrightness16;


            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = IN.texcoord;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1,1);
                #endif

                OUT.color = IN.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; //i added
            float4 _MainTex_ST; //i added

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color =
                    _BackgroundBrightness
                // texture 1
                + tex2D(_Texture1, IN.texcoord).r * _SegmentBrightness0
                + tex2D(_Texture1, IN.texcoord).g * _SegmentBrightness1
                + tex2D(_Texture1, IN.texcoord).b * _SegmentBrightness2
                + (1 - tex2D(_Texture1, IN.texcoord).a) * _SegmentBrightness3
                // texture 2
                + tex2D(_Texture2, IN.texcoord).r * _SegmentBrightness4
                + tex2D(_Texture2, IN.texcoord).g * _SegmentBrightness5
                + tex2D(_Texture2, IN.texcoord).b * _SegmentBrightness6
                + (1 - tex2D(_Texture2, IN.texcoord).a) * _SegmentBrightness7
                // texture 3
                + tex2D(_Texture3, IN.texcoord).r * _SegmentBrightness8
                + tex2D(_Texture3, IN.texcoord).g * _SegmentBrightness9
                + tex2D(_Texture3, IN.texcoord).b * _SegmentBrightness10
                + (1 - tex2D(_Texture3, IN.texcoord).a) * _SegmentBrightness11
                // texture 4
                + tex2D(_Texture4, IN.texcoord).r * _SegmentBrightness12
                + tex2D(_Texture4, IN.texcoord).g * _SegmentBrightness13
                + tex2D(_Texture4, IN.texcoord).b * _SegmentBrightness14
                + (1 - tex2D(_Texture4, IN.texcoord).a) * _SegmentBrightness15
                // texture 5 (only technically need R and G channel for the last of the segments)
                + tex2D(_Texture5, IN.texcoord).r * _SegmentBrightness16;
                //+ tex2D(_Texture5, i.uv).g * _SegmentBrightness17; // ignoringh comma for now
                //+ tex2D(_Texture2, i.uv).b * _SegmentBrightness6
                //+ (1 - tex2D(_Texture2, i.uv).a) * _SegmentBrightness7;

            color.a = 1;

            color *= _Color;




                //half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif







                return color;
            }
        ENDCG
        }
    }
}
