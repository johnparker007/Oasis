Shader "Oasis/SegmentedDisplay"
{
    Properties
    {
        _SegmentMaskLow ("Segment Mask Low", Float) = 0
        _SegmentMaskHigh ("Segment Mask High", Float) = 0
        _OnColor ("On Color", Color) = (1,0,0,1)
        _OffColor ("Off Color", Color) = (0.05,0,0,1)
        _ActiveEmission ("Active Emission", Float) = 2.5
        _InactiveEmission ("Inactive Emission", Float) = 0.05
        _Brightness ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv2 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float segmentIndex : TEXCOORD0; };
            float _SegmentMaskLow; float _SegmentMaskHigh; float4 _OnColor; float4 _OffColor; float _ActiveEmission; float _InactiveEmission; float _Brightness;
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.segmentIndex = v.uv2.x; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float idx = floor(i.segmentIndex + 0.5);
                float mask = idx < 8.0 ? _SegmentMaskLow : _SegmentMaskHigh;
                float shiftedIndex = idx < 8.0 ? idx : idx - 8.0;
                float bitValue = pow(2.0, shiftedIndex);
                float lit = fmod(floor(mask / bitValue), 2.0);
                float4 color = lerp(_OffColor * _InactiveEmission, _OnColor * _ActiveEmission * _Brightness, lit);
                return color;
            }
            ENDCG
        }
    }
}
