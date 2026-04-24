using System.ComponentModel;
using OasisEditor.Commands;

namespace OasisEditor;

public sealed class DocumentTabViewModel : INotifyPropertyChanged
{
    private readonly CommandService _commandService;
    private string? _panelLayoutJson;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DocumentTabViewModel(
        EditorDocument document,
        string? panelLayoutJson = null,
        Guid? documentId = null,
        CommandService? commandService = null)
    {
        Document = document;
        DocumentId = documentId ?? Guid.NewGuid();
        _commandService = commandService ?? new CommandService(new CommandHistory(), DocumentId);
        _panelLayoutJson = panelLayoutJson;
    }

    public EditorDocument Document { get; }
    public Guid DocumentId { get; }
    public CommandService CommandService => _commandService;
    public string Title => Document.IsDirty ? $"{Document.Title}*" : Document.Title;
    public string TypeLabel => Document.DocumentType switch
    {
        EditorDocumentType.ProjectOverview => "Project",
        EditorDocumentType.Panel2D => "Panel 2D",
        EditorDocumentType.Cabinet3D => "Cabinet 3D",
        EditorDocumentType.Machine => "Machine",
        _ => "Document Type"
    };
    public string FilePath => Document.FilePath;
    public string ContentSummary => Document.ContentSummary;
    public bool IsDirty => Document.IsDirty;

    public string? PanelLayoutJson
    {
        get => _panelLayoutJson;
        set
        {
            if (string.Equals(_panelLayoutJson, value, StringComparison.Ordinal))
            {
                return;
            }

            _panelLayoutJson = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelLayoutJson)));
        }
    }
}
