using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OasisEditor;

public sealed class InspectorViewModel : INotifyPropertyChanged
{
    private readonly Func<AssetBrowserItemViewModel?> _selectedAssetAccessor;
    private readonly Func<DocumentTabViewModel?> _selectedDocumentAccessor;
    private readonly Func<EditorProject?> _loadedProjectAccessor;
    private readonly ActiveDocumentContextService _activeDocumentContext;
    private readonly Func<DocumentTabViewModel, string, DocumentTabViewModel?> _applySummary;
    private readonly ObservableCollection<InspectorPropertyRowViewModel> _propertyRows = [];
    private string _inspectorEditableSummary = string.Empty;

    public InspectorViewModel(
        Func<AssetBrowserItemViewModel?> selectedAssetAccessor,
        Func<DocumentTabViewModel?> selectedDocumentAccessor,
        Func<EditorProject?> loadedProjectAccessor,
        ActiveDocumentContextService activeDocumentContext,
        Func<DocumentTabViewModel, string, DocumentTabViewModel?> applySummary)
    {
        _selectedAssetAccessor = selectedAssetAccessor;
        _selectedDocumentAccessor = selectedDocumentAccessor;
        _loadedProjectAccessor = loadedProjectAccessor;
        _activeDocumentContext = activeDocumentContext;
        _applySummary = applySummary;
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
                return $"Asset: {selectedAsset.DisplayPath}";
            }

            var selectedDocument = _selectedDocumentAccessor();
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
                return "Asset File";
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
            var selectedDocument = _selectedDocumentAccessor();
            if (selectedDocument is not null)
            {
                if (_activeDocumentContext.ActivePanelSelection is PanelSelectionInfo panelSelection)
                {
                    if (selectedDocument.TryGetPanelElement(panelSelection, out var selectedElement))
                    {
                        return BuildSelectedElementSummary(selectedElement);
                    }

                    return $"Selected {panelSelection.Kind} at ({panelSelection.X:0.##}, {panelSelection.Y:0.##}) sized {panelSelection.Width:0.##} x {panelSelection.Height:0.##}.";
                }

                return selectedDocument.ContentSummary;
            }

            var selectedAsset = _selectedAssetAccessor();
            if (selectedAsset is not null)
            {
                return "Use this panel as the starting point for future property editing.";
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
            var selectedDocument = _selectedDocumentAccessor();
            return selectedDocument is not null
                && selectedDocument.Document.DocumentType != EditorDocumentType.ProjectOverview;
        }
    }

    public void NotifyContextChanged()
    {
        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(InspectorType));
        OnPropertyChanged(nameof(InspectorPath));
        OnPropertyChanged(nameof(InspectorSummary));
        OnPropertyChanged(nameof(CanEditInspectorSummary));

        RebuildPropertyRows();

        InspectorEditableSummary = _selectedDocumentAccessor()?.ContentSummary ?? string.Empty;
        NotifyInspectorEditCommand();
    }

    private void RebuildPropertyRows()
    {
        _propertyRows.Clear();

        var selectedDocument = _selectedDocumentAccessor();
        var selection = _activeDocumentContext.ActivePanelSelection;
        if (selectedDocument is null || selection is not PanelSelectionInfo selectedSelection || !selectedDocument.TryGetPanelElement(selectedSelection, out var selectedElement))
        {
            OnPropertyChanged(nameof(InspectorPropertyRows));
            return;
        }

        _propertyRows.Add(new InspectorTextPropertyViewModel("Name", "Common", selectedElement.Name));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Object ID", "Metadata", selectedElement.ObjectId));
        _propertyRows.Add(new InspectorInfoPropertyViewModel("Kind", "Metadata", selectedElement.Kind.ToString()));
        _propertyRows.Add(new InspectorDoublePropertyViewModel("X", "Transform", selectedElement.X));
        _propertyRows.Add(new InspectorDoublePropertyViewModel("Y", "Transform", selectedElement.Y));
        _propertyRows.Add(new InspectorDoublePropertyViewModel("Width", "Transform", selectedElement.Width));
        _propertyRows.Add(new InspectorDoublePropertyViewModel("Height", "Transform", selectedElement.Height));
        _propertyRows.Add(new InspectorBoolPropertyViewModel("Locked", "Common", selectedElement.IsLocked));
        _propertyRows.Add(new InspectorBoolPropertyViewModel("Visible", "Common", selectedElement.IsVisible));

        AddTypeSpecificRows(selectedElement);

        OnPropertyChanged(nameof(InspectorPropertyRows));
    }

    private void AddTypeSpecificRows(PanelElementModel selectedElement)
    {
        if (selectedElement.DisplayNumber.HasValue)
        {
            _propertyRows.Add(new InspectorIntPropertyViewModel("Display Number", "Type-specific", selectedElement.DisplayNumber));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.AssetPath))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("Asset Path", "Type-specific", selectedElement.AssetPath));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.SecondaryAssetPath))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("Secondary Asset", "Type-specific", selectedElement.SecondaryAssetPath));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.OnColorHex))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("On Color", "Type-specific", selectedElement.OnColorHex));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.OffColorHex))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("Off Color", "Type-specific", selectedElement.OffColorHex));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.TextColorHex))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("Text Color", "Type-specific", selectedElement.TextColorHex));
        }

        if (!string.IsNullOrWhiteSpace(selectedElement.DisplayText))
        {
            _propertyRows.Add(new InspectorTextPropertyViewModel("Display Text", "Type-specific", selectedElement.DisplayText));
        }

        if (selectedElement.Stops.HasValue)
        {
            _propertyRows.Add(new InspectorIntPropertyViewModel("Stops", "Type-specific", selectedElement.Stops));
        }

        if (selectedElement.VisibleScale.HasValue)
        {
            _propertyRows.Add(new InspectorDoublePropertyViewModel("Visible Scale", "Type-specific", selectedElement.VisibleScale.Value));
        }

        if (selectedElement.IsReversed.HasValue)
        {
            _propertyRows.Add(new InspectorBoolPropertyViewModel("Reversed", "Type-specific", selectedElement.IsReversed.Value));
        }

        if (selectedElement.ImportSource is not null)
        {
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Import Format", "Metadata", selectedElement.ImportSource.Format));
            _propertyRows.Add(new InspectorInfoPropertyViewModel("Import Reference", "Metadata", selectedElement.ImportSource.Reference));
        }
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
