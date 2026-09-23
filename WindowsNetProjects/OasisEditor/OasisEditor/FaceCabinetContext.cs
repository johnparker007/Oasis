using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor;

public sealed record FaceCabinetContext(
    CabinetDocument? CabinetDocument,
    string? CabinetAssetPath,
    IReadOnlyList<MachineReelAssignment>? MachineReelAssignments = null,
    IReadOnlyDictionary<MachineObjectReference, ReelDocument>? ResolvedReels = null);
