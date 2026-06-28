using System.Windows.Media.Media3D;

namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetFaceTarget(
    string Id,
    string SourceName,
    string DisplayName,
    IReadOnlyList<Point3D> Corners,
    Vector3D Normal,
    Point3D Center,
    bool IsValid,
    string? ErrorMessage);
