namespace OasisEditor;

public interface IMameSetupOrchestrator
{
    Task<MameSetupState> ValidateAsync(MameSetupValidationRequest request, CancellationToken cancellationToken);
}
