using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetFaceTargetViewModel
{
    public CabinetFaceTargetViewModel(CabinetFaceTarget target)
    {
        Id = target.Id;
        SourceName = target.SourceName;
        DisplayName = target.DisplayName;
        IsValid = target.IsValid;
        ErrorMessage = target.ErrorMessage;
    }

    public string Id { get; }
    public string SourceName { get; }
    public string DisplayName { get; }
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public string StateText => IsValid ? "Valid quad" : $"Invalid: {ErrorMessage}";
}
