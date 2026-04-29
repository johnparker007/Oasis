namespace OasisEditor;

[Flags]
public enum PanelChangeProperties
{
    None = 0,
    Geometry = 1 << 0,
    Name = 1 << 1,
    Visibility = 1 << 2,
    LockState = 1 << 3,
    Ordering = 1 << 4,
    Style = 1 << 5,
    Metadata = 1 << 6,
    Structure = 1 << 7
}

public readonly record struct PanelChangeEvent(
    Guid DocumentId,
    string? ObjectId,
    PanelChangeProperties ChangedProperties,
    bool AffectsCanvas,
    bool AffectsHierarchy,
    bool AffectsInspectorRows,
    bool AffectsPersistence);
