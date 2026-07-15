Shader "Custom/CarpetShader"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Base (RGB)", 2D) = "white" {}
        _BumpMap("Bump (RGBA)", 2D) = "bump" {}
        _BumpAmt("Bump Amount", Range(0.0, 1.0)) = 0.1
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" }

            LOD 200

            CGPROGRAM
            #pragma surface surf Lambert

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float _BumpAmt;
            fixed4 _Color;

            struct Input
            {
                float2 uv_MainTex;
                float2 uv_BumpMap;
            };

            void surf(Input IN, inout SurfaceOutput o)
            {
                // Sample the main texture and the bump map
                fixed4 mainTex = tex2D(_MainTex, IN.uv_MainTex);
                fixed4 bumpMap = tex2D(_BumpMap, IN.uv_BumpMap);

                // Calculate the bump amount
                float3 bump = _BumpAmt * UnpackNormal(bumpMap).rgb;

                // Offset the UV coordinates by the bump amount
                IN.uv_MainTex.xy += bump.xy;

                // Sample the main texture again with the bumped UV coordinates
                fixed4 carpet = tex2D(_MainTex, IN.uv_MainTex);

                // Multiply the carpet color by the base color
                o.Albedo = carpet.rgb * _Color.rgb;
            }
            ENDCG
        }
}
