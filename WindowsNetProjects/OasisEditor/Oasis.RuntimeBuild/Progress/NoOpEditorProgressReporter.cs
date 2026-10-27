namespace OasisEditor.Progress;

public sealed class NoOpEditorProgressReporter : IEditorProgressReporter
{
    public static NoOpEditorProgressReporter Instance { get; } = new();

    private NoOpEditorProgressReporter()
    {
    }

    public void Report(double normalizedProgress, string message)
    {
    }

    public void ReportIndeterminate(string message)
    {
    }

    public void ReportMessage(string message)
    {
    }

    public IEditorProgressReporter CreateChild(double start, double end, string? defaultMessagePrefix = null)
    {
        return this;
    }
}
