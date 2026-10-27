namespace OasisEditor;

/// <summary>
/// Machine-owned composition supplied while exporting a Face as part of a Machine build.
/// A standalone Face export deliberately has no composition context.
/// </summary>
public sealed record FaceRuntimeCompositionContext(
    IReadOnlyList<MachineReelAssignment> MachineReelAssignments,
    IReadOnlyDictionary<MachineObjectReference, ReelDocument> ResolvedReels);
