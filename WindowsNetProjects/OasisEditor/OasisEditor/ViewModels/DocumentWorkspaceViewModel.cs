using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using EditorCommands = OasisEditor.Commands;

namespace OasisEditor;

public sealed class DocumentWorkspaceViewModel
{
    private readonly EditorCommands.CommandService _shellCommandService = new();
    private readonly Func<EditorProject?> _getLoadedProject;
    private readonly Action<EditorProject?> _setLoadedProject;
    private readonly ObservableCollection<DocumentTabViewModel> _openDocuments;
    private readonly Func<DocumentTabViewModel?> _getSelectedDocument;
    private readonly Action<DocumentTabViewModel?> _setSelectedDocument;
    private readonly Action _notifyUndoRedoStateChanged;
    private readonly Action<string> _setStatusMessage;
    private readonly Action<string, OutputLogStatus> _addOutputEntry;
    private readonly Action<Guid> _onDocumentClosed;

    private int _untitledDocumentCounter = 1;
    private int _panelDocumentCounter = 1;
    private int _cabinetDocumentCounter = 1;
    private int _machineDocumentCounter = 1;

    public DocumentWorkspaceViewModel(
        Func<EditorProject?> getLoadedProject,
        Action<EditorProject?> setLoadedProject,
        ObservableCollection<DocumentTabViewModel> openDocuments,
        Func<DocumentTabViewModel?> getSelectedDocument,
        Action<DocumentTabViewModel?> setSelectedDocument,
        Action notifyUndoRedoStateChanged,
        Action<string> setStatusMessage,
        Action<string, OutputLogStatus> addOutputEntry,
        Action<Guid>? onDocumentClosed = null)
    {
        _getLoadedProject = getLoadedProject;
        _setLoadedProject = setLoadedProject;
        _openDocuments = openDocuments;
        _getSelectedDocument = getSelectedDocument;
        _setSelectedDocument = setSelectedDocument;
        _notifyUndoRedoStateChanged = notifyUndoRedoStateChanged;
        _setStatusMessage = setStatusMessage;
        _addOutputEntry = addOutputEntry;
        _onDocumentClosed = onDocumentClosed ?? (_ => { });
    }

    public bool CanOpenUntitledDocument() => _getLoadedProject() is not null;
    public bool CanOpenDocument() => _getLoadedProject() is not null;
    public bool CanCloseSelectedDocument() => _getSelectedDocument() is not null;

    public bool CanSaveSelectedDocument()
    {
        var selectedDocument = _getSelectedDocument();
        return selectedDocument is not null && selectedDocument.Document.DocumentType != EditorDocumentType.ProjectOverview;
    }

