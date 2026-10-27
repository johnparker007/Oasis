namespace OasisEditor.Features.CabinetEditor.Services;

public enum CabinetSemanticGeometryKind
{
    Visual,
    FaceTarget,
    Collider,
    Trigger
}

public static class CabinetSemanticGeometry
{
    public const string FaceTargetPrefix = "OasisFace_";
    public const string ColliderPrefix = "OasisCollider_";
    public const string TriggerPrefix = "OasisTrigger_";

    public static CabinetSemanticGeometryKind Classify(string? nodeName, string? meshName)
    {
        var nodeKind = ClassifyName(nodeName);
        return nodeKind != CabinetSemanticGeometryKind.Visual ? nodeKind : ClassifyName(meshName);
    }

    /// <summary>Returns the semantic declaration which wins classification (node before mesh).</summary>
    public static string? GetWinningSemanticName(string? nodeName, string? meshName)
    {
        return ClassifyName(nodeName) != CabinetSemanticGeometryKind.Visual ? nodeName
            : ClassifyName(meshName) != CabinetSemanticGeometryKind.Visual ? meshName
            : null;
    }

    public static string? GetSemanticId(string? nodeName, string? meshName, CabinetSemanticGeometryKind expectedKind)
    {
        if (Classify(nodeName, meshName) != expectedKind) return null;
        var name = GetWinningSemanticName(nodeName, meshName);
        var prefix = expectedKind switch
        {
            CabinetSemanticGeometryKind.FaceTarget => FaceTargetPrefix,
            CabinetSemanticGeometryKind.Collider => ColliderPrefix,
            CabinetSemanticGeometryKind.Trigger => TriggerPrefix,
            _ => string.Empty
        };
        return name is not null && prefix.Length > 0 ? name[prefix.Length..] : null;
    }

    public static CabinetSemanticGeometryKind ClassifyName(string? name)
    {
        if (name?.StartsWith(FaceTargetPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.FaceTarget;
        if (name?.StartsWith(ColliderPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.Collider;
        if (name?.StartsWith(TriggerPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.Trigger;
        return CabinetSemanticGeometryKind.Visual;
    }
}
