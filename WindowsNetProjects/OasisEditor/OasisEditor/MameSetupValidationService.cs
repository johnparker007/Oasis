using System.IO;

namespace OasisEditor;

public sealed class MameSetupValidationService : IMameSetupValidationService
{
    private readonly MamePluginAssetValidator _pluginAssetValidator;
    private readonly MameDownloadService _downloadService;

    public MameSetupValidationService(MamePluginAssetValidator pluginAssetValidator, MameDownloadService downloadService)
    {
        _pluginAssetValidator = pluginAssetValidator;
        _downloadService = downloadService;
    }

    public async Task<MameSetupState> ValidateAsync(MameSetupValidationRequest request, CancellationToken cancellationToken)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ExecutablePath) || !File.Exists(request.ExecutablePath))
        {
            issues.Add("MAME executable is missing.");
        }

        if (string.IsNullOrWhiteSpace(request.InstallRootDirectory))
        {
            issues.Add("Managed runtime root is unavailable.");
        }
        else
        {
            Directory.CreateDirectory(request.InstallRootDirectory);
        }

        if (string.IsNullOrWhiteSpace(request.PluginSourceDirectory) || !Directory.Exists(request.PluginSourceDirectory))
        {
            issues.Add("Managed Lua plugin source is unavailable.");
        }
        else
        {
            var missingFiles = _pluginAssetValidator.GetMissingFiles(request.PluginSourceDirectory);
            if (missingFiles.Count > 0)
            {
                issues.Add($"Managed Lua plugin source is missing files: {string.Join(", ", missingFiles)}.");
            }
        }

        string? latestVersion = null;
        try
        {
            var versions = await _downloadService.GetKnownVersionsAsync(cancellationToken).ConfigureAwait(false);
            latestVersion = versions.OrderByDescending(v => v).FirstOrDefault();
        }
        catch
        {
            // Background discovery should not fail the entire validation state.
        }

        if (issues.Count > 0)
        {
            return new MameSetupState(
                MameSetupPhase.NeedsAttention,
                string.Join(" ", issues),
                latestVersion,
                DateTimeOffset.UtcNow);
        }

        return new MameSetupState(
            MameSetupPhase.Ready,
            "MAME setup is valid.",
            latestVersion,
            DateTimeOffset.UtcNow);
    }
}
