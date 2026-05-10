using System.IO;

namespace OasisEditor;

public sealed class MameSetupValidationService : IMameSetupValidationService
{
    private readonly MamePluginAssetValidator _pluginAssetValidator;
    private readonly IMameVersionCatalogService _versionCatalogService;

    public MameSetupValidationService(MamePluginAssetValidator pluginAssetValidator, IMameVersionCatalogService versionCatalogService)
    {
        _pluginAssetValidator = pluginAssetValidator;
        _versionCatalogService = versionCatalogService;
    }

    public async Task<MameSetupState> ValidateAsync(MameSetupValidationRequest request, CancellationToken cancellationToken)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ExecutablePath) || !File.Exists(request.ExecutablePath))
        {
            issues.Add("MAME executable is missing.");
        }

        if (!string.IsNullOrWhiteSpace(request.SelectedVersion) && !string.IsNullOrWhiteSpace(request.InstallRootDirectory))
        {
            var normalizedSelectedVersion = MameVersionParsing.NormalizeVersion(request.SelectedVersion);
            var expectedInstallDirectory = Path.Combine(request.InstallRootDirectory, $"mame{normalizedSelectedVersion}");
            var expectedExecutableCandidates = new[]
            {
                Path.Combine(expectedInstallDirectory, "mame.exe"),
                Path.Combine(expectedInstallDirectory, $"mame{normalizedSelectedVersion}", "mame.exe")
            };

            if (!expectedExecutableCandidates.Any(File.Exists))
            {
                issues.Add($"Selected MAME version {normalizedSelectedVersion} is not installed.");
            }
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
            var latest = await _versionCatalogService.GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
            latestVersion = latest.LatestVersion;
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
                false,
                issues);
        }

        return new MameSetupState(
            MameSetupPhase.Ready,
            "MAME setup is valid.",
            latestVersion,
            false,
            []);
    }
}
