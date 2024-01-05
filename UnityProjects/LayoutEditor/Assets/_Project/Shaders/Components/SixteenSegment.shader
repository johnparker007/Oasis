Shader "Components/Unlit/SixteenSegment"
{
    Properties
    {
        _Texture1("Texture 1", 2D) = "white" {}
        _Texture2("Texture 2", 2D) = "white" {}
        _Texture3("Texture 3", 2D) = "white" {}
        _Texture4("Texture 4", 2D) = "white" {}
        _Texture5("Texture 5", 2D) = "white" {}

        _Color("Color", Color) = (1,1,1,1)

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
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

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

            float4 _Color;

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


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _Texture1); // do I get away with this?  Should be same for both textures
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color =
                    _BackgroundBrightness
                // texture 1
                + tex2D(_Texture1, i.uv).r * _SegmentBrightness0
                + tex2D(_Texture1, i.uv).g * _SegmentBrightness1
                + tex2D(_Texture1, i.uv).b * _SegmentBrightness2
                + (1 - tex2D(_Texture1, i.uv).a) * _SegmentBrightness3
                // texture 2
                + tex2D(_Texture2, i.uv).r * _SegmentBrightness4
                + tex2D(_Texture2, i.uv).g * _SegmentBrightness5
                + tex2D(_Texture2, i.uv).b * _SegmentBrightness6
                + (1 - tex2D(_Texture2, i.uv).a) * _SegmentBrightness7
                // texture 3
                + tex2D(_Texture3, i.uv).r * _SegmentBrightness8
                + tex2D(_Texture3, i.uv).g * _SegmentBrightness9
                + tex2D(_Texture3, i.uv).b * _SegmentBrightness10
                + (1 - tex2D(_Texture3, i.uv).a) * _SegmentBrightness11
                // texture 4
                + tex2D(_Texture4, i.uv).r * _SegmentBrightness12
                + tex2D(_Texture4, i.uv).g * _SegmentBrightness13
                + tex2D(_Texture4, i.uv).b * _SegmentBrightness14
                + (1 - tex2D(_Texture4, i.uv).a) * _SegmentBrightness15
                // texture 5 (only technically need R and G channel for the last of the segments)
                + tex2D(_Texture5, i.uv).r * _SegmentBrightness16;
                //+ tex2D(_Texture5, i.uv).g * _SegmentBrightness17; // ignoringh comma for now
                //+ tex2D(_Texture2, i.uv).b * _SegmentBrightness6
                //+ (1 - tex2D(_Texture2, i.uv).a) * _SegmentBrightness7;

                color *= _Color;

                return color;
            }
            ENDCG
        }
    }
}