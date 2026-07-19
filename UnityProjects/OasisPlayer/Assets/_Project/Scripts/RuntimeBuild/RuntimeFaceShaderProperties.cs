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
        public const string EmissionStrengthName = "_OasisEmissionStrength";
        public const string StaticBrightnessName = "_OasisStaticBrightness";
        public const string LampLiftName = "_OasisLampLift";
        public const string MaskStrengthName = "_OasisMaskStrength";

        public static readonly int ArtworkTexture = Shader.PropertyToID(ArtworkTextureName);
        public static readonly int MaskTexture = Shader.PropertyToID(MaskTextureName);
        public static readonly int TrayIdTexture = Shader.PropertyToID(TrayIdTextureName);
        public static readonly int LampIds0Texture = Shader.PropertyToID(LampIds0TextureName);
        public static readonly int LampWeights0Texture = Shader.PropertyToID(LampWeights0TextureName);
        public static readonly int LampStateTexture = Shader.PropertyToID(LampStateTextureName);
        public static readonly int EmissionStrength = Shader.PropertyToID(EmissionStrengthName);
        public static readonly int StaticBrightness = Shader.PropertyToID(StaticBrightnessName);
        public static readonly int LampLift = Shader.PropertyToID(LampLiftName);
        public static readonly int MaskStrength = Shader.PropertyToID(MaskStrengthName);
    }
}
