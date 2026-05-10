namespace OasisEditor;

public interface IMameSetupValidationService
{
    Task<MameSetupState> ValidateAsync(MameSetupValidationRequest request, CancellationToken cancellationToken);
}

public sealed record MameSetupValidationRequest(
    string ExecutablePath,
    string InstallRootDirectory,
    string PluginSourceDirectory,
    string SelectedVersion,
    string ReleaseSource);
