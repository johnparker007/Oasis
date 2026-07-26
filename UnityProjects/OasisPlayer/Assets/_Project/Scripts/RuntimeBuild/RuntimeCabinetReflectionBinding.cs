using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class RuntimeCabinetReflectionBinding
    {
        public static void Apply(Material material, RuntimeFace face, RuntimeLampStateTexture lampState, RuntimeCabinetReflectionPlane plane, float lampExposureStops = 2.5f, float maskStrength = 1f)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (face == null) throw new ArgumentNullException(nameof(face));
            if (face.Artwork == null || face.Artwork.Texture == null || face.Mask == null || face.Mask.Texture == null)
                throw new ArgumentException("Reflection source Face requires artwork and mask textures.", nameof(face));

            SetTexture(material, RuntimeFaceShaderProperties.ArtworkTexture, face.Artwork.Texture);
            SetTexture(material, RuntimeFaceShaderProperties.MaskTexture, face.Mask.Texture);
            if (face.LampIds0 != null) SetTexture(material, RuntimeFaceShaderProperties.LampIds0Texture, face.LampIds0.Texture);
            if (face.LampWeights0 != null) SetTexture(material, RuntimeFaceShaderProperties.LampWeights0Texture, face.LampWeights0.Texture);
            if (lampState != null && lampState.Texture != null) SetTexture(material, RuntimeFaceShaderProperties.LampStateTexture, lampState.Texture);

            var orientation = RuntimeFaceTextureOrientation.FromReference(face.Reference);
            material.SetFloat(RuntimeFaceShaderProperties.LampExposureStops, Mathf.Max(0f, lampExposureStops));
            material.SetFloat(RuntimeFaceShaderProperties.MaskStrength, Mathf.Max(0f, maskStrength));
            material.SetFloat(RuntimeFaceShaderProperties.FaceRotationQuarterTurns, orientation.UnityUvQuarterTurns);
            material.SetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal, orientation.FlipHorizontal ? 1f : 0f);
            material.SetVector(RuntimeCabinetReflectionShaderProperties.FacePlaneOrigin, plane.Origin);
            material.SetVector(RuntimeCabinetReflectionShaderProperties.FacePlaneRight, plane.Right);
            material.SetVector(RuntimeCabinetReflectionShaderProperties.FacePlaneUp, plane.Up);
            material.SetVector(RuntimeCabinetReflectionShaderProperties.FacePlaneNormal, plane.Normal);
            material.SetVector(RuntimeCabinetReflectionShaderProperties.FacePlaneSize, new Vector4(plane.Width, plane.Height, 0f, 0f));
        }

        private static void SetTexture(Material material, int propertyId, Texture texture)
        {
            material.SetTexture(propertyId, texture);
            material.SetTextureScale(propertyId, Vector2.one);
            material.SetTextureOffset(propertyId, Vector2.zero);
        }
    }
}
