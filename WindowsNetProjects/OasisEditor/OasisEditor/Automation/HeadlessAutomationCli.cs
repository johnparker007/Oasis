namespace OasisEditor.Automation;

internal enum HeadlessExitCode
{
    Success = 0,
    InvalidArguments = 1,
    InputFileNotFound = 2,
    ImportFailed = 3,
    SaveFailed = 4,
    ExportFailed = 5,
    UnexpectedError = 10
}

internal sealed record HeadlessCliParseResult(bool IsHeadless, ConvertMfmeAutomationOptions? Options, string? ErrorMessage, HeadlessExitCode ErrorCode)
{
    public static HeadlessCliParseResult NotHeadless() => new(false, null, null, HeadlessExitCode.Success);
    public static HeadlessCliParseResult Success(ConvertMfmeAutomationOptions options) => new(true, options, null, HeadlessExitCode.Success);
    public static HeadlessCliParseResult Failure(string errorMessage, HeadlessExitCode code) => new(true, null, errorMessage, code);
}

internal static class HeadlessAutomationCli
{
    public static HeadlessCliParseResult Parse(string[] args)
    {
        if (args.Length == 0 || !args.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            return HeadlessCliParseResult.NotHeadless();
        }

        if (!args.Contains("convert-mfme", StringComparer.OrdinalIgnoreCase))
        {
            return HeadlessCliParseResult.Failure("Missing command. Expected: convert-mfme", HeadlessExitCode.InvalidArguments);
        }

        static string? ReadValue(string[] source, string key)
        {
            for (var i = 0; i < source.Length - 1; i++)
            {
                if (string.Equals(source[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return source[i + 1];
                }
            }

            return null;
        }

        var input = ReadValue(args, "--input");
        var projectFile = ReadValue(args, "--project");
        var panel = ReadValue(args, "--panel");

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(projectFile) || string.IsNullOrWhiteSpace(panel))
        {
            return HeadlessCliParseResult.Failure("Missing required args: --input, --project, --panel", HeadlessExitCode.InvalidArguments);
        }

        var fullInput = Path.GetFullPath(input);
        if (!File.Exists(fullInput))
        {
            return HeadlessCliParseResult.Failure($"Input MFME extract not found: {fullInput}", HeadlessExitCode.InputFileNotFound);
        }

        var projectFullPath = Path.GetFullPath(projectFile);
        var projectDirectory = Path.GetDirectoryName(projectFullPath);
        var projectName = Path.GetFileNameWithoutExtension(projectFullPath);
        if (string.IsNullOrWhiteSpace(projectDirectory) || string.IsNullOrWhiteSpace(projectName))
        {
            return HeadlessCliParseResult.Failure("Invalid --project path.", HeadlessExitCode.InvalidArguments);
        }

        var panelRelativeOrName = panel.Trim();
        var outputPanelPath = Path.IsPathRooted(panelRelativeOrName)
            ? panelRelativeOrName
            : Path.Combine(projectDirectory, "Assets", panelRelativeOrName);

        var options = new ConvertMfmeAutomationOptions
        {
            ProjectName = projectName,
            ProjectRootLocation = projectDirectory,
            InputExtractPath = fullInput,
            PanelDocumentTitle = Path.GetFileNameWithoutExtension(panelRelativeOrName),
            OutputPanelPath = outputPanelPath
        };

        return HeadlessCliParseResult.Success(options);
    }

    public static async Task<HeadlessExitCode> RunAsync(ConvertMfmeAutomationOptions options)
    {
        var runner = new OasisAutomationCommandRunner();
        var command = new ConvertMfmeAutomationCommand(
            new ProjectContainerCreationService(),
            new Panel2DDocumentCreationService(),
            new MfmeExtractImportService(),
            new DocumentSaveService(),
            options);

        var context = new OasisAutomationCommandContext
        {
            WorkingDirectory = Environment.CurrentDirectory,
            Logger = new ConsoleAutomationLog()
        };

        var result = await runner.RunSequentialAsync([command], context).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return HeadlessExitCode.Success;
        }

        var message = result.Message ?? string.Empty;
        if (message.Contains("import", StringComparison.OrdinalIgnoreCase))
        {
            return HeadlessExitCode.ImportFailed;
        }

        if (message.Contains("save", StringComparison.OrdinalIgnoreCase))
        {
            return HeadlessExitCode.SaveFailed;
        }

        return HeadlessExitCode.UnexpectedError;
    }

    private sealed class ConsoleAutomationLog : IAutomationLog
    {
        public void Info(string message) => Console.WriteLine(message);
        public void Warning(string message) => Console.WriteLine($"WARN: {message}");
        public void Error(string message, Exception? exception = null)
            => Console.Error.WriteLine(exception is null ? $"ERROR: {message}" : $"ERROR: {message} {exception.Message}");
    }
}
