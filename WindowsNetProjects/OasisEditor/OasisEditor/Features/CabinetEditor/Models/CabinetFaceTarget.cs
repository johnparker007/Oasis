using System.Numerics;

namespace OasisEditor.Features.CabinetEditor.Models;

public sealed record CabinetFaceTarget(
    string Id,
    string SourceName,
    string DisplayName,
    IReadOnlyList<Vector3> Corners,
    Vector3 Normal,
    Vector3 Center,
    bool IsValid,
    string? ErrorMessage,
    Vector3? UvOrigin = null,
    Vector3? UvRightSpan = null,
    Vector3? UvUpSpan = null);
