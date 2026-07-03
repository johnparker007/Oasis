using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OasisEditor.Features.CabinetEditor.ViewModels;
using EditorCommands = OasisEditor.Commands;

namespace OasisEditor;

public sealed class InspectorViewModel : INotifyPropertyChanged
{
    private readonly Func<AssetBrowserItemViewModel?> _selectedAssetAccessor;
    private readonly Func<AssetDirectoryNodeViewModel?> _selectedAssetDirectoryAccessor;
    private readonly Func<DocumentTabViewModel?> _selectedDocumentAccessor;
    private readonly Func<IEnumerable<DocumentTabViewModel>> _openDocumentsAccessor;
    private readonly Func<EditorProject?> _loadedProjectAccessor;
    private readonly ActiveDocumentContextService _activeDocumentContext;
    private readonly Func<Guid, EditorCommands.ICommand, bool> _executeCanvasCommand;
    private readonly Func<DocumentTabViewModel, string, DocumentTabViewModel?> _applySummary;
    private readonly ICommand? _generateFaceFromSourceShapeCommand;
    private readonly ObservableCollection<InspectorPropertyRowViewModel> _propertyRows = [];
    private string _inspectorEditableSummary = string.Empty;
    private DateTime _suppressPropertyRowRefreshUntilUtc;
    private string? _lastInspectorSelectionObjectId;
    private PanelElementKind? _lastInspectorSelectionKind;
    private string? _lastInspectorFaceSelectionKind;
    private bool _hadInspectorSelection;

    public InspectorViewModel(
        Func<AssetBrowserItemViewModel?> selectedAssetAccessor,
        Func<AssetDirectoryNodeViewModel?> selectedAssetDirectoryAccessor,
        Func<DocumentTabViewModel?> selectedDocumentAccessor,
        Func<EditorProject?> loadedProjectAccessor,
        ActiveDocumentContextService activeDocumentContext,
        Func<Guid, EditorCommands.ICommand, bool> executeCanvasCommand,
        Func<DocumentTabViewModel, string, DocumentTabViewModel?> applySummary,
        ICommand? generateFaceFromSourceShapeCommand = null)
        : this(
            selectedAssetAccessor,
            selectedAssetDirectoryAccessor,
            selectedDocumentAccessor,
            () => [],
            loadedProjectAccessor,
            activeDocumentContext,
            executeCanvasCommand,
            applySummary,
            generateFaceFromSourceShapeCommand)
    {
    }

    public InspectorViewModel(
        Func<AssetBrowserItemViewModel?> selectedAssetAccessor,
        Func<AssetDirectoryNodeViewModel?> selectedAssetDirectoryAccessor,
        Func<DocumentTabViewModel?> selectedDocumentAccessor,
        Func<IEnumerable<DocumentTabViewModel>> openDocumentsAccessor,
        Func<EditorProject?> loadedProjectAccessor,
        ActiveDocumentContextService activeDocumentContext,
        Func<Guid, EditorCommands.ICommand, bool> executeCanvasCommand,
        Func<DocumentTabViewModel, string, DocumentTabViewModel?> applySummary,
        ICommand? generateFaceFromSourceShapeCommand = null)
    {
        _selectedAssetAccessor = selectedAssetAccessor;
        _selectedAssetDirectoryAccessor = selectedAssetDirectoryAccessor;
        _selectedDocumentAccessor = selectedDocumentAccessor;
        _openDocumentsAccessor = openDocumentsAccessor;
        _loadedProjectAccessor = loadedProjectAccessor;
        _activeDocumentContext = activeDocumentContext;
        _executeCanvasCommand = executeCanvasCommand;
        _applySummary = applySummary;
        _generateFaceFromSourceShapeCommand = generateFaceFromSourceShapeCommand;
        ApplyInspectorSummaryCommand = new RelayCommand(ApplyInspectorSummary, CanApplyInspectorSummary);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ApplyInspectorSummaryCommand { get; }

    public IReadOnlyList<InspectorPropertyRowViewModel> InspectorPropertyRows => _propertyRows;

    public string InspectorTitle
    {
        get
        {
            var selectedAsset = _selectedAssetAccessor();
            if (selectedAsset is not null)
            {
                var loadedProjectForAsset = _loadedProjectAccessor();
                return loadedProjectForAsset is not null
                    ? AssetInspectorDetailsBuilder.GetTitle(loadedProjectForAsset, selectedAsset.FullPath, selectedAsset.IsDirectory)
                    : $"Asset: {selectedAsset.DisplayPath}";
            }

            var selectedAssetDirectory = _selectedAssetDirectoryAccessor();
            if (selectedAssetDirectory is not null)
            {
                var loadedProjectForDirectory = _loadedProjectAccessor();
                return loadedProjectForDirectory is not null
                    ? AssetInspectorDetailsBuilder.GetTitle(loadedProjectForDirectory, selectedAssetDirectory.FullPath, isDirectory: true)
                    : $"Folder: {selectedAssetDirectory.DisplayPath}";
            }

            var selectedDocument = _selectedDocumentAccessor();
            if (selectedDocument is not null
                && _activeDocumentContext.ActivePanelSelection is PanelSelectionInfo panelSelection)
            {
                if (selectedDocument.Document.DocumentType == EditorDocumentType.Face
                    && selectedDocument.TryGetFaceElement(panelSelection, out var selectedFaceElement))
                {
                    return NicifyFaceElementKind(selectedFaceElement);
                }

                if (selectedDocument.TryGetPanelElement(panelSelection, out var selectedElement))
                {
                    return NicifyElementKind(selectedElement.Kind);
                }
            }

            if (selectedDocument is not null)
            {
                return $"Document: {selectedDocument.Title}";
            }

            var loadedProject = _loadedProjectAccessor();
            if (loadedProject is not null)
            {
                return $"Project: {loadedProject.Name}";
            }

            return "No selection";
        }
    }

    public string InspectorType
    {
        get
        {
            var selectedAsset = _selectedAssetAccessor();
            if (selectedAsset is not null)
            {
                return AssetInspectorDetailsBuilder.GetType(selectedAsset.FullPath, selectedAsset.IsDirectory);
            }

            var selectedAssetDirectory = _selectedAssetDirectoryAccessor();
            if (selectedAssetDirectory is not null)
            {
                return AssetInspectorDetailsBuilder.GetType(selectedAssetDirectory.FullPath, isDirectory: true);
            }

            var selectedDocument = _selectedDocumentAccessor();
            if (selectedDocument is not null)
            {
                return selectedDocument.TypeLabel;
            }

            var loadedProject = _loadedProjectAccessor();
            if (loadedProject is not null)
            {
                return "Editor Project";
            }

            return "None";
        }
    }

    public string InspectorPath
    {
        get
        {
            var selectedAsset = _selectedAssetAccessor();
            if (selectedAsset is not null)
            {
                return selectedAsset.FullPath;
            }

            var selectedAssetDirectory = _selectedAssetDirectoryAccessor();
            if (selectedAssetDirectory is not null)
            {
                return selectedAssetDirectory.FullPath;
            }

            var selectedDocument = _selectedDocumentAccessor();
            if (selectedDocument is not null)
            {
                return selectedDocument.FilePath;
            }

            var loadedProject = _loadedProjectAccessor();
            if (loadedProject is not null)
            {
                return loadedProject.ProjectFilePath;
            }

            return "Select an asset or document to inspect details.";
        }
    }

    public string InspectorSummary
    {
        get
        {
            var selectedAssetForSummary = _selectedAssetAccessor();
            if (selectedAssetForSummary is not null)
            {
                return selectedAssetForSummary.IsDirectory
                    ? "Selected asset folder details are shown below."
                    : "Selected asset file details are shown below.";
            }

            var selectedAssetDirectoryForSummary = _selectedAssetDirectoryAccessor();
            if (selectedAssetDirectoryForSummary is not null)
            {
                return "Selected asset folder details are shown below.";
            }

            var selectedDocument = _selectedDocumentAccessor();
            if (selectedDocument is not null)
            {
                if (_activeDocumentContext.ActivePanelSelection is PanelSelectionInfo panelSelection)
                {
                    if (selectedDocument.Document.DocumentType == EditorDocumentType.Face)
                    {
                        if (selectedDocument.GetFaceDocument().MaskLayer is FaceMaskLayerModel selectedMaskLayer
                            && FaceMaskLayerSelectionService.IsMaskLayerSelection(panelSelection))
                        {
                            return BuildSelectedFaceMaskLayerSummary(selectedMaskLayer);
                        }

                        if (selectedDocument.TryGetFaceElement(panelSelection, out var selectedFaceElement))
                        {
                            return BuildSelectedFaceElementSummary(selectedFaceElement);
                        }
                    }

                    if (selectedDocument.TryGetPanelElement(panelSelection, out var selectedElement))
                    {
                        return BuildSelectedElementSummary(selectedElement);
                    }

                    return $"Selected {panelSelection.Kind} at ({panelSelection.X:0.##}, {panelSelection.Y:0.##}) sized {panelSelection.Width:0.##} x {panelSelection.Height:0.##}.";
                }

                return selectedDocument.ContentSummary;
            }


            var loadedProject = _loadedProjectAccessor();
            if (loadedProject is not null)
            {
                return "Project loaded. Select a document tab or asset file to inspect it.";
            }

            return "Open or create a project to enable the inspector.";
        }
    }

    public string InspectorEditableSummary
    {
        get => _inspectorEditableSummary;
        set
        {
            if (SetProperty(ref _inspectorEditableSummary, value))
            {
                NotifyInspectorEditCommand();
            }
        }
    }

    public bool CanEditInspectorSummary
    {
        get
        {
            if (_selectedAssetAccessor() is not null || _selectedAssetDirectoryAccessor() is not null)
            {
                return false;
            }

            var selectedDocument = _selectedDocumentAccessor();
            return selectedDocument is not null
                && selectedDocument.Document.DocumentType != EditorDocumentType.ProjectOverview;
        }
    }

    public bool ShowLampTestButton
    {
        get
        {
            if (_selectedAssetAccessor() is not null || _selectedAssetDirectoryAccessor() is not null)
            {
                return false;
            }

            var selectedDocument = _selectedDocumentAccessor();
            return selectedDocument is not null
                && selectedDocument.Document.DocumentType == EditorDocumentType.Panel2D
                && _activeDocumentContext.ActivePanelSelection is PanelSelectionInfo panelSelection
                && selectedDocument.TryGetPanelElement(panelSelection, out var selectedElement)
                && selectedElement.Kind == PanelElementKind.Lamp;
        }
    }

    public void NotifyContextChanged()
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (!ShowLampTestButton && selectedDocument is not null && selectedDocument.RuntimeState.IsLampTestActive)
        {
            selectedDocument.RuntimeState.LampTestObjectId = null;
            selectedDocument.NotifyPanelVisualPreviewChanged();
        }

        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(InspectorType));
        OnPropertyChanged(nameof(InspectorPath));
        OnPropertyChanged(nameof(InspectorSummary));
        OnPropertyChanged(nameof(CanEditInspectorSummary));
        OnPropertyChanged(nameof(ShowLampTestButton));

