using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeCabinetReflectionShaderProperties
    {
        public const string ShaderName = "Oasis/CabinetAnalyticReflection";
        public const int MaximumSources = 4;
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap"); public static readonly int BaseColor = Shader.PropertyToID("_BaseColor"); public static readonly int NormalMap = Shader.PropertyToID("_BumpMap"); public static readonly int NormalScale = Shader.PropertyToID("_BumpScale"); public static readonly int Smoothness = Shader.PropertyToID("_Smoothness"); public static readonly int Metallic = Shader.PropertyToID("_Metallic");
        public static readonly int VisibilityMask = Shader.PropertyToID("_OasisReflectionVisibilityMaskTex"); public static readonly int Enabled = Shader.PropertyToID("_OasisReflectionEnabled"); public static readonly int SourceCount = Shader.PropertyToID("_OasisReflectionSourceCount");
        public static readonly int Strength = Shader.PropertyToID("_OasisReflectionStrength"); public static readonly int UnlitArtworkStrength = Shader.PropertyToID("_OasisReflectionUnlitArtworkStrength"); public static readonly int LitLampStrength = Shader.PropertyToID("_OasisReflectionLitLampStrength"); public static readonly int FresnelPower = Shader.PropertyToID("_OasisReflectionFresnelPower"); public static readonly int FresnelStrength = Shader.PropertyToID("_OasisReflectionFresnelStrength"); public static readonly int Roughness = Shader.PropertyToID("_OasisReflectionRoughness"); public static readonly int Distortion = Shader.PropertyToID("_OasisReflectionDistortion"); public static readonly int EdgeFade = Shader.PropertyToID("_OasisReflectionEdgeFade");
        public static readonly int[] FaceOrigin = Build("_OasisFacePlaneOriginWS"); public static readonly int[] FaceRight = Build("_OasisFacePlaneRightWS"); public static readonly int[] FaceUp = Build("_OasisFacePlaneUpWS"); public static readonly int[] FaceNormal = Build("_OasisFacePlaneNormalWS"); public static readonly int[] FaceSize = Build("_OasisFacePlaneSize");
        public static readonly int[] Artwork = Build("_OasisArtworkTex"); public static readonly int[] Mask = Build("_OasisMaskTex"); public static readonly int[] LampIds = Build("_OasisLampIds0Tex"); public static readonly int[] LampWeights = Build("_OasisLampWeights0Tex"); public static readonly int[] Rotation = Build("_OasisFaceRotationQuarterTurns"); public static readonly int[] Flip = Build("_OasisFaceFlipHorizontal"); public static readonly int[] Exposure = Build("_OasisLampExposureStops"); public static readonly int[] MaskStrength = Build("_OasisMaskStrength");
        private static int[] Build(string prefix) { var result = new int[MaximumSources]; for (var i = 0; i < result.Length; i++) result[i] = Shader.PropertyToID(prefix + i); return result; }
        public static readonly int[] RequiredProperties = { BaseMap, BaseColor, NormalMap, NormalScale, Smoothness, Metallic, VisibilityMask, Enabled, SourceCount, Strength, UnlitArtworkStrength, LitLampStrength, FresnelPower, FresnelStrength, Roughness, Distortion, EdgeFade };
    }
}
