namespace OasisEditor;

public sealed class OasisPlayerPreviewService
{
    private readonly IMachineRuntimeBuildService _buildService;
    private readonly OasisPlayerLaunchService _launchService;

    public OasisPlayerPreviewService(IMachineRuntimeBuildService? buildService = null, OasisPlayerLaunchService? launchService = null)
    {
        _buildService = buildService ?? new MachineRuntimeBuildService();
        _launchService = launchService ?? new OasisPlayerLaunchService();
    }

    public OasisPlayerPreviewResult Preview(EditorProject project, string cabinetManifestPath, OasisPlayerPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(preferences);

        var validationRequest = new OasisPlayerLaunchRequest(preferences.ExecutablePath, project.GeneratedDirectory, preferences.Fullscreen, preferences.PreviewWidth, preferences.PreviewHeight);
        var validationError = OasisPlayerLaunchService.Validate(validationRequest);
        if (validationError is not null)
        {
            return OasisPlayerPreviewResult.Fail(validationError);
        }

        var buildResult = _buildService.BuildFromCabinetDocument(project, cabinetManifestPath);
        if (!buildResult.Success || string.IsNullOrWhiteSpace(buildResult.BuildRoot))
        {
            return OasisPlayerPreviewResult.Fail(buildResult.ErrorMessage ?? "Failed to build Oasis Player runtime output.");
        }

        var launchResult = _launchService.Launch(new OasisPlayerLaunchRequest(preferences.ExecutablePath, buildResult.BuildRoot, preferences.Fullscreen, preferences.PreviewWidth, preferences.PreviewHeight));
        if (!launchResult.Success)
        {
            return OasisPlayerPreviewResult.Fail(launchResult.ErrorMessage ?? "Failed to launch Oasis Player.", buildResult.BuildRoot);
        }

        return OasisPlayerPreviewResult.Ok(buildResult.BuildRoot, launchResult.ExecutablePath!, launchResult.Arguments);
    }
}

public sealed record OasisPlayerPreviewResult(bool Success, string? BuildRoot, string? ExecutablePath, IReadOnlyList<string> Arguments, string? ErrorMessage)
{
    public static OasisPlayerPreviewResult Ok(string buildRoot, string executablePath, IReadOnlyList<string> arguments) => new(true, buildRoot, executablePath, arguments, null);
    public static OasisPlayerPreviewResult Fail(string errorMessage, string? buildRoot = null) => new(false, buildRoot, null, Array.Empty<string>(), errorMessage);
}
