using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeFaceShaderProperties
    {
        public const string ShaderName = "Oasis/Face";
        public const string ArtworkTextureName = "_OasisArtworkTex";
        public const string MaskTextureName = "_OasisMaskTex";
        public const string TrayIdTextureName = "_OasisTrayIdTex";
        public const string LampIds0TextureName = "_OasisLampIds0Tex";
        public const string LampWeights0TextureName = "_OasisLampWeights0Tex";
        public const string LampStateTextureName = "_OasisLampStateTex";
        public const string LampExposureStopsName = "_OasisLampExposureStops";
        public const string StaticBrightnessName = "_OasisStaticBrightness";
        public const string BaseAmbientStrengthName = "_OasisBaseAmbientStrength";
        public const string BaseMainLightStrengthName = "_OasisBaseMainLightStrength";
        public const string BaseAdditionalLightStrengthName = "_OasisBaseAdditionalLightStrength";
        public const string MaskStrengthName = "_OasisMaskStrength";
        public const string NormalSignName = "_OasisNormalSign";
        public const string CullModeName = "_Cull";
        public const string FaceRotationQuarterTurnsName = "_OasisFaceRotationQuarterTurns";
        public const string FaceFlipHorizontalName = "_OasisFaceFlipHorizontal";

        public static readonly int ArtworkTexture = Shader.PropertyToID(ArtworkTextureName);
        public static readonly int MaskTexture = Shader.PropertyToID(MaskTextureName);
        public static readonly int TrayIdTexture = Shader.PropertyToID(TrayIdTextureName);
        public static readonly int LampIds0Texture = Shader.PropertyToID(LampIds0TextureName);
        public static readonly int LampWeights0Texture = Shader.PropertyToID(LampWeights0TextureName);
        public static readonly int LampStateTexture = Shader.PropertyToID(LampStateTextureName);
        public static readonly int LampExposureStops = Shader.PropertyToID(LampExposureStopsName);
        public static readonly int StaticBrightness = Shader.PropertyToID(StaticBrightnessName);
        public static readonly int BaseAmbientStrength = Shader.PropertyToID(BaseAmbientStrengthName);
        public static readonly int BaseMainLightStrength = Shader.PropertyToID(BaseMainLightStrengthName);
        public static readonly int BaseAdditionalLightStrength = Shader.PropertyToID(BaseAdditionalLightStrengthName);
        public static readonly int MaskStrength = Shader.PropertyToID(MaskStrengthName);
        public static readonly int NormalSign = Shader.PropertyToID(NormalSignName);
        public static readonly int CullMode = Shader.PropertyToID(CullModeName);
        public static readonly int FaceRotationQuarterTurns = Shader.PropertyToID(FaceRotationQuarterTurnsName);
        public static readonly int FaceFlipHorizontal = Shader.PropertyToID(FaceFlipHorizontalName);
    }
}
