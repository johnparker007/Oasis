#ifndef OASIS_FACE_UV_ORIENTATION_INCLUDED
#define OASIS_FACE_UV_ORIENTATION_INCLUDED
// Reflection planes are derived from raw GLB TEXCOORD_0, while glTFast converts that
// convention before exposing UVs on Unity meshes. Authored rotation and flip remain
// exclusively owned by TransformFaceUv below.
float2 ConvertReflectionPlaneUvToUnityBaseUv(float2 uv)
{
    return 1.0 - uv;
}

float2 TransformFaceUv(float2 uv, float quarterTurns, float flipHorizontal)
{
    float turns = floor(quarterTurns + 0.5);
    if (turns == 1.0) uv = float2(1.0 - uv.y, uv.x);
    else if (turns == 2.0) uv = 1.0 - uv;
    else if (turns == 3.0) uv = float2(uv.y, 1.0 - uv.x);
    if (flipHorizontal >= 0.5) uv.x = 1.0 - uv.x;
    return uv;
}
#endif
