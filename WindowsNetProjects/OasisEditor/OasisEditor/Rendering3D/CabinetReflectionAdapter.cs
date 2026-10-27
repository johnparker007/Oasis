using System.Numerics;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Avalonia.Rendering3D;

/// <summary>Maps authored Cabinet reflection definitions onto a receiver material in the portable scene.</summary>
public static class CabinetReflectionAdapter
{
    public static bool TryAttach(SceneRenderMesh receiverMesh, string receiverNodePath, int materialSlot,
        CabinetDocument document, Matrix4x4 cabinetTransform,
        IReadOnlyDictionary<string, SceneTexture> sourceFaceTextures,
        IReadOnlyDictionary<string, SceneTexture>? sourceLampTextures,
        IReadOnlyDictionary<string, SceneTexture>? visibilityMasks,
        out SceneRenderMesh attached, out string? diagnostic)
    {
        attached = receiverMesh;
        diagnostic = null;
        var definition = (document.Reflections ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.TargetId?.Trim(), receiverNodePath?.Trim(), StringComparison.Ordinal)
            && candidate.MaterialSlot == materialSlot);
        if (definition is null) return false;

        definition = definition.Normalized();
        if (!definition.Settings.Enabled) return false;
        if (definition.Sources.Length is < 1 or > CabinetReflectionContract.MaximumSources)
        { diagnostic = $"Reflection '{definition.Id}' must have 1–{CabinetReflectionContract.MaximumSources} sources."; return false; }

        var sources = new List<SceneReflectionSource>(definition.Sources.Length);
        foreach (var source in definition.Sources)
        {
            if (!sourceFaceTextures.TryGetValue(source.SourceSurfaceTargetId, out var texture))
            { diagnostic = $"Reflection '{definition.Id}' source '{source.SourceSurfaceTargetId}' has no mounted Face texture."; return false; }
            if (!CabinetReflectionPlaneValidation.TryValidate(source.Plane, out var planeError))
            { diagnostic = $"Reflection '{definition.Id}' source '{source.SourceSurfaceTargetId}' is invalid: {planeError}"; return false; }
            if (!TryTransformPlane(source.Plane, cabinetTransform, out var origin, out var right, out var up, out var normal, out var width, out var height))
            { diagnostic = $"Reflection '{definition.Id}' source '{source.SourceSurfaceTargetId}' could not be transformed."; return false; }

            var surface = (document.SurfaceTargetSettings ?? []).FirstOrDefault(item =>
                string.Equals(item.TargetId, source.SourceSurfaceTargetId, StringComparison.Ordinal))?.Normalized()
                ?? CabinetSurfaceTargetSettings.Default(source.SourceSurfaceTargetId);
            if (!TryGetUvQuarterTurns(surface.FaceRotation, out var turns))
            { diagnostic = $"Reflection '{definition.Id}' source '{source.SourceSurfaceTargetId}' has an unsupported Face rotation."; return false; }
            var visibleNormal = surface.FrontSide == CabinetSurfaceTargetSettings.InvertedFrontSide ? -normal : normal;
            SceneTexture? lampTexture = null;
            if (sourceLampTextures is not null) sourceLampTextures.TryGetValue(source.SourceSurfaceTargetId, out lampTexture);
            sources.Add(new(origin, right, up, visibleNormal, width, height, source.SourceSurfaceTargetId, texture, lampTexture, turns, surface.FaceFlipHorizontal));
        }

        SceneTexture? mask = null;
        if (!string.IsNullOrWhiteSpace(definition.VisibilityMask))
        {
            if (visibilityMasks is null || !visibilityMasks.TryGetValue(definition.VisibilityMask, out mask))
            { diagnostic = $"Reflection '{definition.Id}' visibility mask '{definition.VisibilityMask}' is unavailable."; return false; }
        }

        var reflectionSettings = definition.Settings.Normalized();
        attached = receiverMesh with
        {
            Reflection = new SceneReflectionBinding(reflectionSettings.Enabled, (float)reflectionSettings.Strength,
                (float)reflectionSettings.FresnelPower, (float)reflectionSettings.FresnelStrength,
                (float)reflectionSettings.UnlitArtworkStrength, (float)reflectionSettings.LitLampStrength,
                (float)reflectionSettings.Roughness, (float)reflectionSettings.Distortion,
                (float)reflectionSettings.EdgeFade, sources, mask)
        };
        return true;
    }

    public static SceneReflectionBinding WithSourceTexture(SceneReflectionBinding binding, string sourceTargetId, SceneTexture texture, bool lampOverlay = false)
    {
        var sources = binding.Sources.Select(source =>
        {
            if (string.Equals(sourceTargetId, source.SourceSurfaceTargetId, StringComparison.Ordinal))
                return lampOverlay ? source with { LampOverlayTexture = texture } : source with { Texture = texture };
            return source;
        }).ToArray();
        return sources.Where((source, index) => !ReferenceEquals(source, binding.Sources[index])).Any()
            ? binding with { Sources = sources } : binding;
    }

    private static bool TryTransformPlane(CabinetReflectionPlane plane, Matrix4x4 transform,
        out Vector3 origin, out Vector3 right, out Vector3 up, out Vector3 normal, out float width, out float height)
    {
        var rawOrigin = ToVector(plane.Origin);
        var rawRight = ToVector(plane.Right);
        var rawUp = ToVector(plane.Up);
        origin = Vector3.Transform(rawOrigin, transform);
        var rightSpan = Vector3.TransformNormal(Vector3.Normalize(rawRight) * (float)plane.Width, transform);
        var upSpan = Vector3.TransformNormal(Vector3.Normalize(rawUp) * (float)plane.Height, transform);
        width = rightSpan.Length(); height = upSpan.Length();
        right = width > 1e-6f ? rightSpan / width : Vector3.Zero;
        up = height > 1e-6f ? upSpan / height : Vector3.Zero;
        var sourceNormal = plane.Normal is null ? Vector3.Cross(rawRight, rawUp) : ToVector(plane.Normal);
        normal = Vector3.TransformNormal(sourceNormal, transform);
        if (!float.IsFinite(normal.X) || !float.IsFinite(normal.Y) || !float.IsFinite(normal.Z) || normal.LengthSquared() < 1e-10f)
        { normal = Vector3.Zero; return false; }
        normal = Vector3.Normalize(normal);
        return IsFinite(origin) && IsFinite(right) && IsFinite(up) && float.IsFinite(width) && float.IsFinite(height)
            && width > 1e-6f && height > 1e-6f && MathF.Abs(Vector3.Dot(right, up)) < 1e-4f
            && MathF.Abs(Vector3.Dot(normal, right)) < 1e-4f && MathF.Abs(Vector3.Dot(normal, up)) < 1e-4f;
    }

    private static bool TryGetUvQuarterTurns(int degrees, out int turns)
    {
        turns = degrees switch { 0 => 0, 90 => 1, 180 => 2, 270 => 3, _ => -1 };
        return turns >= 0;
    }

    private static Vector3 ToVector(CabinetReflectionVector value) => new((float)value.X, (float)value.Y, (float)value.Z);
    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
