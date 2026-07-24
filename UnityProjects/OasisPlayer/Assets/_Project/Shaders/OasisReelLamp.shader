Shader "Oasis/ReelLamp"
{
    Properties
    {
        _MainTex ("Reel Band", 2D) = "white" {}
        _OasisReelTransmissionMaskTex ("Transmission Mask", 2D) = "white" {}
        _OasisReelTransmissionMaskEnabled ("Transmission Mask Enabled", Float) = 0
        _OasisReelLampBrightness ("Lamp Brightness", Vector) = (0,0,0,0)
        _OasisReelLampCenters ("Lamp Centers", Vector) = (0.5,0.1667,0.5,0.5)
        _OasisReelLampRadii ("Lamp Radii", Vector) = (0.42,0.42,0.42,0)
        _OasisReelLampIntensities ("Lamp Intensities", Vector) = (1,1,1,0)
        _OasisReelLampColor ("Lamp Color", Color) = (1.0,0.82,0.55,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150

        CGPROGRAM
        #pragma surface surf Lambert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _OasisReelTransmissionMaskTex;
        float _OasisReelTransmissionMaskEnabled;
        float4 _OasisReelLampBrightness;
        float4 _OasisReelLampCenters;
        float4 _OasisReelLampRadii;
        float4 _OasisReelLampIntensities;
        fixed4 _OasisReelLampColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        float Field(float2 windowUv, float2 center, float radius)
        {
            float d = distance(windowUv, center);
            return 1.0 - smoothstep(radius * 0.35, max(radius, 0.0001), d);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // The generated reel cylinder rotates as a GameObject. Mesh UVs are band coordinates,
            // so artwork and mask travel with symbols. The lamp field uses local X/Y aperture
            // coordinates instead, keeping lamp centres fixed in the visible reel window.
            float2 bandUv = IN.uv_MainTex;
            fixed4 base = tex2D(_MainTex, bandUv);
            float2 windowUv = saturate(float2(bandUv.x, frac(IN.worldPos.y + 0.5)));
            float mask = lerp(1.0, tex2D(_OasisReelTransmissionMaskTex, bandUv).r, saturate(_OasisReelTransmissionMaskEnabled));
            float lamp = 0.0;
            lamp += Field(windowUv, _OasisReelLampCenters.xy, _OasisReelLampRadii.x) * _OasisReelLampBrightness.x * _OasisReelLampIntensities.x;
            lamp += Field(windowUv, _OasisReelLampCenters.zw, _OasisReelLampRadii.y) * _OasisReelLampBrightness.y * _OasisReelLampIntensities.y;
            lamp += Field(windowUv, float2(0.5, 0.8333), _OasisReelLampRadii.z) * _OasisReelLampBrightness.z * _OasisReelLampIntensities.z;
            o.Albedo = base.rgb;
            o.Alpha = base.a;
            o.Emission = base.rgb * _OasisReelLampColor.rgb * lamp * mask;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
