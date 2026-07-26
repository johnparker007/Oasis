using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public static class CabinetReflectionPlaneDeriver
{
    public static bool TryDerive(CabinetFaceTarget? target, out CabinetReflectionPlane plane, out string error)
    {
        plane = default!; error = string.Empty;
        if (target is null || !target.IsValid || target.Corners.Count != 4) { error = target?.ErrorMessage ?? "The source Face target is unavailable or is not a rectangular quad."; return false; }
        if (target.UvOrigin is null || target.UvRightSpan is null || target.UvUpSpan is null) { error = "The source Face target has no canonical texture-coordinate mapping."; return false; }
        var origin = target.UvOrigin.Value; var rightSpan = target.UvRightSpan.Value; var upSpan = target.UvUpSpan.Value;
        if (rightSpan.Length < 1e-6 || upSpan.Length < 1e-6) { error = "The source Face target has degenerate edges."; return false; }
        var width = rightSpan.Length; var height = upSpan.Length;
        rightSpan.Normalize(); upSpan.Normalize();
        plane = new CabinetReflectionPlane(new(origin.X, origin.Y, origin.Z), new(rightSpan.X, rightSpan.Y, rightSpan.Z), new(upSpan.X, upSpan.Y, upSpan.Z), width, height, new(target.Normal.X, target.Normal.Y, target.Normal.Z));
        return CabinetReflectionPlaneValidation.TryValidate(plane, out error);
    }
}
