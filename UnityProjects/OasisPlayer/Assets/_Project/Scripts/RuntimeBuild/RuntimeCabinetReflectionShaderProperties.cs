using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeCabinetReflectionShaderProperties
    {
        public const string ShaderName = "Oasis/CabinetAnalyticReflection";
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int NormalMap = Shader.PropertyToID("_BumpMap");
        public static readonly int NormalScale = Shader.PropertyToID("_BumpScale");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int VisibilityMask = Shader.PropertyToID("_OasisReflectionVisibilityMaskTex");
        public static readonly int Enabled = Shader.PropertyToID("_OasisReflectionEnabled");
        public static readonly int FaceOrigin = Shader.PropertyToID("_OasisFacePlaneOriginWS");
        public static readonly int FaceRight = Shader.PropertyToID("_OasisFacePlaneRightWS");
        public static readonly int FaceUp = Shader.PropertyToID("_OasisFacePlaneUpWS");
        public static readonly int FaceNormal = Shader.PropertyToID("_OasisFacePlaneNormalWS");
        public static readonly int FaceSize = Shader.PropertyToID("_OasisFacePlaneSize");
        public static readonly int Strength = Shader.PropertyToID("_OasisReflectionStrength");
        public static readonly int UnlitArtworkStrength = Shader.PropertyToID("_OasisReflectionUnlitArtworkStrength");
        public static readonly int LitLampStrength = Shader.PropertyToID("_OasisReflectionLitLampStrength");
        public static readonly int FresnelPower = Shader.PropertyToID("_OasisReflectionFresnelPower");
        public static readonly int FresnelStrength = Shader.PropertyToID("_OasisReflectionFresnelStrength");
        public static readonly int Roughness = Shader.PropertyToID("_OasisReflectionRoughness");
        public static readonly int Distortion = Shader.PropertyToID("_OasisReflectionDistortion");
        public static readonly int EdgeFade = Shader.PropertyToID("_OasisReflectionEdgeFade");
    }
}
