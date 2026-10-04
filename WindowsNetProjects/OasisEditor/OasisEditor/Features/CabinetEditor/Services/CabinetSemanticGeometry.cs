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

    public static CabinetSemanticGeometryKind ClassifyName(string? name)
    {
        if (name?.StartsWith(FaceTargetPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.FaceTarget;
        if (name?.StartsWith(ColliderPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.Collider;
        if (name?.StartsWith(TriggerPrefix, StringComparison.Ordinal) == true) return CabinetSemanticGeometryKind.Trigger;
        return CabinetSemanticGeometryKind.Visual;
    }
}
