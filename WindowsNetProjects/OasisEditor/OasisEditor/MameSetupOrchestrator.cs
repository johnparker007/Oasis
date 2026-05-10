namespace OasisEditor;

public sealed class MameSetupOrchestrator : IMameSetupOrchestrator
{
    private readonly IMameSetupValidationService _validationService;

    public MameSetupOrchestrator(IMameSetupValidationService validationService)
    {
        _validationService = validationService;
    }

    public async Task<MameSetupState> ValidateAsync(MameSetupValidationRequest request, CancellationToken cancellationToken)
    {
        var result = await _validationService.ValidateAsync(request, cancellationToken);
        return new MameSetupState(
            result.Phase,
            result.Summary,
            string.IsNullOrWhiteSpace(result.LatestKnownVersion) ? "Unknown" : result.LatestKnownVersion,
            false);
    }
}
