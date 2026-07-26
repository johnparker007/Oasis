using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeCabinetReflectionShaderProperties
    {
        public const string ShaderName = "Oasis/Cabinet Analytic Reflection";
        public static readonly int ReflectionStrength = Shader.PropertyToID("_OasisReflectionStrength");
        public static readonly int ReflectionVisibilityMask = Shader.PropertyToID("_OasisReflectionVisibilityMask");
        public static readonly int FacePlaneOrigin = Shader.PropertyToID("_OasisFacePlaneOriginWS");
        public static readonly int FacePlaneRight = Shader.PropertyToID("_OasisFacePlaneRightWS");
        public static readonly int FacePlaneUp = Shader.PropertyToID("_OasisFacePlaneUpWS");
        public static readonly int FacePlaneNormal = Shader.PropertyToID("_OasisFacePlaneNormalWS");
        public static readonly int FacePlaneSize = Shader.PropertyToID("_OasisFacePlaneSize");
    }
}