        if (!ShouldSuppressPropertyRowRefresh() && ShouldRebuildRowsForContextChange())
        {
            RebuildPropertyRows();
        }

        InspectorEditableSummary = _selectedDocumentAccessor()?.ContentSummary ?? string.Empty;
        NotifyInspectorEditCommand();
    }

    public void NotifyPanelChanged(PanelChangeEvent panelChange)
    {
        var selectedDocument = _selectedDocumentAccessor();
        var selection = _activeDocumentContext.ActivePanelSelection;
        if (selectedDocument is null || selection is not PanelSelectionInfo panelSelection)
        {
            NotifyContextChanged();
            return;
        }

        if (!string.IsNullOrWhiteSpace(panelChange.ObjectId)
            && !string.Equals(panelChange.ObjectId, panelSelection.ObjectId, StringComparison.Ordinal))
        {
            return;
        }

        if (panelChange.ChangedProperties.HasFlag(PanelChangeProperties.Structure))
        {
            RebuildPropertyRows();
            return;
        }

        if (selectedDocument.Document.DocumentType == EditorDocumentType.Face)
        {
            if (!selectedDocument.TryGetFaceElement(panelSelection, out var selectedFaceElement))
            {
                RebuildPropertyRows();
                return;
            }

            RefreshFacePropertyRowValues(selectedFaceElement);
            OnPropertyChanged(nameof(InspectorSummary));
            return;
        }

        if (!selectedDocument.TryGetPanelElement(panelSelection, out var selectedElement))
        {
            RebuildPropertyRows();
            return;
        }

        RefreshPropertyRowValues(selectedElement);
        OnPropertyChanged(nameof(InspectorSummary));
    }

    private void RebuildPropertyRows()
    {
        _propertyRows.Clear();

        var loadedProjectForAsset = _loadedProjectAccessor();
        var selectedAssetForRows = _selectedAssetAccessor();
        if (loadedProjectForAsset is not null && selectedAssetForRows is not null)
        {
            AssetInspectorDetailsBuilder.BuildRows(_propertyRows, loadedProjectForAsset, selectedAssetForRows.FullPath, selectedAssetForRows.IsDirectory);
            OnPropertyChanged(nameof(InspectorPropertyRows));
            return;
        }

        var selectedDirectoryForRows = _selectedAssetDirectoryAccessor();
        if (loadedProjectForAsset is not null && selectedDirectoryForRows is not null)
        {
            AssetInspectorDetailsBuilder.BuildRows(_propertyRows, loadedProjectForAsset, selectedDirectoryForRows.FullPath, isDirectory: true);
            OnPropertyChanged(nameof(InspectorPropertyRows));
            return;
        }

        var selectedDocument = _selectedDocumentAccessor();
        var selection = _activeDocumentContext.ActivePanelSelection;
        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && selection is PanelSelectionInfo maskSelection
            && selectedDocument.GetFaceDocument().MaskLayer is FaceMaskLayerModel selectedMaskLayer
            && FaceMaskLayerSelectionService.IsMaskLayerSelection(maskSelection))
        {
            RebuildFaceMaskLayerPropertyRows(selectedDocument, selectedMaskLayer);
            return;
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && selection is PanelSelectionInfo faceSelection
            && selectedDocument.TryGetFaceElement(faceSelection, out var selectedFaceElement))
        {
            RebuildFacePropertyRows(selectedDocument, selectedFaceElement);
            return;
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && (selection is not PanelSelectionInfo selectedFaceSelection
                || !selectedDocument.TryGetFaceElement(selectedFaceSelection, out _)))
        {
            RebuildFaceDocumentPropertyRows(selectedDocument);
            return;
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Panel2D
            && selection is PanelSelectionInfo sourceShapeSelection
            && string.Equals(sourceShapeSelection.Kind, PanelFaceSourceShapeCommands.SelectionKind, StringComparison.Ordinal)
            && selectedDocument.TryGetPanelFaceSourceShape(sourceShapeSelection.ObjectId, out var selectedSourceShape))
        {
            RebuildFaceSourceShapePropertyRows(selectedDocument, selectedSourceShape);
            return;
        }

        if (selectedDocument is null)
        {
            var loadedProject = _loadedProjectAccessor();
            var selectedAsset = _selectedAssetAccessor();
            if (loadedProject is not null && selectedAsset is not null)
            {
                AssetInspectorDetailsBuilder.BuildRows(_propertyRows, loadedProject, selectedAsset.FullPath, selectedAsset.IsDirectory);
                OnPropertyChanged(nameof(InspectorPropertyRows));
                return;
            }

            var selectedAssetDirectory = _selectedAssetDirectoryAccessor();
            if (loadedProject is not null && selectedAssetDirectory is not null)
            {
                AssetInspectorDetailsBuilder.BuildRows(_propertyRows, loadedProject, selectedAssetDirectory.FullPath, isDirectory: true);
                OnPropertyChanged(nameof(InspectorPropertyRows));
                return;
            }
        }

        if (selectedDocument is null || selection is not PanelSelectionInfo selectedSelection || !selectedDocument.TryGetPanelElement(selectedSelection, out var selectedElement))
        {
            _hadInspectorSelection = false;
            _lastInspectorSelectionObjectId = null;
            _lastInspectorSelectionKind = null;
            _lastInspectorFaceSelectionKind = null;
            OnPropertyChanged(nameof(InspectorPropertyRows));
            return;
        }

        _lastInspectorFaceSelectionKind = null;
        _propertyRows.Add(new InspectorTextPropertyViewModel(
            "Name",
            "Common",
            selectedElement.Name,
            commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update name", new PanelElementModelUpdate { Name = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "X",
            "Transform",
            selectedElement.X,
            commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update X", new PanelElementModelUpdate { X = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Y",
            "Transform",
            selectedElement.Y,
            commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update Y", new PanelElementModelUpdate { Y = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Width",
            "Transform",
            selectedElement.Width,
            commit: value => value > 0
                ? TryApplyUpdate(selectedElement.ObjectId, "Update width", new PanelElementModelUpdate { Width = value })
                : "Width must be greater than zero."));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Height",
            "Transform",
            selectedElement.Height,
            commit: value => value > 0
                ? TryApplyUpdate(selectedElement.ObjectId, "Update height", new PanelElementModelUpdate { Height = value })
                : "Height must be greater than zero."));
        _propertyRows.Add(new InspectorBoolPropertyViewModel(
            "Locked",
            "Common",
            selectedElement.IsLocked,
            commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update lock state", new PanelElementModelUpdate { IsLocked = value })));
        _propertyRows.Add(new InspectorBoolPropertyViewModel(
            "Visible",
            "Common",
            selectedElement.IsVisible,
            commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update visibility", new PanelElementModelUpdate { IsVisible = value })));

        AddTypeSpecificRows(selectedElement);
        _hadInspectorSelection = true;
        _lastInspectorSelectionObjectId = selectedElement.ObjectId;
        _lastInspectorSelectionKind = selectedElement.Kind;

        OnPropertyChanged(nameof(InspectorPropertyRows));
    }


    private void RebuildFaceSourceShapePropertyRows(DocumentTabViewModel selectedDocument, PanelFaceSourceShapeModel sourceShape)
    {
        _lastInspectorFaceSelectionKind = PanelFaceSourceShapeCommands.SelectionKind;
        _lastInspectorSelectionObjectId = sourceShape.Id;
        _lastInspectorSelectionKind = null;
        _hadInspectorSelection = true;

        if (_generateFaceFromSourceShapeCommand is not null)
        {
            _propertyRows.Add(new InspectorActionPropertyViewModel("Create Face from this Face Source Shape", "Actions", _generateFaceFromSourceShapeCommand));
        }

        _propertyRows.Add(new InspectorTextPropertyViewModel("Name", "Face Source Shape", sourceShape.Name, commit: value => TryApplyFaceSourceShapeUpdate(selectedDocument, sourceShape, new PanelFaceSourceShapeModel { Id = sourceShape.Id, Name = value, Type = sourceShape.Type, TopLeft = sourceShape.TopLeft, TopRight = sourceShape.TopRight, BottomRight = sourceShape.BottomRight, BottomLeft = sourceShape.BottomLeft })));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Type", "Face Source Shape", "Perspective Rectangle"));
        AddFaceSourceShapePointRows(selectedDocument, sourceShape, "Top Left", 0, sourceShape.TopLeft);
        AddFaceSourceShapePointRows(selectedDocument, sourceShape, "Top Right", 1, sourceShape.TopRight);
        AddFaceSourceShapePointRows(selectedDocument, sourceShape, "Bottom Right", 2, sourceShape.BottomRight);
        AddFaceSourceShapePointRows(selectedDocument, sourceShape, "Bottom Left", 3, sourceShape.BottomLeft);
        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    private void AddFaceSourceShapePointRows(DocumentTabViewModel selectedDocument, PanelFaceSourceShapeModel sourceShape, string label, int pointIndex, FacePointModel point)
    {
        _propertyRows.Add(new InspectorDoublePropertyViewModel($"{label} X", "Corners", point.X, commit: value => TryApplyFaceSourceShapePointUpdate(selectedDocument, sourceShape, pointIndex, value, isX: true)));
        _propertyRows.Add(new InspectorDoublePropertyViewModel($"{label} Y", "Corners", point.Y, commit: value => TryApplyFaceSourceShapePointUpdate(selectedDocument, sourceShape, pointIndex, value, isX: false)));
    }

    private string? TryApplyFaceSourceShapePointUpdate(DocumentTabViewModel selectedDocument, PanelFaceSourceShapeModel sourceShape, int pointIndex, double value, bool isX)
    {
        var current = pointIndex switch
        {
            0 => sourceShape.TopLeft,
            1 => sourceShape.TopRight,
            2 => sourceShape.BottomRight,
            _ => sourceShape.BottomLeft
        };
        var point = new FacePointModel { X = isX ? value : current.X, Y = isX ? current.Y : value };
        return TryApplyFaceSourceShapeUpdate(selectedDocument, sourceShape, CreateFaceSourceShapeWithPoint(sourceShape, pointIndex, point));
    }

    private string? TryApplyFaceSourceShapeUpdate(DocumentTabViewModel selectedDocument, PanelFaceSourceShapeModel sourceShape, PanelFaceSourceShapeModel updated)
    {
        var command = PanelFaceSourceShapeCommands.CreateUpdateCommand(selectedDocument.DocumentId, selectedDocument, updated, "Update Face Source Shape");
        return _executeCanvasCommand(selectedDocument.DocumentId, command) ? null : "Could not update Face Source Shape.";
    }

    private static PanelFaceSourceShapeModel CreateFaceSourceShapeWithPoint(PanelFaceSourceShapeModel source, int pointIndex, FacePointModel point)
    {
        return new PanelFaceSourceShapeModel
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            TopLeft = pointIndex == 0 ? point : source.TopLeft,
            TopRight = pointIndex == 1 ? point : source.TopRight,
            BottomRight = pointIndex == 2 ? point : source.BottomRight,
            BottomLeft = pointIndex == 3 ? point : source.BottomLeft
        };
    }

    private bool ShouldRebuildRowsForContextChange()
    {
        var selectedDocument = _selectedDocumentAccessor();
        var selection = _activeDocumentContext.ActivePanelSelection;
        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && selection is PanelSelectionInfo maskSelection
            && selectedDocument.GetFaceDocument().MaskLayer is FaceMaskLayerModel selectedMaskLayer
            && FaceMaskLayerSelectionService.IsMaskLayerSelection(maskSelection))
        {
            if (!_hadInspectorSelection)
            {
                return true;
            }

            return !string.Equals(_lastInspectorSelectionObjectId, selectedMaskLayer.Id, StringComparison.Ordinal)
                || !string.Equals(_lastInspectorFaceSelectionKind, FaceMaskLayerSelectionService.KindToken, StringComparison.Ordinal);
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && selection is PanelSelectionInfo faceSelection
            && selectedDocument.TryGetFaceElement(faceSelection, out var selectedFaceElement))
        {
            if (!_hadInspectorSelection)
            {
                return true;
            }

            return !string.Equals(_lastInspectorSelectionObjectId, selectedFaceElement.ObjectId, StringComparison.Ordinal)
                || !string.Equals(_lastInspectorFaceSelectionKind, FaceSelectionService.GetKindToken(selectedFaceElement), StringComparison.Ordinal);
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Face
            && (selection is not PanelSelectionInfo selectedFaceSelection
                || !selectedDocument.TryGetFaceElement(selectedFaceSelection, out _)))
        {
            return true;
        }

        if (selectedDocument is not null
            && selectedDocument.Document.DocumentType == EditorDocumentType.Panel2D
            && selection is PanelSelectionInfo sourceShapeSelection
            && string.Equals(sourceShapeSelection.Kind, PanelFaceSourceShapeCommands.SelectionKind, StringComparison.Ordinal)
            && selectedDocument.TryGetPanelFaceSourceShape(sourceShapeSelection.ObjectId, out var selectedSourceShape))
        {
            if (!_hadInspectorSelection)
            {
                return true;
            }

            return !string.Equals(_lastInspectorSelectionObjectId, selectedSourceShape.Id, StringComparison.Ordinal)
                || !string.Equals(_lastInspectorFaceSelectionKind, PanelFaceSourceShapeCommands.SelectionKind, StringComparison.Ordinal);
        }

        if (selectedDocument is null || selection is not PanelSelectionInfo selectedSelection || !selectedDocument.TryGetPanelElement(selectedSelection, out var selectedElement))
        {
            return _hadInspectorSelection || _propertyRows.Count > 0;
        }

        if (!_hadInspectorSelection)
        {
            return true;
        }

        return !string.Equals(_lastInspectorSelectionObjectId, selectedElement.ObjectId, StringComparison.Ordinal)
            || _lastInspectorSelectionKind != selectedElement.Kind;
    }

    private void AddTypeSpecificRows(PanelElementModel selectedElement)
    {
        if (selectedElement.Kind is PanelElementKind.Lamp or PanelElementKind.Reel or PanelElementKind.SevenSegment)
        {
            _propertyRows.Add(new InspectorIntPropertyViewModel(
                "Display Number",
                "Type-specific",
                selectedElement.DisplayNumber,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update display number", new PanelElementModelUpdate { DisplayNumber = value })));
        }

        if (selectedElement.Kind is PanelElementKind.Image or PanelElementKind.Background or PanelElementKind.Lamp or PanelElementKind.Reel)
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel(
                "Asset Path",
                "Type-specific",
                selectedElement.AssetPath ?? string.Empty,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update asset path", new PanelElementModelUpdate { AssetPath = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Background or PanelElementKind.Reel)
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel(
                "Secondary Asset",
                "Type-specific",
                selectedElement.SecondaryAssetPath ?? string.Empty,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update secondary asset path", new PanelElementModelUpdate { SecondaryAssetPath = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Background)
        {
            _propertyRows.Add(new InspectorColorPropertyViewModel(
                "Color",
                "Type-specific",
                selectedElement.OnColorHex ?? string.Empty,
                commit: value => TryApplyColorUpdate(selectedElement.ObjectId, "Update background color", new PanelElementModelUpdate { OnColorHex = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Lamp or PanelElementKind.SevenSegment or PanelElementKind.Alpha or PanelElementKind.VfdDotMatrix)
        {
            _propertyRows.Add(new InspectorColorPropertyViewModel(
                "On Color",
                "Type-specific",
                selectedElement.OnColorHex ?? string.Empty,
                commit: value => TryApplyColorUpdate(selectedElement.ObjectId, "Update on color", new PanelElementModelUpdate { OnColorHex = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Lamp)
        {
            _propertyRows.Add(new InspectorColorPropertyViewModel(
                "Off Color",
                "Type-specific",
                selectedElement.OffColorHex ?? string.Empty,
                commit: value => TryApplyColorUpdate(selectedElement.ObjectId, "Update off color", new PanelElementModelUpdate { OffColorHex = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Lamp or PanelElementKind.Alpha)
        {
            _propertyRows.Add(new InspectorColorPropertyViewModel(
                "Text Color",
                "Type-specific",
                selectedElement.TextColorHex ?? string.Empty,
                commit: value => TryApplyColorUpdate(selectedElement.ObjectId, "Update text color", new PanelElementModelUpdate { TextColorHex = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Lamp or PanelElementKind.Alpha)
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel(
                "Display Text",
                "Type-specific",
                selectedElement.DisplayText ?? string.Empty,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update display text", new PanelElementModelUpdate { DisplayText = NormalizeOptionalText(value) })));
        }

        if (selectedElement.Kind is PanelElementKind.Alpha)
        {
            var selectedDisplayKind = string.Equals(selectedElement.SegmentDisplayType, "led14seg", StringComparison.OrdinalIgnoreCase)
                ? "14 Segment"
                : "16 Segment";
            _propertyRows.Add(new InspectorChoicePropertyViewModel(
                "Segment Type",
                "Type-specific",
                ["14 Segment", "16 Segment"],
                selectedDisplayKind,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update segment type", new PanelElementModelUpdate
                {
                    SegmentDisplayType = string.Equals(value, "14 Segment", StringComparison.Ordinal) ? "led14seg" : "led16seg"
                })));
            _propertyRows.Add(new InspectorBoolPropertyViewModel(
                "Decimal Point",
                "Type-specific",
                selectedElement.ShowDecimalPoint,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update decimal point visibility", new PanelElementModelUpdate { ShowDecimalPoint = value })));
            _propertyRows.Add(new InspectorBoolPropertyViewModel(
                "Comma",
                "Type-specific",
                selectedElement.ShowCommaTail,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update comma visibility", new PanelElementModelUpdate { ShowCommaTail = value })));
        }

        if (selectedElement.Kind is PanelElementKind.Lamp)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Text Font Name", "Type-specific", selectedElement.TextBoxFontName ?? "Tahoma"));
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Text Font Style", "Type-specific", selectedElement.TextBoxFontStyle ?? "Regular"));
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Text Font Size", "Type-specific", selectedElement.TextBoxFontSize ?? "8"));
        }

        if (selectedElement.Kind is PanelElementKind.Reel)
        {
            _propertyRows.Add(new InspectorIntPropertyViewModel(
                "Stops",
                "Type-specific",
                selectedElement.Stops,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update stops", new PanelElementModelUpdate { Stops = value })));

            if (selectedElement.VisibleScale.HasValue)
            {
                _propertyRows.Add(new InspectorDoublePropertyViewModel(
                    "Visible Scale",
                    "Type-specific",
                    selectedElement.VisibleScale.Value,
                    commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update visible scale", new PanelElementModelUpdate { VisibleScale = value }),
                    format: "G17"));
            }

            _propertyRows.Add(new InspectorDoublePropertyViewModel(
                "Band Offset",
                "Type-specific",
                selectedElement.BandOffset ?? 0d,
                commit: value => TryApplyBandOffsetUpdate(selectedElement.ObjectId, value),
                format: "G17"));
        }

        if (selectedElement.Kind is PanelElementKind.Reel or PanelElementKind.Alpha)
        {
            _propertyRows.Add(new InspectorBoolPropertyViewModel(
                "Reversed",
                "Type-specific",
                selectedElement.IsReversed ?? false,
                commit: value => TryApplyUpdate(selectedElement.ObjectId, "Update reversed", new PanelElementModelUpdate { IsReversed = value })));
        }

        if (selectedElement.ImportSource is not null)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Import Format", "Metadata", selectedElement.ImportSource.Format));
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Import Reference", "Metadata", selectedElement.ImportSource.Reference ?? string.Empty));
            if (selectedElement.SourceComponentIndex.HasValue)
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Component Index", "Metadata", selectedElement.SourceComponentIndex.Value.ToString(CultureInfo.InvariantCulture)));
            }
            if (selectedElement.SourceElementIndex.HasValue)
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Element Index", "Metadata", selectedElement.SourceElementIndex.Value.ToString(CultureInfo.InvariantCulture)));
            }
            if (!string.IsNullOrWhiteSpace(selectedElement.SharedSourceSetId))
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Shared Source Set", "Metadata", selectedElement.SharedSourceSetId));
            }
            if (selectedElement.SharedSourceSetCount.HasValue)
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Shared Source Count", "Metadata", selectedElement.SharedSourceSetCount.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }


    private void RebuildFaceDocumentPropertyRows(DocumentTabViewModel selectedDocument)
    {
        var faceDocument = selectedDocument.GetFaceDocument();
        AddFaceCabinetAssignmentRows(selectedDocument, faceDocument);
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Panel2D Document", "Face Provenance", faceDocument.SourcePanel2DDocumentId ?? string.Empty));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Face Source Shape Output Bounds", "Face Provenance", faceDocument.SourceRegion is not null ? FormatSourceRegion(faceDocument.SourceRegion) : string.Empty));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Generated Element Count", "Face Provenance", CountGeneratedElements(faceDocument).ToString()));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Last Regenerated", "Face Provenance", FormatTimestamp(faceDocument.LastRegeneratedAtUtc)));
        AddFaceMaskLayerSummaryRows(faceDocument.MaskLayer, "Mask Layer");
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Face Commands", "Workflow", "Use File > Regenerate Face, File > Validate Face, File > Open Source Panel2D, or Cabinet Assignment > Cabinet Face Target."));

        var missingMachineReferenceCount = CountMissingMachineReferences(faceDocument);
        if (missingMachineReferenceCount > 0)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Machine Reference Warnings", "Diagnostics", $"{missingMachineReferenceCount} runtime-linked element(s) are missing machine references."));
        }

        _hadInspectorSelection = false;
        _lastInspectorSelectionObjectId = null;
        _lastInspectorSelectionKind = null;
        _lastInspectorFaceSelectionKind = null;
        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    private void AddFaceCabinetAssignmentRows(DocumentTabViewModel selectedDocument, FaceDocumentModel faceDocument)
    {
        var availableTargets = GetAvailableCabinetFaceTargets();
        var assignedTargetId = NormalizeCabinetFaceTargetId(faceDocument.AssignedCabinetFaceTargetId);
        var choices = new List<string> { "(None)" };
        var labelsById = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var target in availableTargets)
        {
            if (string.IsNullOrWhiteSpace(target.Id) || !target.IsValid)
            {
                continue;
            }

            var label = FormatCabinetFaceTargetChoice(target);
            labelsById[target.Id] = label;
            choices.Add(label);
        }

        var currentChoice = "(None)";
        if (!string.IsNullOrWhiteSpace(assignedTargetId))
        {
            if (!labelsById.TryGetValue(assignedTargetId, out currentChoice))
            {
                currentChoice = $"{assignedTargetId} (saved; unavailable)";
                choices.Add(currentChoice);
            }
        }

        _propertyRows.Add(new InspectorChoicePropertyViewModel(
            "Cabinet Face Target",
            "Cabinet Assignment",
            choices,
            currentChoice,
            commit: choice => TryApplyFaceCabinetTargetAssignment(selectedDocument, choice)));

        _propertyRows.Add(new InspectorInfoPropertyViewModel(
            "Assigned Target ID",
            "Cabinet Assignment",
            assignedTargetId ?? string.Empty));

        if (availableTargets.Count == 0)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel(
                "Available Targets",
                "Cabinet Assignment",
                "No detected OasisFace targets are currently available from open Cabinet3D model viewers."));
        }
    }

    private IReadOnlyList<CabinetFaceTargetViewModel> GetAvailableCabinetFaceTargets()
    {
        return (_openDocumentsAccessor() ?? [])
            .Where(document => document.Document.DocumentType == EditorDocumentType.Cabinet3D)
            .Select(document => document.CabinetViewer)
            .Where(viewer => viewer is not null)
            .SelectMany(viewer => viewer!.FaceTargets)
            .ToArray();
    }

    private static string FormatCabinetFaceTargetChoice(CabinetFaceTargetViewModel target)
    {
        var displayName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Id : target.DisplayName.Trim();
        return $"{displayName} ({target.Id})";
    }

    private string? TryApplyFaceCabinetTargetAssignment(DocumentTabViewModel selectedDocument, string choice)
    {
        var targetId = ParseCabinetFaceTargetChoice(choice);
        var command = FaceMutationCommands.CreateAssignCabinetFaceTargetCommand(selectedDocument.DocumentId, selectedDocument, targetId);
        if (!_executeCanvasCommand(selectedDocument.DocumentId, command))
        {
            return "Unable to update cabinet face target assignment.";
        }

        return null;
    }

    private static string? ParseCabinetFaceTargetChoice(string choice)
    {
        if (string.IsNullOrWhiteSpace(choice) || string.Equals(choice, "(None)", StringComparison.Ordinal))
        {
            return null;
        }

        const string unavailableSuffix = " (saved; unavailable)";
        if (choice.EndsWith(unavailableSuffix, StringComparison.Ordinal))
        {
            return NormalizeCabinetFaceTargetId(choice[..^unavailableSuffix.Length]);
        }

        var match = Regex.Match(choice, @"\((?<id>[^()]*)\)\s*$");
        return match.Success ? NormalizeCabinetFaceTargetId(match.Groups["id"].Value) : NormalizeCabinetFaceTargetId(choice);
    }

    private static string? NormalizeCabinetFaceTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private void RebuildFaceMaskLayerPropertyRows(DocumentTabViewModel selectedDocument, FaceMaskLayerModel maskLayer)
    {
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Name", "Mask Layer", string.IsNullOrWhiteSpace(maskLayer.Name) ? "Face Mask" : maskLayer.Name));
        AddFaceMaskLayerSummaryRows(maskLayer, "Mask Layer");
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Panel2D Document", "Mask Provenance", maskLayer.SourcePanel2DDocumentId ?? selectedDocument.GetFaceDocument().SourcePanel2DDocumentId ?? string.Empty));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Renderer Contract", "Future Renderer Consumption", "Use this single face-sized mask as an aligned opacity/escape map. Do not infer runtime lamp identity from contribution metadata."));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Mask Commands", "Workflow", "Use File > Regenerate Face to regenerate this layer from source metadata, or File > Validate Face to report mask diagnostics."));

        _hadInspectorSelection = true;
        _lastInspectorSelectionObjectId = maskLayer.Id;
        _lastInspectorSelectionKind = null;
        _lastInspectorFaceSelectionKind = FaceMaskLayerSelectionService.KindToken;
        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    private void AddFaceMaskLayerSummaryRows(FaceMaskLayerModel? maskLayer, string category)
    {
        if (maskLayer is null)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Mask Layer", category, "No FaceMaskLayer metadata."));
            return;
        }

        _propertyRows.Add(new InspectorInfoPropertyViewModel("Asset Path", category, maskLayer.AssetPath ?? string.Empty));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Dimensions", category, FormatDimensions(maskLayer.Width, maskLayer.Height)));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Face Source Shape Output Bounds", category, maskLayer.SourceRegion is not null ? FormatSourceRegion(maskLayer.SourceRegion) : string.Empty));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Threshold", category, maskLayer.ExtractionThreshold.ToString()));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Generated", category, FormatTimestamp(maskLayer.GeneratedUtc)));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Contribution Count", category, maskLayer.Contributions.Count.ToString()));
    }

    private void RebuildFacePropertyRows(DocumentTabViewModel selectedDocument, FaceElementModel selectedElement)
    {
        _propertyRows.Add(new InspectorTextPropertyViewModel(
            "Name",
            "Common",
            selectedElement.Name,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update name", new FaceElementModelUpdate { Name = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "X",
            "Transform",
            selectedElement.X,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update X", new FaceElementModelUpdate { X = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Y",
            "Transform",
            selectedElement.Y,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update Y", new FaceElementModelUpdate { Y = value })));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Width",
            "Transform",
            selectedElement.Width,
            commit: value => value > 0
                ? TryApplyFaceUpdate(selectedElement.ObjectId, "Update width", new FaceElementModelUpdate { Width = value })
                : "Width must be greater than zero."));
        _propertyRows.Add(new InspectorDoublePropertyViewModel(
            "Height",
            "Transform",
            selectedElement.Height,
            commit: value => value > 0
                ? TryApplyFaceUpdate(selectedElement.ObjectId, "Update height", new FaceElementModelUpdate { Height = value })
                : "Height must be greater than zero."));
        _propertyRows.Add(new InspectorBoolPropertyViewModel(
            "Locked",
            "Common",
            selectedElement.IsLocked,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update lock state", new FaceElementModelUpdate { IsLocked = value })));
        _propertyRows.Add(new InspectorBoolPropertyViewModel(
            "Visible",
            "Common",
            selectedElement.IsVisible,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update visibility", new FaceElementModelUpdate { IsVisible = value })));
        _propertyRows.Add(new InspectorTextPropertyViewModel(
            "Machine Reference",
            "References",
            selectedElement.LinkedMachineObjectReference?.ToString() ?? string.Empty,
            commit: value => TryApplyFaceMachineReferenceUpdate(selectedElement.ObjectId, value)));
        _propertyRows.Add(new InspectorTextPropertyViewModel(
            "Linked Panel2D Element",
            "References",
            selectedElement.LinkedPanel2DElementId ?? string.Empty,
            commit: value => TryApplyFaceUpdate(selectedElement.ObjectId, "Update linked Panel2D element", new FaceElementModelUpdate { HasLinkedPanel2DElementId = true, LinkedPanel2DElementId = NormalizeOptionalText(value) })));

        if (selectedElement is FaceArtworkElement artwork)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Asset Path", "Artwork", artwork.AssetPath ?? string.Empty));
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Panel2D Document", "Artwork", artwork.SourcePanel2DDocumentId ?? string.Empty));
            if (artwork.SourceRegion is not null)
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Face Source Shape Output Bounds", "Artwork", FormatSourceRegion(artwork.SourceRegion)));
            }

            if (artwork.Provenance is not null)
            {
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Artwork Generator", "Provenance", artwork.Provenance.Generator));
                _propertyRows.Add(new InspectorInfoPropertyViewModel("Source Asset", "Provenance", artwork.Provenance.SourceAssetPath ?? string.Empty));
            }
        }

        _hadInspectorSelection = true;
        _lastInspectorSelectionObjectId = selectedElement.ObjectId;
        _lastInspectorSelectionKind = null;
        _lastInspectorFaceSelectionKind = FaceSelectionService.GetKindToken(selectedElement);
        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    private void RefreshPropertyRowValues(PanelElementModel selectedElement)
    {
        foreach (var row in _propertyRows)
        {
            switch (row.DisplayName)
            {
                case "Name" when row is InspectorTextPropertyViewModel textRow:
                    textRow.SetCommittedValue(selectedElement.Name);
                    break;
                case "X" when row is InspectorDoublePropertyViewModel doubleRow:
                    doubleRow.SetCommittedValue(selectedElement.X);
                    break;
                case "Y" when row is InspectorDoublePropertyViewModel yRow:
                    yRow.SetCommittedValue(selectedElement.Y);
                    break;
                case "Width" when row is InspectorDoublePropertyViewModel widthRow:
                    widthRow.SetCommittedValue(selectedElement.Width);
                    break;
                case "Height" when row is InspectorDoublePropertyViewModel heightRow:
                    heightRow.SetCommittedValue(selectedElement.Height);
                    break;
                case "Locked" when row is InspectorBoolPropertyViewModel lockedRow:
                    lockedRow.SetCommittedValue(selectedElement.IsLocked);
                    break;
                case "Visible" when row is InspectorBoolPropertyViewModel visibleRow:
                    visibleRow.SetCommittedValue(selectedElement.IsVisible);
                    break;
                case "Display Number" when row is InspectorIntPropertyViewModel displayRow:
                    displayRow.SetCommittedValue(selectedElement.DisplayNumber);
                    break;
                case "Asset Path" when row is InspectorTextPropertyViewModel assetPathRow:
                    assetPathRow.SetCommittedValue(selectedElement.AssetPath);
                    break;
                case "Secondary Asset" when row is InspectorTextPropertyViewModel secondaryRow:
                    secondaryRow.SetCommittedValue(selectedElement.SecondaryAssetPath);
                    break;
                case "On Color" when row is InspectorColorPropertyViewModel onColorRow:
                    onColorRow.SetCommittedValue(selectedElement.OnColorHex);
                    break;
                case "Color" when row is InspectorColorPropertyViewModel colorRow:
                    colorRow.SetCommittedValue(selectedElement.OnColorHex);
                    break;
                case "Off Color" when row is InspectorColorPropertyViewModel offColorRow:
                    offColorRow.SetCommittedValue(selectedElement.OffColorHex);
                    break;
                case "Text Color" when row is InspectorColorPropertyViewModel textColorRow:
                    textColorRow.SetCommittedValue(selectedElement.TextColorHex);
                    break;
                case "Display Text" when row is InspectorTextPropertyViewModel displayTextRow:
                    displayTextRow.SetCommittedValue(selectedElement.DisplayText);
                    break;
                case "Segment Type" when row is InspectorChoicePropertyViewModel segmentTypeRow:
                    segmentTypeRow.SetCommittedValue(string.Equals(selectedElement.SegmentDisplayType, "led14seg", StringComparison.OrdinalIgnoreCase) ? "14 Segment" : "16 Segment");
                    break;
                case "Decimal Point" when row is InspectorBoolPropertyViewModel decimalPointRow:
                    decimalPointRow.SetCommittedValue(selectedElement.ShowDecimalPoint);
                    break;
                case "Comma" when row is InspectorBoolPropertyViewModel commaRow:
                    commaRow.SetCommittedValue(selectedElement.ShowCommaTail);
                    break;
                case "Text Font Name" when row is InspectorInfoPropertyViewModel fontNameRow:
                    fontNameRow.SetCommittedValue(selectedElement.TextBoxFontName ?? "Tahoma");
                    break;
                case "Text Font Style" when row is InspectorInfoPropertyViewModel fontStyleRow:
                    fontStyleRow.SetCommittedValue(selectedElement.TextBoxFontStyle ?? "Regular");
                    break;
                case "Text Font Size" when row is InspectorInfoPropertyViewModel fontSizeRow:
                    fontSizeRow.SetCommittedValue(selectedElement.TextBoxFontSize ?? "8");
                    break;
                case "Stops" when row is InspectorIntPropertyViewModel stopsRow:
                    stopsRow.SetCommittedValue(selectedElement.Stops);
                    break;
                case "Visible Scale" when row is InspectorDoublePropertyViewModel scaleRow && selectedElement.VisibleScale.HasValue:
                    scaleRow.SetCommittedValue(selectedElement.VisibleScale.Value);
                    break;
                case "Reversed" when row is InspectorBoolPropertyViewModel reversedRow:
                    reversedRow.SetCommittedValue(selectedElement.IsReversed ?? false);
                    break;
                case "Band Offset" when row is InspectorDoublePropertyViewModel bandOffsetRow:
                    bandOffsetRow.SetCommittedValue(selectedElement.BandOffset ?? 0d);
                    break;
                case "Import Format" when row is InspectorInfoPropertyViewModel importFormatRow:
                    importFormatRow.SetCommittedValue(selectedElement.ImportSource?.Format ?? string.Empty);
                    break;
                case "Import Reference" when row is InspectorInfoPropertyViewModel importReferenceRow:
                    importReferenceRow.SetCommittedValue(selectedElement.ImportSource?.Reference ?? string.Empty);
                    break;
            }
        }
    }

    private void RefreshFacePropertyRowValues(FaceElementModel selectedElement)
    {
        foreach (var row in _propertyRows)
        {
            switch (row.DisplayName)
            {
                case "Name" when row is InspectorTextPropertyViewModel textRow:
                    textRow.SetCommittedValue(selectedElement.Name);
                    break;
                case "X" when row is InspectorDoublePropertyViewModel doubleRow:
                    doubleRow.SetCommittedValue(selectedElement.X);
                    break;
                case "Y" when row is InspectorDoublePropertyViewModel yRow:
                    yRow.SetCommittedValue(selectedElement.Y);
                    break;
                case "Width" when row is InspectorDoublePropertyViewModel widthRow:
                    widthRow.SetCommittedValue(selectedElement.Width);
                    break;
                case "Height" when row is InspectorDoublePropertyViewModel heightRow:
                    heightRow.SetCommittedValue(selectedElement.Height);
                    break;
                case "Locked" when row is InspectorBoolPropertyViewModel lockedRow:
                    lockedRow.SetCommittedValue(selectedElement.IsLocked);
                    break;
                case "Visible" when row is InspectorBoolPropertyViewModel visibleRow:
                    visibleRow.SetCommittedValue(selectedElement.IsVisible);
                    break;
                case "Machine Reference" when row is InspectorTextPropertyViewModel referenceRow:
                    referenceRow.SetCommittedValue(selectedElement.LinkedMachineObjectReference?.ToString() ?? string.Empty);
                    break;
                case "Linked Panel2D Element" when row is InspectorTextPropertyViewModel panelRow:
                    panelRow.SetCommittedValue(selectedElement.LinkedPanel2DElementId ?? string.Empty);
                    break;
            }
        }
    }

    private string? TryApplyColorUpdate(string objectId, string description, PanelElementModelUpdate update)
    {
        return TryApplyUpdate(objectId, description, update, suppressInspectorRefresh: true);
    }

    private string? TryApplyBandOffsetUpdate(string objectId, double value)
    {
        if (!PanelElementValidation.IsValidBandOffset(value))
        {
            return "Enter a value from -1 to 1.";
        }

        return TryApplyUpdate(objectId, "Update band offset", new PanelElementModelUpdate { BandOffset = value });
    }

    private string? TryApplyUpdate(string objectId, string description, PanelElementModelUpdate update, bool suppressInspectorRefresh = false)
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null)
        {
            return "No active document.";
        }

        var existing = selectedDocument.GetPanelElements()
            .FirstOrDefault(element => string.Equals(element.ObjectId, objectId, StringComparison.Ordinal));
        if (existing is null)
        {
            return "Selected element no longer exists.";
        }

        var updated = PanelElementModelUpdater.Apply(existing, update);
        if (!PanelElementValidation.IsValidForInspectorUpdate(updated))
        {
            return "Invalid element dimensions.";
        }

        var command = CanvasMutationCommands.CreateUpdateElementCommand(
            selectedDocument.DocumentId,
            selectedDocument,
            objectId,
            updated,
            description);
        if (suppressInspectorRefresh)
        {
            _suppressPropertyRowRefreshUntilUtc = DateTime.UtcNow.AddSeconds(1);
        }

        var executed = _executeCanvasCommand(selectedDocument.DocumentId, command);
        if (!executed)
        {
            _suppressPropertyRowRefreshUntilUtc = DateTime.MinValue;
            return "Unable to apply update.";
        }

        return null;
    }


    private string? TryApplyFaceMachineReferenceUpdate(string objectId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TryApplyFaceUpdate(objectId, "Update machine reference", new FaceElementModelUpdate
            {
                HasLinkedMachineObjectReference = true,
                LinkedMachineObjectReference = null
            });
        }

        if (!MachineObjectReference.TryParse(value, out var reference))
        {
            return "Use a machine reference such as lamp:17, reel:2, alpha:0, sevenSegment:12, or input:start.";
        }

        return TryApplyFaceUpdate(objectId, "Update machine reference", new FaceElementModelUpdate
        {
            HasLinkedMachineObjectReference = true,
            LinkedMachineObjectReference = reference
        });
    }

    private string? TryApplyFaceUpdate(string objectId, string description, FaceElementModelUpdate update)
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null || selectedDocument.Document.DocumentType != EditorDocumentType.Face)
        {
            return "No active face document.";
        }

        var existing = selectedDocument.GetFaceElements()
            .FirstOrDefault(element => string.Equals(element.ObjectId, objectId, StringComparison.Ordinal));
        if (existing is null)
        {
            return "Selected face element no longer exists.";
        }

        var updated = FaceElementModelUpdater.Apply(existing, update);
        if (!FaceElementValidation.IsValidForInspectorUpdate(updated))
        {
            return "Invalid face element dimensions.";
        }

        var command = FaceMutationCommands.CreateUpdateElementCommand(
            selectedDocument.DocumentId,
            selectedDocument,
            objectId,
            updated,
            description);

        var executed = _executeCanvasCommand(selectedDocument.DocumentId, command);
        if (!executed)
        {
            return "Unable to apply update.";
        }

        return null;
    }

    private bool ShouldSuppressPropertyRowRefresh()
    {
        return DateTime.UtcNow < _suppressPropertyRowRefreshUntilUtc;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NicifyElementKind(PanelElementKind kind)
    {
        var serializedKind = Panel2DDocumentStorage.SerializeElementKind(kind);
        if (string.IsNullOrWhiteSpace(serializedKind))
        {
            return kind.ToString();
        }

        var titleCased = char.ToUpperInvariant(serializedKind[0]) + serializedKind[1..];
        return Regex.Replace(titleCased, "([a-z0-9])([A-Z])", "$1 $2");
    }

    private static string NicifyFaceElementKind(FaceElementModel element)
    {
        return element switch
        {
            FaceArtworkElement => "Face Artwork",
            FaceLampWindowElement => "Face Lamp Window",
            FaceSevenSegmentDisplayElement => "Face Seven Segment Display",
            FaceAlphaDisplayElement => "Face Alpha Display",
            FaceButtonElement => "Face Button",
            _ => "Face Element"
        };
    }


    private static int CountGeneratedElements(FaceDocumentModel faceDocument)
    {
        return faceDocument.Elements.Count(element => !string.IsNullOrWhiteSpace(element.LinkedPanel2DElementId)
            || element is FaceArtworkElement { Provenance: not null });
    }

    private static int CountMissingMachineReferences(FaceDocumentModel faceDocument)
    {
        return faceDocument.Elements.Count(element => IsRuntimeLinkedFaceElement(element)
            && (element.LinkedMachineObjectReference is not MachineObjectReference reference || reference.IsEmpty));
    }

    private static bool IsRuntimeLinkedFaceElement(FaceElementModel element)
    {
        return element is FaceLampWindowElement
            or FaceReelDisplayElement
            or FaceSevenSegmentDisplayElement
            or FaceAlphaDisplayElement
            or FaceButtonElement;
    }

    private static string FormatTimestamp(DateTime? timestampUtc)
    {
        return timestampUtc.HasValue
            ? $"{timestampUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC"
            : string.Empty;
    }

    private static string FormatDimensions(int width, int height)
    {
        return width > 0 && height > 0 ? $"{width}×{height}" : string.Empty;
    }

    private static string FormatSourceRegion(FaceSourceRegionModel region)
    {
        return $"{region.Kind}: {Math.Round(region.X, 2)}, {Math.Round(region.Y, 2)}, {Math.Round(region.Width, 2)}×{Math.Round(region.Height, 2)}";
    }

    private bool CanApplyInspectorSummary()
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (!CanEditInspectorSummary || selectedDocument is null)
        {
            return false;
        }

        return !string.Equals(
            selectedDocument.ContentSummary,
            InspectorEditableSummary,
            StringComparison.Ordinal);
    }

    private void ApplyInspectorSummary()
    {
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null || !CanEditInspectorSummary)
        {
            return;
        }

        _applySummary(selectedDocument, InspectorEditableSummary);
    }

    private void NotifyInspectorEditCommand()
    {
        if (ApplyInspectorSummaryCommand is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }

    public void SetLampTestActive(bool isActive)
    {
        var selectedLampObjectId = _activeDocumentContext.ActivePanelSelection?.ObjectId;
        if (isActive && string.IsNullOrWhiteSpace(selectedLampObjectId))
        {
            return;
        }

        var targetObjectId = isActive ? selectedLampObjectId : null;
        var selectedDocument = _selectedDocumentAccessor();
        if (selectedDocument is null)
        {
            return;
        }

        if (selectedDocument.RuntimeState.IsLampTestActive == isActive
            && string.Equals(selectedDocument.RuntimeState.LampTestObjectId, targetObjectId, StringComparison.Ordinal))
        {
            return;
        }

        selectedDocument.RuntimeState.LampTestObjectId = targetObjectId;
        Debug.WriteLine($"[LampTest] SetLampTestActive={isActive} document={selectedDocument.DocumentId}");
        selectedDocument.NotifyPanelVisualPreviewChanged();
    }

    private static string BuildSelectedFaceMaskLayerSummary(FaceMaskLayerModel maskLayer)
    {
        var displayName = string.IsNullOrWhiteSpace(maskLayer.Name) ? "Face Mask" : maskLayer.Name.Trim();
        var asset = string.IsNullOrWhiteSpace(maskLayer.AssetPath) ? "No asset path." : $"Asset: {maskLayer.AssetPath}.";
        return $"Selected face mask layer '{displayName}' sized {maskLayer.Width} x {maskLayer.Height}. {asset} Contributions: {maskLayer.Contributions.Count}.";
    }

    private static string BuildSelectedFaceElementSummary(FaceElementModel selectedElement)
    {
        var displayName = string.IsNullOrWhiteSpace(selectedElement.Name)
            ? "Lamp Window"
            : selectedElement.Name.Trim();
        var reference = selectedElement.LinkedMachineObjectReference is MachineObjectReference machineReference && !machineReference.IsEmpty
            ? $" Machine reference: {machineReference}."
            : string.Empty;
        return $"Selected face lamp window '{displayName}' at ({selectedElement.X:0.##}, {selectedElement.Y:0.##}) sized {selectedElement.Width:0.##} x {selectedElement.Height:0.##}.{reference}";
    }

    private static string BuildSelectedElementSummary(PanelElementModel selectedElement)
    {
        var displayName = string.IsNullOrWhiteSpace(selectedElement.Name)
            ? Panel2DDocumentStorage.CreateDefaultElementName(selectedElement.Kind, selectedElement.ObjectId)
            : selectedElement.Name.Trim();
        var kind = Panel2DDocumentStorage.SerializeElementKind(selectedElement.Kind);
        var geometrySummary = $"Selected {kind} '{displayName}' at ({selectedElement.X:0.##}, {selectedElement.Y:0.##}) sized {selectedElement.Width:0.##} x {selectedElement.Height:0.##}.";

        return selectedElement.Kind switch
        {
            PanelElementKind.Background => string.IsNullOrWhiteSpace(selectedElement.AssetPath)
                ? $"{geometrySummary} Background fill is color-based."
                : $"{geometrySummary} Background asset: {selectedElement.AssetPath}.",
            PanelElementKind.Lamp => selectedElement.DisplayNumber.HasValue
                ? $"{geometrySummary} Lamp number: {selectedElement.DisplayNumber.Value}."
                : geometrySummary,
            PanelElementKind.Reel => BuildReelSummary(geometrySummary, selectedElement),
            PanelElementKind.SevenSegment => selectedElement.DisplayNumber.HasValue
                ? $"{geometrySummary} Display number: {selectedElement.DisplayNumber.Value}."
                : geometrySummary,
            PanelElementKind.Alpha => selectedElement.IsReversed == true
                ? $"{geometrySummary} Alpha mode: reversed."
                : geometrySummary,
            PanelElementKind.VfdDotMatrix => $"{geometrySummary} VFD dot matrix: 96 x 8 dots, 16 cells.",
            _ => geometrySummary
        };
    }

    private static string BuildReelSummary(string geometrySummary, PanelElementModel selectedElement)
    {
        if (selectedElement.DisplayNumber.HasValue && selectedElement.Stops.HasValue)
        {
            return $"{geometrySummary} Reel number: {selectedElement.DisplayNumber.Value}, stops: {selectedElement.Stops.Value}.";
        }

        if (selectedElement.DisplayNumber.HasValue)
        {
            return $"{geometrySummary} Reel number: {selectedElement.DisplayNumber.Value}.";
        }

        if (selectedElement.Stops.HasValue)
        {
            return $"{geometrySummary} Reel stops: {selectedElement.Stops.Value}.";
        }

        return geometrySummary;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
