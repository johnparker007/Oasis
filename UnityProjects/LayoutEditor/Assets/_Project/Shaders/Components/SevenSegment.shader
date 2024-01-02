Shader "Components/Unlit/SevenSegment" 
{
    Properties
    {
        _Texture1("Texture 1", 2D) = "white" {}
        _Texture2("Texture 2", 2D) = "white" {}

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
            float4 _Texture1_ST;
            float4 _Texture2_ST;

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
                + (1 - tex2D(_Texture2, i.uv).a) * _SegmentBrightness7;

                color *= _Color;

                return color;
            }
            ENDCG
        }
    }
}
