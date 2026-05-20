using OasisEditor.Automation;

namespace OasisEditor.Tests;

public sealed class OasisAutomationCommandRunnerTests
{
    [Fact]
    public async Task RunSequentialAsync_AllSuccess_ReturnsSuccess()
    {
        var runner = new OasisAutomationCommandRunner();
        var log = new TestAutomationLog();
        var context = new OasisAutomationCommandContext { Logger = log };
        var commands = new IOasisAutomationCommand[]
        {
            new DelegateAutomationCommand("one", _ => Task.FromResult(OasisAutomationCommandResult.Success("ok1"))),
            new DelegateAutomationCommand("two", _ => Task.FromResult(OasisAutomationCommandResult.Success("ok2")))
        };

        var result = await runner.RunSequentialAsync(commands, context);

        Assert.True(result.Succeeded);
        Assert.Contains(log.Infos, e => e.Contains("one", StringComparison.Ordinal));
        Assert.Contains(log.Infos, e => e.Contains("two", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunSequentialAsync_Failure_StopsPipeline()
    {
        var runner = new OasisAutomationCommandRunner();
        var context = new OasisAutomationCommandContext { Logger = new TestAutomationLog() };
        var executed = 0;

        var commands = new IOasisAutomationCommand[]
        {
            new DelegateAutomationCommand("one", _ =>
            {
                executed++;
                return Task.FromResult(OasisAutomationCommandResult.Failure("nope"));
            }),
            new DelegateAutomationCommand("two", _ =>
            {
                executed++;
                return Task.FromResult(OasisAutomationCommandResult.Success("ok"));
            })
        };

        var result = await runner.RunSequentialAsync(commands, context);

        Assert.False(result.Succeeded);
        Assert.Equal(1, executed);
    }

    private sealed class DelegateAutomationCommand(string name, Func<OasisAutomationCommandContext, Task<OasisAutomationCommandResult>> execute)
        : IOasisAutomationCommand
    {
        public string Name { get; } = name;
        public Task<OasisAutomationCommandResult> ExecuteAsync(OasisAutomationCommandContext context) => execute(context);
    }

    private sealed class TestAutomationLog : IAutomationLog
    {
        public List<string> Infos { get; } = [];
        public void Info(string message) => Infos.Add(message);
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
