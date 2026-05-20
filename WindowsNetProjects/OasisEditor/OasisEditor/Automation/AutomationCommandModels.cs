namespace OasisEditor.Automation;

public interface IAutomationLog
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class OasisAutomationCommandContext
{
    public string WorkingDirectory { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }
    public IAutomationLog Logger { get; init; } = new NullAutomationLog();

    private sealed class NullAutomationLog : IAutomationLog
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}

public sealed record OasisAutomationCommandResult(bool Succeeded, string Message)
{
    public static OasisAutomationCommandResult Success(string message) => new(true, message);
    public static OasisAutomationCommandResult Failure(string message) => new(false, message);
}

public interface IOasisAutomationCommand
{
    string Name { get; }
    Task<OasisAutomationCommandResult> ExecuteAsync(OasisAutomationCommandContext context);
}

public sealed class OasisAutomationCommandRunner
{
    public async Task<OasisAutomationCommandResult> RunSequentialAsync(
        IEnumerable<IOasisAutomationCommand> commands,
        OasisAutomationCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var command in commands)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.Logger.Info($"Running automation command: {command.Name}");

            var result = await command.ExecuteAsync(context).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                context.Logger.Error($"Automation command failed: {command.Name}. {result.Message}");
                return result;
            }
        }

        return OasisAutomationCommandResult.Success("Automation pipeline completed.");
    }
}