    public void OpenUntitledDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(EditorDocument.CreateUntitled($"Untitled {_untitledDocumentCounter++}"));
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened document tab: {document.Title}");
        _addOutputEntry($"Opened document tab: {document.Title}", OutputLogStatus.Info);
    }

    public void OpenPanel2DStubDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(
            EditorDocument.CreatePanel2DStub($"Panel {_panelDocumentCounter++}"),
            panelLayoutJson: Panel2DDocumentStorage.SerializeLayout([]));

        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened panel document stub: {document.Title}");
        _addOutputEntry($"Opened panel document stub: {document.Title}", OutputLogStatus.Info);
    }

    public void OpenCabinet3DStubDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(EditorDocument.CreateCabinet3DStub($"Cabinet {_cabinetDocumentCounter++}"));
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened cabinet document stub: {document.Title}");
        _addOutputEntry($"Opened cabinet document stub: {document.Title}", OutputLogStatus.Info);
    }

    public void OpenMachineStubDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = new DocumentTabViewModel(EditorDocument.CreateMachineStub($"Machine {_machineDocumentCounter++}"));
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened machine document stub: {document.Title}");
        _addOutputEntry($"Opened machine document stub: {document.Title}", OutputLogStatus.Info);
    }

    public void CloseSelectedDocument()
    {
        var selectedDocument = _getSelectedDocument();
        if (selectedDocument is null)
        {
            return;
        }

        ExecuteDocumentMutation(new CloseDocumentTabMutationCommand(this, selectedDocument));
        _setStatusMessage($"Closed document tab: {selectedDocument.Title}");
        _addOutputEntry($"Closed document tab: {selectedDocument.Title}", OutputLogStatus.Info);
    }

    public bool OpenOrSelectDocument(string path, string summary, string? panelLayoutJson)
    {
        var existing = _openDocuments.FirstOrDefault(tab => string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ExecuteDocumentMutation(new SelectDocumentTabMutationCommand(this, existing));
            return false;
        }

        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile(path, summary), panelLayoutJson);
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        return true;
    }

    public void ReplaceDocument(DocumentTabViewModel original, DocumentTabViewModel updated)
    {
        ExecuteDocumentMutation(new ReplaceDocumentTabMutationCommand(this, original, updated));
    }

    public void EnsureProjectOverviewDocument()
    {
        var loadedProject = _getLoadedProject();
        if (loadedProject is null)
        {
            return;
        }

        var overviewDocument = new DocumentTabViewModel(EditorDocument.CreateProjectOverview(loadedProject));
        ExecuteDocumentMutation(new ReplaceOpenDocumentsMutationCommand(this, [overviewDocument], overviewDocument));
    }

    public void ClearProjectSessionState()
    {
        _shellCommandService.History.Clear();
        _openDocuments.Clear();
        _setSelectedDocument(null);
        _setLoadedProject(null);
        _setStatusMessage("Project closed. Returned to Launcher.");
    }

    public bool CanUndoActiveDocument() => _getSelectedDocument()?.CommandService.CanUndo ?? false;
    public bool CanRedoActiveDocument() => _getSelectedDocument()?.CommandService.CanRedo ?? false;

    public bool UndoActiveDocument()
    {
        var activeDocument = _getSelectedDocument();
        if (activeDocument is null)
        {
            return false;
        }

        var undone = activeDocument.CommandService.TryUndo();
        if (undone)
        {
            _notifyUndoRedoStateChanged();
        }

        return undone;
    }

    public bool RedoActiveDocument()
    {
        var activeDocument = _getSelectedDocument();
        if (activeDocument is null)
        {
            return false;
        }

        var redone = activeDocument.CommandService.TryRedo();
        if (redone)
        {
            _notifyUndoRedoStateChanged();
        }

        return redone;
    }

    public bool ExecuteDocumentCanvasCommand(Guid documentId, EditorCommands.ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var activeDocument = _getSelectedDocument();
        if (activeDocument is null || activeDocument.DocumentId != documentId)
        {
            return false;
        }

        activeDocument.CommandService.Execute(command);
        _notifyUndoRedoStateChanged();
        return true;
    }

    public DocumentTabViewModel? ApplyInspectorSummary(string summary)
    {
        var selectedDocument = _getSelectedDocument();
        if (selectedDocument is null)
        {
            return null;
        }

        var updated = new DocumentTabViewModel(
            selectedDocument.Document.WithContentSummary(summary).MarkDirty(),
            selectedDocument.PanelLayoutJson,
            selectedDocument.DocumentId,
            selectedDocument.CommandService)
        {
            PanelZoom = selectedDocument.PanelZoom,
            PanelPanX = selectedDocument.PanelPanX,
            PanelPanY = selectedDocument.PanelPanY
        };

        ExecuteDocumentMutation(new ReplaceDocumentTabMutationCommand(this, selectedDocument, updated));
        _setStatusMessage($"Updated inspector summary for {updated.Title}");
        _addOutputEntry($"Inspector summary updated for {updated.Title}", OutputLogStatus.Info);
        return updated;
    }

    internal static OpenDocumentData BuildOpenDocumentData(string path, string content)
    {
        if (string.Equals(Path.GetExtension(path), ".panel2d", StringComparison.OrdinalIgnoreCase)
            && Panel2DDocumentStorage.TryRead(content, out var panelDocument))
        {
            var summary = string.IsNullOrWhiteSpace(panelDocument.Summary)
                ? "Panel document opened."
                : panelDocument.Summary.Trim();
            return new OpenDocumentData(summary, Panel2DDocumentStorage.SerializeLayout(panelDocument.Elements));
        }

        var preview = content.Length > 300 ? $"{content[..300]}..." : content;
        if (string.IsNullOrWhiteSpace(preview))
        {
            preview = "Document opened (file is empty).";
        }

        return new OpenDocumentData(preview, null);
    }

    public static string BuildDocumentContent(DocumentTabViewModel document)
    {
        if (document.Document.DocumentType == EditorDocumentType.Panel2D)
        {
            var elements = Panel2DDocumentStorage.ToStorageElements(document.GetPanelElements());
            return Panel2DDocumentStorage.Serialize(document.Document.Title, document.ContentSummary, elements);
        }

        var persisted = new
        {
            title = document.Title,
            type = document.Document.DocumentType.ToString(),
            summary = document.ContentSummary,
            savedAtUtc = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true });
    }

    private void ExecuteDocumentMutation(EditorCommands.ICommand command)
    {
        _shellCommandService.Execute(command);
    }

    private sealed class OpenDocumentTabMutationCommand : EditorCommands.ICommand
    {
        private readonly DocumentWorkspaceViewModel _owner;
        private readonly DocumentTabViewModel _document;
        private DocumentTabViewModel? _previousSelection;

        public OpenDocumentTabMutationCommand(DocumentWorkspaceViewModel owner, DocumentTabViewModel document)
        {
            _owner = owner;
            _document = document;
        }

        public string Description => $"Open document tab {_document.Title}";

        public void Execute()
        {
            _previousSelection = _owner._getSelectedDocument();
            _owner._openDocuments.Add(_document);
            _owner._setSelectedDocument(_document);
        }

        public void Undo()
        {
            _owner._openDocuments.Remove(_document);
            _owner._setSelectedDocument(_previousSelection);
        }
    }

    private sealed class SelectDocumentTabMutationCommand : EditorCommands.ICommand
    {
        private readonly DocumentWorkspaceViewModel _owner;
        private readonly DocumentTabViewModel _target;
        private DocumentTabViewModel? _previousSelection;

        public SelectDocumentTabMutationCommand(DocumentWorkspaceViewModel owner, DocumentTabViewModel target)
        {
            _owner = owner;
            _target = target;
        }

        public string Description => $"Select document tab {_target.Title}";

        public void Execute()
        {
            _previousSelection = _owner._getSelectedDocument();
            _owner._setSelectedDocument(_target);
        }

        public void Undo()
        {
            _owner._setSelectedDocument(_previousSelection);
        }
    }

    private sealed class ReplaceDocumentTabMutationCommand : EditorCommands.ICommand
    {
        private readonly DocumentWorkspaceViewModel _owner;
        private readonly DocumentTabViewModel _original;
        private readonly DocumentTabViewModel _updated;
        private int _index = -1;
        private DocumentTabViewModel? _previousSelection;

        public ReplaceDocumentTabMutationCommand(DocumentWorkspaceViewModel owner, DocumentTabViewModel original, DocumentTabViewModel updated)
        {
            _owner = owner;
            _original = original;
            _updated = updated;
        }

        public string Description => $"Replace document tab {_original.Title}";

        public void Execute()
        {
            _index = _owner._openDocuments.IndexOf(_original);
            _previousSelection = _owner._getSelectedDocument();
            if (_index >= 0)
            {
                _owner._openDocuments[_index] = _updated;
            }

            _owner._setSelectedDocument(_updated);
        }

        public void Undo()
        {
            if (_index >= 0)
            {
                _owner._openDocuments[_index] = _original;
            }

            _owner._setSelectedDocument(_previousSelection);
        }
    }

    private sealed class CloseDocumentTabMutationCommand : EditorCommands.ICommand, EditorCommands.IExecutionTrackedCommand
    {
        private readonly DocumentWorkspaceViewModel _owner;
        private readonly DocumentTabViewModel _document;
        private int _index = -1;
        private DocumentTabViewModel? _nextSelection;

        public bool WasExecuted { get; private set; }

        public CloseDocumentTabMutationCommand(DocumentWorkspaceViewModel owner, DocumentTabViewModel document)
        {
            _owner = owner;
            _document = document;
        }

        public string Description => $"Close document tab {_document.Title}";

        public void Execute()
        {
            WasExecuted = false;
            _index = _owner._openDocuments.IndexOf(_document);
            _owner._openDocuments.Remove(_document);
            _document.CommandService.History.Clear();
            _owner._onDocumentClosed(_document.DocumentId);

            _nextSelection = _owner._openDocuments.Count == 0
                ? null
                : _owner._openDocuments[Math.Clamp(_index, 0, _owner._openDocuments.Count - 1)];
            _owner._setSelectedDocument(_nextSelection);

            // Closing tabs should not be undoable while per-document history is discarded.
            WasExecuted = false;
        }

        public void Undo()
        {
            if (_index < 0 || _index > _owner._openDocuments.Count)
            {
                _owner._openDocuments.Add(_document);
                _owner._setSelectedDocument(_document);
                return;
            }

            _owner._openDocuments.Insert(_index, _document);
            _owner._setSelectedDocument(_document);
        }
    }

    private sealed class ReplaceOpenDocumentsMutationCommand : EditorCommands.ICommand
    {
        private readonly DocumentWorkspaceViewModel _owner;
        private readonly IReadOnlyList<DocumentTabViewModel> _nextDocuments;
        private readonly DocumentTabViewModel? _nextSelection;
        private List<DocumentTabViewModel>? _previousDocuments;
        private DocumentTabViewModel? _previousSelection;

        public ReplaceOpenDocumentsMutationCommand(
            DocumentWorkspaceViewModel owner,
            IReadOnlyList<DocumentTabViewModel> nextDocuments,
            DocumentTabViewModel? nextSelection)
        {
            _owner = owner;
            _nextDocuments = nextDocuments;
            _nextSelection = nextSelection;
        }

        public string Description => "Replace open documents";

        public void Execute()
        {
            _previousDocuments = _owner._openDocuments.ToList();
            _previousSelection = _owner._getSelectedDocument();

            _owner._openDocuments.Clear();
            foreach (var document in _nextDocuments)
            {
                _owner._openDocuments.Add(document);
            }

            _owner._setSelectedDocument(_nextSelection);
        }

        public void Undo()
        {
            _owner._openDocuments.Clear();
            if (_previousDocuments is not null)
            {
                foreach (var document in _previousDocuments)
                {
                    _owner._openDocuments.Add(document);
                }
            }

            _owner._setSelectedDocument(_previousSelection);
        }
    }
}
