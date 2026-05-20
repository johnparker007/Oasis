namespace OasisEditor.Automation;

internal interface IMameLayExportService
{
    OasisAutomationCommandResult Export(DocumentTabViewModel panelDocument, string outputLayPath);
}

internal sealed class PlaceholderMameLayExportService : IMameLayExportService
{
    public OasisAutomationCommandResult Export(DocumentTabViewModel panelDocument, string outputLayPath)
    {
        return OasisAutomationCommandResult.Failure("MAME .lay export is not implemented yet.");
    }
}
