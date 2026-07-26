using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public static class CabinetReflectionPlaneDeriver
{
    public static bool TryDerive(CabinetFaceTarget? target, out CabinetReflectionPlane plane, out string error)
    {
        plane = default!; error = string.Empty;
        if (target is null || !target.IsValid || target.Corners.Count != 4) { error = target?.ErrorMessage ?? "The source Face target is unavailable or is not a rectangular quad."; return false; }
        var origin = target.Corners[0]; var rightSpan = target.Corners[1] - origin; var upSpan = target.Corners[3] - origin;
        if (rightSpan.Length < 1e-6 || upSpan.Length < 1e-6) { error = "The source Face target has degenerate edges."; return false; }
        rightSpan.Normalize(); upSpan.Normalize();
        plane = new CabinetReflectionPlane(new(origin.X, origin.Y, origin.Z), new(rightSpan.X, rightSpan.Y, rightSpan.Z), new(upSpan.X, upSpan.Y, upSpan.Z), (target.Corners[1] - origin).Length, (target.Corners[3] - origin).Length);
        return CabinetReflectionPlaneValidation.TryValidate(plane, out error);
    }
}
