#ifndef OASIS_FACE_LAMP_COMMON_INCLUDED
#define OASIS_FACE_LAMP_COMMON_INCLUDED

// Shared face-artwork lamp reconstruction. This is kept independent of the
// forward-lighting code so it can also be reused by analytic cabinet reflections.
TEXTURE2D(_OasisMaskTex);
SAMPLER(sampler_OasisMaskTex);
TEXTURE2D(_OasisLampIds0Tex);
SAMPLER(sampler_OasisLampIds0Tex);
TEXTURE2D(_OasisLampWeights0Tex);
SAMPLER(sampler_OasisLampWeights0Tex);
TEXTURE2D(_OasisLampStateTex);
SAMPLER(sampler_OasisLampStateTex);

float2 TransformFaceUv(float2 uv)
{
    float turns = floor(_OasisFaceRotationQuarterTurns + 0.5);
    float2 transformed = uv;
    if (turns == 1.0)
    {
        transformed = float2(1.0 - uv.y, uv.x);
    }
    else if (turns == 2.0)
    {
        transformed = float2(1.0 - uv.x, 1.0 - uv.y);
    }
    else if (turns == 3.0)
    {
        transformed = float2(uv.y, 1.0 - uv.x);
    }

    if (_OasisFaceFlipHorizontal >= 0.5)
    {
        transformed.x = 1.0 - transformed.x;
    }

    return transformed;
}

float DecodeLampBrightness(float lampId)
{
    float decoded = floor(saturate(lampId) * 255.0 + 0.5);
    if (decoded < 1.0 || decoded > 255.0) return 0.0;
    float u = (decoded + 0.5) / 256.0;
    return SAMPLE_TEXTURE2D(_OasisLampStateTex, sampler_OasisLampStateTex, float2(u, 0.5)).r;
}

float DecodeWeight(float weight)
{
    return floor(saturate(weight) * 255.0 + 0.5) / 255.0;
}

float DecodeMask(half4 mask)
{
    float grayscale = max(mask.r, max(mask.g, mask.b));
    return saturate(grayscale * mask.a * _OasisMaskStrength);
}

float AccumulateLampAmount(float2 uv)
{
    half4 mask = SAMPLE_TEXTURE2D(_OasisMaskTex, sampler_OasisMaskTex, uv);
    half4 lampIds = SAMPLE_TEXTURE2D(_OasisLampIds0Tex, sampler_OasisLampIds0Tex, uv);
    half4 weights = SAMPLE_TEXTURE2D(_OasisLampWeights0Tex, sampler_OasisLampWeights0Tex, uv);

    float visibleLight = 0.0;
    visibleLight += DecodeLampBrightness(lampIds.r) * DecodeWeight(weights.r);
    visibleLight += DecodeLampBrightness(lampIds.g) * DecodeWeight(weights.g);
    visibleLight += DecodeLampBrightness(lampIds.b) * DecodeWeight(weights.b);
    return DecodeMask(mask) * saturate(visibleLight);
}

float3 EvaluateLampExposure(float3 artworkRgb, float lampAmount)
{
    float exposureStops = max(_OasisLampExposureStops, 0.0) * saturate(lampAmount);
    float exposureGain = exp2(exposureStops);
    return artworkRgb * exposureGain;
}

#endif
