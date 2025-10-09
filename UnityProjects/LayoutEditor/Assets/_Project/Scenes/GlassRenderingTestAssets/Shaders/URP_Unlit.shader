Shader "Unlit/URP_Unlit"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _LampTex("Texture", 2D) = "white" {}
        _LampStrength("Lamp Strength", Range(0.0, 10.0)) = 5.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent" 
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        //ZWrite Off

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _LampTex;
            float4 _LampTex_ST;
            float _LampStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                fixed4 lampColor = tex2D(_LampTex, i.uv);

                float alpha = lampColor.r * _LampStrength;

                fixed4 color;
                color = max(baseColor, baseColor * _LampStrength);
                color.a = alpha;

                return color;
            }
            ENDCG
        }
    }
}
