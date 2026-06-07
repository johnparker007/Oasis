using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
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
    private readonly MachineRuntimeStateStore _runtimeStateStore;
    private readonly Automation.IPanel2DDocumentCreationService _panel2dCreationService;
    private readonly Automation.IFaceDocumentCreationService _faceCreationService;
    private readonly FaceGenerationService _faceGenerationService = new();
    private readonly FaceRegenerationService _faceRegenerationService = new();
    private readonly FaceValidationService _faceValidationService = new();

    private int _untitledDocumentCounter = 1;
    private int _panelDocumentCounter = 1;
    private int _cabinetDocumentCounter = 1;
    private int _machineDocumentCounter = 1;
    private int _faceDocumentCounter = 1;

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
        : this(
            getLoadedProject,
            setLoadedProject,
            openDocuments,
            getSelectedDocument,
            setSelectedDocument,
            notifyUndoRedoStateChanged,
            setStatusMessage,
            addOutputEntry,
            new MachineRuntimeStateStore(),
            new Automation.Panel2DDocumentCreationService(),
            onDocumentClosed,
            new Automation.FaceDocumentCreationService())
    {
    }

    public DocumentWorkspaceViewModel(
        Func<EditorProject?> getLoadedProject,
        Action<EditorProject?> setLoadedProject,
        ObservableCollection<DocumentTabViewModel> openDocuments,
        Func<DocumentTabViewModel?> getSelectedDocument,
        Action<DocumentTabViewModel?> setSelectedDocument,
        Action notifyUndoRedoStateChanged,
        Action<string> setStatusMessage,
        Action<string, OutputLogStatus> addOutputEntry,
        MachineRuntimeStateStore runtimeStateStore,
        Automation.IPanel2DDocumentCreationService panel2dCreationService,
        Action<Guid>? onDocumentClosed = null,
        Automation.IFaceDocumentCreationService? faceCreationService = null)
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
        _runtimeStateStore = runtimeStateStore;
        _panel2dCreationService = panel2dCreationService;
        _faceCreationService = faceCreationService ?? new Automation.FaceDocumentCreationService();
    }

    public bool CanOpenUntitledDocument() => _getLoadedProject() is not null;
    public bool CanOpenDocument() => _getLoadedProject() is not null;
    public bool CanCloseSelectedDocument() => _getSelectedDocument() is not null;

    public bool CanSaveSelectedDocument()
    {
        var selectedDocument = _getSelectedDocument();
        return selectedDocument is not null;
    }


    public bool CanGenerateFaceFromSelectedPanel2DRegion()
    {
        return _getLoadedProject() is not null
            && _getSelectedDocument() is { Document.DocumentType: EditorDocumentType.Panel2D };
    }

    public DocumentTabViewModel? GenerateFaceFromSelectedPanel2DRegion(Rect sourceRect)
    {
        var sourceDocument = _getSelectedDocument();
        var loadedProject = _getLoadedProject();
        if (loadedProject is null || sourceDocument is null || sourceDocument.Document.DocumentType != EditorDocumentType.Panel2D)
        {
            return null;
        }

        var sourceRegion = FaceSourceRegionModel.FromRect(sourceRect);
        if (!sourceRegion.IsValid)
        {
            return null;
        }

        var title = $"{sourceDocument.Title} Face";
        var result = _faceGenerationService.GenerateFromPanelRegion(
            sourceDocument.GetPanelDocument(),
            sourceRegion,
            title,
            sourceDocument.DocumentId.ToString("N"),
            loadedProject.InputDefinitions,
            loadedProject.ProjectDirectory,
            loadedProject.GeneratedDirectory);

        var faceJson = FaceDocumentStorage.Serialize(result.Document);
        var faceEditorDocument = EditorDocument.CreateFaceStub(title).WithContentSummary(result.Document.Summary ?? "Generated Face document.");
        var document = CreateDocumentTab(faceEditorDocument, faceDocumentJson: faceJson);
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Generated face document from Panel2D region with {result.ArtworkElementCount} artwork element(s), {result.ConvertedLampCount} lamp window(s), {result.ConvertedReelDisplayCount} reel display(s), {result.ConvertedSevenSegmentDisplayCount} seven-segment display(s), {result.ConvertedAlphaDisplayCount} alpha display(s), and {result.ConvertedButtonCount} button(s).");
        _addOutputEntry($"Generated face '{document.Title}' from Panel2D region with {result.ArtworkElementCount} artwork element(s), {result.ConvertedLampCount} lamp window(s), {result.ConvertedReelDisplayCount} reel display(s), {result.ConvertedSevenSegmentDisplayCount} seven-segment display(s), {result.ConvertedAlphaDisplayCount} alpha display(s), and {result.ConvertedButtonCount} button(s).", OutputLogStatus.Info);
        LogFaceMaskLayerStatus(result.Document, loadedProject);
        LogFaceDiagnostics(result.Document);
        return document;
    }

    public bool CanRegenerateSelectedFace()
    {
        var selectedDocument = _getSelectedDocument();
        if (_getLoadedProject() is null || selectedDocument is not { Document.DocumentType: EditorDocumentType.Face })
        {
            return false;
        }

        var faceDocument = selectedDocument.GetFaceDocument();
        return !string.IsNullOrWhiteSpace(faceDocument.SourcePanel2DDocumentId)
            && faceDocument.SourceRegion is { IsValid: true };
    }

    public bool CanOpenSourcePanel2DForSelectedFace()
    {
        var selectedDocument = _getSelectedDocument();
        if (_getLoadedProject() is null || selectedDocument is not { Document.DocumentType: EditorDocumentType.Face })
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(selectedDocument.GetFaceDocument().SourcePanel2DDocumentId);
    }

    public bool OpenSourcePanel2DForSelectedFace()
    {
        var selectedDocument = _getSelectedDocument();
        if (_getLoadedProject() is null || selectedDocument is not { Document.DocumentType: EditorDocumentType.Face })
        {
            return false;
        }

        var faceDocument = selectedDocument.GetFaceDocument();
        if (!TryFindSourcePanelDocument(faceDocument, out var sourcePanelDocument))
        {
            _setStatusMessage("Unable to open Face source: source Panel2D document is not open.");
            _addOutputEntry(BuildSourcePanelMissingMessage(faceDocument), OutputLogStatus.Warning);
            LogFaceDiagnostics(faceDocument);
            return false;
        }

        _setSelectedDocument(sourcePanelDocument);
        _setStatusMessage($"Opened source Panel2D for face '{selectedDocument.Title}': {sourcePanelDocument.Title}");
        _addOutputEntry($"Activated source Panel2D document '{sourcePanelDocument.Title}' for face '{selectedDocument.Title}'.", OutputLogStatus.Info);
        return true;
    }

    public bool RegenerateSelectedFace()
    {
        var selectedDocument = _getSelectedDocument();
        var loadedProject = _getLoadedProject();
        if (loadedProject is null || selectedDocument is null || selectedDocument.Document.DocumentType != EditorDocumentType.Face)
        {
            return false;
        }

        var existingFace = selectedDocument.GetFaceDocument();
        if (!TryFindSourcePanelDocument(existingFace, out var sourcePanelDocument))
        {
            _setStatusMessage("Unable to regenerate Face: source Panel2D document is not open.");
            _addOutputEntry(BuildSourcePanelMissingMessage(existingFace), OutputLogStatus.Warning);
            LogFaceDiagnostics(existingFace);
            return false;
        }

        var result = _faceRegenerationService.Regenerate(
            existingFace,
            sourcePanelDocument.GetPanelDocument(),
            loadedProject.InputDefinitions,
            loadedProject.ProjectDirectory,
            loadedProject.GeneratedDirectory);

        selectedDocument.SetFaceDocument(
            result.Document,
            new PanelChangeEvent(
                selectedDocument.DocumentId,
                null,
                PanelChangeProperties.Structure | PanelChangeProperties.Geometry | PanelChangeProperties.Metadata,
                AffectsCanvas: true,
                AffectsHierarchy: true,
                AffectsInspectorRows: true,
                AffectsPersistence: true));
        selectedDocument.MarkDirty();

        _setStatusMessage($"Regenerated face '{selectedDocument.Title}' from source Panel2D.");
        _addOutputEntry($"Regenerated face '{selectedDocument.Title}' from source Panel2D with {result.UpdatedElementCount} updated generated element(s), {result.AddedElementCount} added generated element(s), {result.RemovedGeneratedElementCount} removed stale generated element(s), and {result.PreservedManualElementCount} preserved manual element(s).", OutputLogStatus.Info);
        LogFaceMaskLayerStatus(result.Document, loadedProject);
        LogFaceDiagnostics(result.Document);
        return true;
    }

    public IReadOnlyList<FaceValidationDiagnostic> ValidateSelectedFace()
    {
        var selectedDocument = _getSelectedDocument();
        if (selectedDocument is not { Document.DocumentType: EditorDocumentType.Face })
        {
            return [];
        }

        return _faceValidationService.Validate(selectedDocument.GetFaceDocument(), _getLoadedProject(), _openDocuments.ToArray());
    }


    private void LogFaceMaskLayerStatus(FaceDocumentModel faceDocument, EditorProject project)
    {
        var maskLayer = faceDocument.MaskLayer;
        if (maskLayer is null)
        {
            _addOutputEntry($"Face '{faceDocument.Title}' has no generated mask layer metadata.", OutputLogStatus.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(maskLayer.AssetPath))
        {
            _addOutputEntry($"Face '{faceDocument.Title}' mask layer metadata was generated, but no mask asset path was written. Check source lamp artwork assets and project Generated folder settings.", OutputLogStatus.Warning);
            return;
        }

        var fullPath = ResolveProjectRelativePath(project.ProjectDirectory, maskLayer.AssetPath);
        if (File.Exists(fullPath))
        {
            _addOutputEntry($"Generated face mask layer asset for '{faceDocument.Title}': {fullPath}", OutputLogStatus.Info);
            return;
        }

        _addOutputEntry($"Face '{faceDocument.Title}' references mask layer asset '{maskLayer.AssetPath}', but the file was not found at '{fullPath}'.", OutputLogStatus.Warning);
    }

    private static string ResolveProjectRelativePath(string projectDirectory, string projectRelativePath)
    {
        if (Path.IsPathRooted(projectRelativePath))
        {
            return projectRelativePath;
        }

        return Path.GetFullPath(Path.Combine(
            projectDirectory,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
    }

    private void LogFaceDiagnostics(FaceDocumentModel faceDocument)
    {
        var diagnostics = _faceValidationService.Validate(faceDocument, _getLoadedProject(), _openDocuments.ToArray());
        foreach (var diagnostic in diagnostics)
        {
            _addOutputEntry($"Face validation ({diagnostic.Code}): {diagnostic.Message}", diagnostic.Severity == FaceValidationSeverity.Error ? OutputLogStatus.Error : OutputLogStatus.Warning);
        }
    }

    private static string BuildSourcePanelMissingMessage(FaceDocumentModel faceDocument)
    {
        var sourceId = string.IsNullOrWhiteSpace(faceDocument.SourcePanel2DDocumentId)
            ? "<missing>"
            : faceDocument.SourcePanel2DDocumentId.Trim();
        return $"Face source Panel2D document '{sourceId}' could not be located among open documents. Open the source Panel2D tab, then retry.";
    }

    private bool TryFindSourcePanelDocument(FaceDocumentModel faceDocument, out DocumentTabViewModel sourcePanelDocument)
    {
        sourcePanelDocument = null!;
        var sourceId = faceDocument.SourcePanel2DDocumentId?.Trim();
        if (string.IsNullOrWhiteSpace(sourceId) || faceDocument.SourceRegion is not { IsValid: true })
        {
            return false;
        }

        var match = _openDocuments.FirstOrDefault(document =>
            document.Document.DocumentType == EditorDocumentType.Panel2D
            && (string.Equals(document.DocumentId.ToString("N"), sourceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(document.DocumentId.ToString("D"), sourceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(document.FilePath, sourceId, StringComparison.OrdinalIgnoreCase)));
        if (match is null)
        {
            return false;
        }

        sourcePanelDocument = match;
        return true;
    }

    public void OpenUntitledDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = CreateDocumentTab(EditorDocument.CreateUntitled($"Untitled {_untitledDocumentCounter++}"));
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

        var document = _panel2dCreationService.CreatePanel2DStubDocument($"Panel {_panelDocumentCounter++}", _panelDocumentCounter - 1);

        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened panel document stub: {document.Title}");
        _addOutputEntry($"Opened panel document stub: {document.Title}", OutputLogStatus.Info);
    }

    public void OpenFaceStubDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = _faceCreationService.CreateFaceStubDocument($"Face {_faceDocumentCounter++}", _faceDocumentCounter - 1);
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        _setStatusMessage($"Opened face document stub: {document.Title}");
        _addOutputEntry($"Opened face document stub: {document.Title}", OutputLogStatus.Info);
    }

    public void OpenCabinet3DStubDocument()
    {
        if (_getLoadedProject() is null)
        {
            return;
        }

        var document = CreateDocumentTab(EditorDocument.CreateCabinet3DStub($"Cabinet {_cabinetDocumentCounter++}"));
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

        var document = CreateDocumentTab(EditorDocument.CreateMachineStub($"Machine {_machineDocumentCounter++}"));
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

    public bool OpenOrSelectDocument(string path, string summary, string? panelLayoutJson, string? panelTitle = null, string? faceDocumentJson = null)
    {
        var existing = _openDocuments.FirstOrDefault(tab => string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ExecuteDocumentMutation(new SelectDocumentTabMutationCommand(this, existing));
            return false;
        }

        var document = CreateDocumentTab(EditorDocument.CreateFromFile(path, summary, panelTitle), panelLayoutJson, faceDocumentJson);
        ExecuteDocumentMutation(new OpenDocumentTabMutationCommand(this, document));
        return true;
    }

    public void ReplaceDocument(DocumentTabViewModel original, DocumentTabViewModel updated)
    {
        ExecuteDocumentMutation(new ReplaceDocumentTabMutationCommand(this, original, updated));
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

        if (command is EditorCommands.IExecutionTrackedCommand executionTrackedCommand
            && !executionTrackedCommand.WasExecuted)
        {
            return false;
        }

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
            selectedDocument.CommandService,
            selectedDocument.RuntimeState,
            selectedDocument.FaceDocumentJson)
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

    private DocumentTabViewModel CreateDocumentTab(EditorDocument document, string? panelLayoutJson = null, string? faceDocumentJson = null)
    {
        var documentId = Guid.NewGuid();
        var runtimeState = _runtimeStateStore.GetOrCreate(documentId);
        runtimeState.FruitMachinePlatform = _getLoadedProject()?.FruitMachinePlatform ?? FruitMachinePlatformType.None;
        return new DocumentTabViewModel(
            document,
            panelLayoutJson,
            documentId,
            runtimeState: runtimeState,
            faceDocumentJson: faceDocumentJson);
    }

    internal static OpenDocumentData BuildOpenDocumentData(string path, string content)
    {
        if (string.Equals(Path.GetExtension(path), ".panel2d", StringComparison.OrdinalIgnoreCase))
        {
            if (Panel2DDocumentStorage.TryReadValidated(content, out var panelDocument, out var errorMessage))
            {
                var summary = string.IsNullOrWhiteSpace(panelDocument.Summary)
                    ? "Panel document opened."
                    : panelDocument.Summary.Trim();
                var title = string.IsNullOrWhiteSpace(panelDocument.Title)
                    ? Path.GetFileName(path)
                    : panelDocument.Title.Trim();
                return new OpenDocumentData(summary, Panel2DDocumentStorage.SerializeLayout(panelDocument.Elements), title);
            }

            return new OpenDocumentData(
                $"Failed to open panel document: {errorMessage}",
                null,
                Path.GetFileName(path));
        }


        if (string.Equals(Path.GetExtension(path), ".face", StringComparison.OrdinalIgnoreCase))
        {
            if (FaceDocumentStorage.TryReadValidated(content, out var faceDocument, out var errorMessage))
            {
                var summary = string.IsNullOrWhiteSpace(faceDocument.Summary)
                    ? "Face document opened."
                    : faceDocument.Summary.Trim();
                var title = string.IsNullOrWhiteSpace(faceDocument.Title)
                    ? Path.GetFileName(path)
                    : faceDocument.Title.Trim();
                return new OpenDocumentData(summary, null, title, FaceDocumentStorage.Serialize(faceDocument));
            }

            return new OpenDocumentData(
                $"Failed to open face document: {errorMessage}",
                null,
                Path.GetFileName(path));
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
            var elements = document.GetPanelElements()
                .Select(Panel2DDocumentStorage.ToStorageElement)
                .ToArray();
            return Panel2DDocumentStorage.Serialize(document.Document.Title, document.ContentSummary, elements);
        }


        if (document.Document.DocumentType == EditorDocumentType.Face)
        {
            var faceDocument = document.GetFaceDocument();
            var persistedFaceDocument = new FaceDocumentModel
            {
                Id = faceDocument.Id,
                Title = document.Document.Title,
                Summary = document.ContentSummary,
                SourcePanel2DDocumentId = faceDocument.SourcePanel2DDocumentId,
                SourceRegion = faceDocument.SourceRegion,
                LastRegeneratedAtUtc = faceDocument.LastRegeneratedAtUtc,
                MaskLayer = faceDocument.MaskLayer,
                Layers = faceDocument.Layers,
                Elements = faceDocument.Elements
            };
            return FaceDocumentStorage.Serialize(persistedFaceDocument);
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
