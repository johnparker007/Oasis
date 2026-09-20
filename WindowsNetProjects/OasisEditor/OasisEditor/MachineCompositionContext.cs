using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.MachineEditor.Models;

namespace OasisEditor;

public sealed record MachineCompositionContext(
    MachineDocument? MachineDocument,
    CabinetDocument? CabinetDocument,
    string? MachineAssetPath,
    string? CabinetAssetPath,
    string? DiagnosticCode,
    string? DiagnosticMessage)
{
    public bool HasCabinet => CabinetDocument is not null;
}
