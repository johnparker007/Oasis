namespace OasisEditor.Progress;

public interface IEditorProgressReporter
{
    void Report(double normalizedProgress, string message);
    void ReportIndeterminate(string message);
    void ReportMessage(string message);
    IEditorProgressReporter CreateChild(double start, double end, string? defaultMessagePrefix = null);
}
