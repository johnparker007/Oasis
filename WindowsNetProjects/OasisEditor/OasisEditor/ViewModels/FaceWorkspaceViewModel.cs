using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;

namespace OasisEditor;

public enum FaceWorkspaceDestination
{
    Overview,
    Artwork,
    ArtworkGeometry,
    ArtworkCalibration,
    Components,
    ComponentsEditor,
    Illumination,
    IlluminationLamps,
    FaceEditor
}

public sealed record FaceWorkspaceBreadcrumb(string Label, ICommand? Command);

/// <summary>Document-local, non-persisted navigation and read-only presentation for a Face.</summary>
public sealed class FaceWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _document;
    private FaceWorkspaceDestination _destination = FaceWorkspaceDestination.Overview;
    private FaceComponentKind? _componentPlacementKind;

    public FaceWorkspaceViewModel(DocumentTabViewModel document)
    {
        _document = document;
        NavigateToOverviewCommand = Command(FaceWorkspaceDestination.Overview);
        NavigateToArtworkCommand = Command(FaceWorkspaceDestination.Artwork);
        NavigateToArtworkGeometryCommand = Command(FaceWorkspaceDestination.ArtworkGeometry);
        NavigateToArtworkCalibrationCommand = Command(FaceWorkspaceDestination.ArtworkCalibration);
        NavigateToComponentsCommand = Command(FaceWorkspaceDestination.Components);
        NavigateToComponentsEditorCommand = Command(FaceWorkspaceDestination.ComponentsEditor);
        NavigateToIlluminationCommand = Command(FaceWorkspaceDestination.Illumination);
        NavigateToIlluminationLampsCommand = Command(FaceWorkspaceDestination.IlluminationLamps);
        NavigateToFaceEditorCommand = Command(FaceWorkspaceDestination.FaceEditor);
        UseImageCommand = new RelayCommand(ChooseImage);
        UsePanel2DSourceCommand = new RelayCommand(UsePanel2DSource, () => CanUsePanel2DSource);
        ReloadImageCommand = new RelayCommand(() => { _document.ReloadArtworkImage(); RefreshSummaries(); });
        ResetRegistrationCommand = new RelayCommand(ResetRegistration);
        BuildFaceCommand = new RelayCommand(() => RunBuild(false));
        RebuildFaceCommand = new RelayCommand(() => RunBuild(true));
        AddReelCommand = Placement(FaceComponentKind.Reel);
        AddButtonCommand = Placement(FaceComponentKind.Button);
        AddSevenSegmentCommand = Placement(FaceComponentKind.SevenSegmentDisplay);
        AddAlphaDisplayCommand = Placement(FaceComponentKind.AlphaDisplay);
        RebuildComponentsFromSourceCommand = new RelayCommand(RebuildComponentsFromSource,()=>CanRebuildComponentsFromSource);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FaceWorkspaceDestination Destination => _destination;
    public string FaceName => _document.Document.Title;
    public string DestinationName => _destination switch
    {
        FaceWorkspaceDestination.ArtworkGeometry => "Geometry",
        FaceWorkspaceDestination.ArtworkCalibration => "Calibration",
        FaceWorkspaceDestination.ComponentsEditor => "Edit",
        FaceWorkspaceDestination.IlluminationLamps => "Lamps",
        FaceWorkspaceDestination.FaceEditor => "Face Editor",
        _ => _destination.ToString()
    };
    public IReadOnlyList<FaceWorkspaceBreadcrumb> Breadcrumbs => BuildBreadcrumbs();
    public bool IsViewportDestination => _destination is FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkCalibration
        or FaceWorkspaceDestination.ComponentsEditor or FaceWorkspaceDestination.IlluminationLamps
        or FaceWorkspaceDestination.FaceEditor;
    public ICommand NavigateToOverviewCommand { get; }
    public ICommand NavigateToArtworkCommand { get; }
    public ICommand NavigateToArtworkGeometryCommand { get; }
    public ICommand NavigateToArtworkCalibrationCommand { get; }
    public ICommand NavigateToComponentsCommand { get; }
    public ICommand NavigateToComponentsEditorCommand { get; }
    public ICommand NavigateToIlluminationCommand { get; }
    public ICommand NavigateToIlluminationLampsCommand { get; }
    public ICommand NavigateToFaceEditorCommand { get; }
    public ICommand UseImageCommand { get; }
    public ICommand UsePanel2DSourceCommand { get; }
    public ICommand ReloadImageCommand { get; }
    public ICommand ResetRegistrationCommand { get; }
    public ICommand BuildFaceCommand { get; }
    public ICommand RebuildFaceCommand { get; }
    public ICommand AddReelCommand { get; }
    public ICommand AddButtonCommand { get; }
    public ICommand AddSevenSegmentCommand { get; }
    public ICommand AddAlphaDisplayCommand { get; }
    public ICommand RebuildComponentsFromSourceCommand { get; }
    public bool CanRebuildComponentsFromSource => _document.GetFaceDocument().Provenance.Components.Origin==FaceSubsystemOrigin.Derived && _document.TryConvertComponentsFromSource(out _,out _);
    public FaceComponentKind? ComponentPlacementKind => _componentPlacementKind;
    public bool IsComponentPlacementActive => _componentPlacementKind.HasValue;
    public string ComponentEditorStatus => _componentPlacementKind is { } kind ? $"Place {DisplayComponentKind(kind)}: click and drag bounds; Escape cancels." : "Select components in the viewport or Hierarchy, or choose an Add tool.";

    public string BuildStatusSummary
    {
        get
        {
            var states = _document.GetFaceDocument().BuildState.Products.Values;
            var errors = states.Count(state => state.Status == FaceBuildStatus.Error);
            var stale = states.Count(state => state.Status == FaceBuildStatus.Stale);
            if (errors > 0) return $"Build status: {errors} output{(errors == 1 ? "" : "s")} failed";
            return stale > 0 ? $"Build status: {stale} output{(stale == 1 ? " needs" : "s need")} building" : "Build status: Current";
        }
    }

    public string ArtworkBuildSummary => $"{FormatProvenance(_document.GetFaceDocument().Provenance.Artwork)} • Base: {Status(FaceGeneratedProduct.BaseArtwork)} • Output: {Status(FaceGeneratedProduct.ArtworkOutput)}";
    public string ComponentsProvenanceSummary => FormatProvenance(_document.GetFaceDocument().Provenance.Components);
    public string IlluminationBuildSummary => $"{FormatProvenance(_document.GetFaceDocument().Provenance.Illumination)} • Mask: {Status(FaceGeneratedProduct.LampMask)} • Trays: {Status(FaceGeneratedProduct.Trays)} • Runtime: {Status(FaceGeneratedProduct.RuntimeAssets)}";
    public string BuildErrorSummary => string.Join(Environment.NewLine,
        _document.GetFaceDocument().BuildState.Products
            .Where(pair => pair.Value.Status == FaceBuildStatus.Error && !string.IsNullOrWhiteSpace(pair.Value.ErrorMessage))
            .Select(pair => $"{DisplayName(pair.Key)}: {pair.Value.ErrorMessage}"));

    private void ChooseImage()
    {
        var dialog=new OpenFileDialog { Title=IsImageArtworkSource ? "Replace Face artwork image" : "Use image for Face artwork",
            Filter="Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*", CheckFileExists=true };
        if(dialog.ShowDialog()!=true)return;
        if(!_document.ImportArtworkImage(dialog.FileName,out var error)) MessageBox.Show(error ?? "The image could not be imported.", "Artwork Source", MessageBoxButton.OK, MessageBoxImage.Error);
        RefreshSummaries();
    }

    private void UsePanel2DSource()
    {
        if (!_document.UsePanel2DArtworkSource(out var error))
            MessageBox.Show(error ?? "The Panel2D artwork source is unavailable.", "Artwork Source",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        RefreshSummaries();
    }

    private ICommand Command(FaceWorkspaceDestination destination) => new RelayCommand(() => NavigateTo(destination));
    private ICommand Placement(FaceComponentKind kind) => new RelayCommand(() =>
    { NavigateTo(FaceWorkspaceDestination.ComponentsEditor); _componentPlacementKind=kind; Raise(nameof(ComponentPlacementKind)); Raise(nameof(IsComponentPlacementActive)); Raise(nameof(ComponentEditorStatus)); });

    public void CancelComponentPlacement()
    { if(!_componentPlacementKind.HasValue)return; _componentPlacementKind=null; Raise(nameof(ComponentPlacementKind)); Raise(nameof(IsComponentPlacementActive)); Raise(nameof(ComponentEditorStatus)); }

    public void CompleteComponentPlacement(double x,double y,double width,double height)
    {
        if(_componentPlacementKind is not { } kind)return;
        var element=FaceComponentFactory.Create(kind,x,y,width > 0 ? width : null,height > 0 ? height : null);
        _document.CommandService.Execute(FaceMutationCommands.CreateAddComponentCommand(_document.DocumentId,_document,element));
        CancelComponentPlacement();
    }

    private static string DisplayComponentKind(FaceComponentKind kind)=>kind switch { FaceComponentKind.SevenSegmentDisplay=>"Seven-Segment Display", FaceComponentKind.AlphaDisplay=>"Alpha Display", _=>kind.ToString() };
    private void RebuildComponentsFromSource()
    {
        if(!_document.TryConvertComponentsFromSource(out var components,out var error)){MessageBox.Show(error,"Rebuild Components",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var warning="Rebuilding Components from the source Panel2D will replace local component edits on this Face. Artwork and Illumination will not be changed.";
        if(MessageBox.Show(warning,"Rebuild Components From Source",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
        var source=_document.GetFaceDocument().Provenance.Components.SourceDocumentPath??_document.GetFaceDocument().SourcePanel2DDocumentPath??string.Empty;
        _document.CommandService.Execute(FaceMutationCommands.CreateRebuildComponentsCommand(_document.DocumentId,_document,components,source)); RefreshSummaries();
    }

    private void RunBuild(bool force)
    {
        _document.BuildFace(force);
        RefreshSummaries();
    }

    private string Status(FaceGeneratedProduct product) => _document.GetFaceDocument().BuildState.Get(product).Status.ToString();
    private static string FormatProvenance(FaceSubsystemProvenanceModel value)
    {
        if(value.Origin==FaceSubsystemOrigin.Authored)return "AUTHORED";
        var source=string.IsNullOrWhiteSpace(value.SourceDocumentPath)?string.Empty:$" from {Path.GetFileName(value.SourceDocumentPath)}";
        return value.IsLocallyModified?$"DERIVED{source} · LOCALLY MODIFIED":$"DERIVED{source}";
    }
    private static string DisplayName(FaceGeneratedProduct product) => product switch
    {
        FaceGeneratedProduct.BaseArtwork => "Base Artwork",
        FaceGeneratedProduct.ArtworkOutput => "Artwork Output",
        FaceGeneratedProduct.LampMask => "Lamp Mask",
        FaceGeneratedProduct.RuntimeAssets => "Runtime Assets",
        _ => product.ToString()
    };

    public string ArtworkSourceSummary
    {
        get
        {
            var artwork = _document.GetFaceDocument().Artwork;
            if (artwork is null) return "No authored artwork information";
            var source = artwork.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape ? "Panel2D / Face Source Shape" : "Image";
            var path = artwork.Source.Panel2DDocumentPath ?? artwork.Source.AssetPath;
            var dimensions = artwork.Source.PixelWidth > 0 ? $" • {artwork.Source.PixelWidth} × {artwork.Source.PixelHeight}" : string.Empty;
            return string.IsNullOrWhiteSpace(path) ? source : $"{source}: {path}{dimensions}";
        }
    }


    public bool IsImageArtworkSource => _document.GetFaceDocument().Artwork?.Source.Kind == FaceArtworkSourceKind.Image;
    public bool IsPanel2DArtworkSource => _document.GetFaceDocument().Artwork?.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape;
    public bool CanUsePanel2DSource => _document.CanUsePanel2DArtworkSource(out _);
    public string Panel2DSourceAvailability => _document.CanUsePanel2DArtworkSource(out var reason)
        ? string.Empty
        : IsImageArtworkSource ? reason ?? "The retained Panel2D source is unavailable." : string.Empty;
    public string? ArtworkRawImagePath => _document.GetArtworkSourceAbsolutePath();
    public int ArtworkSourcePixelWidth => _document.GetFaceDocument().Artwork?.Source.PixelWidth ?? 0;
    public int ArtworkSourcePixelHeight => _document.GetFaceDocument().Artwork?.Source.PixelHeight ?? 0;
    public FacePerspectiveRegistrationModel ArtworkRegistration => _document.GetFaceDocument().Artwork?.Geometry.PerspectiveRegistration ?? FacePerspectiveRegistrationModel.FullImage;
    public void CommitRegistration(FacePerspectiveRegistrationModel value) { _document.SetArtworkRegistration(value); RefreshSummaries(); }
    public void ResetRegistration() { _document.SetArtworkRegistration(FacePerspectiveRegistrationModel.FullImage, "Reset artwork registration"); RefreshSummaries(); }

    public string ArtworkGeometrySummary => IsImageArtworkSource
        ? $"Perspective registration • {_document.GetFaceDocument().Artwork?.OutputWidth} × {_document.GetFaceDocument().Artwork?.OutputHeight}"
        : $"Derived from Face Source Shape • {_document.GetFaceDocument().Artwork?.OutputWidth} × {_document.GetFaceDocument().Artwork?.OutputHeight}";
    public string ArtworkCorrectionSummary
    {
        get
        {
            var settings = _document.GetFaceDocument().GenerationSettings;
            return $"{ArtworkCalibrationSummary} • Sharpening {(settings.PostWarpSharpeningEnabled ? $"enabled ({settings.PostWarpSharpeningAmount:0.##})" : "disabled")}";
        }
    }
    public string ArtworkBaseSummary
    {
        get
        {
            var artwork = _document.GetFaceDocument().Artwork;
            return artwork is null ? "Not configured" : $"Generated • {artwork.OutputWidth} × {artwork.OutputHeight} • {Status(FaceGeneratedProduct.BaseArtwork)} • {artwork.BaseAssetPath}";
        }
    }
    public string ArtworkOutputSummary
    {
        get
        {
            var artwork = _document.GetFaceDocument().Artwork;
            if (artwork is null) return "Generated output not configured";
            var dimensions = artwork.OutputWidth > 0 && artwork.OutputHeight > 0 ? $"{artwork.OutputWidth} × {artwork.OutputHeight}" : "dimensions unavailable";
            var output = string.IsNullOrWhiteSpace(artwork.OutputAssetPath) ? "output path unavailable" : artwork.OutputAssetPath;
            return $"Generated • {dimensions} • {Status(FaceGeneratedProduct.ArtworkOutput)} • {output}";
        }
    }

    public string ArtworkCalibrationSummary
    {
        get
        {
            var calibration = _document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations.OfType<ArtworkCalibrationOperationModel>().FirstOrDefault();
            if (calibration is null) return "Artwork Calibration not configured";
            var samples = calibration.BlackReference.Samples.Count + calibration.WhiteReference.Samples.Count + calibration.SameColorGroups.Sum(group => group.Samples.Count);
            return $"Artwork Calibration {(calibration.Enabled ? "enabled" : "disabled")} • {samples} samples • {calibration.SameColorGroups.Count} colour groups";
        }
    }

    public string ComponentsSummary
    {
        get
        {
            var elements = _document.GetFaceDocument().Elements.Where(FaceElementClassification.IsComponent).ToArray();
            if(elements.Length==0)return "No components";
            var counts=new[]
            {
                (elements.OfType<FaceReelDisplayElement>().Count(),"Reel"),
                (elements.OfType<FaceButtonElement>().Count(),"Button"),
                (elements.OfType<FaceSevenSegmentDisplayElement>().Count(),"Seven-Segment Display"),
                (elements.OfType<FaceAlphaDisplayElement>().Count(),"Alpha Display")
            };
            return string.Join(" • ",counts.Where(item=>item.Item1>0).Select(item=>$"{item.Item1} {item.Item2}{(item.Item1==1?string.Empty:"s")}"));
        }
    }

    public string IlluminationSummary
    {
        get
        {
            var face = _document.GetFaceDocument();
            var lamps = face.Elements.OfType<FaceLampWindowElement>().Count();
            var mask = face.MaskLayer is null ? "no mask" : $"mask {face.MaskLayer.Width} × {face.MaskLayer.Height}";
            return $"{lamps} lamps • {mask} • {face.Trays.Count} trays • {face.LampEmitters.Count} emitters";
        }
    }

    public void NavigateTo(FaceWorkspaceDestination destination)
    {
        if (_destination == destination) return;
        if (destination != FaceWorkspaceDestination.ArtworkCalibration) _document.CancelCalibrationPlacement();
        if (destination != FaceWorkspaceDestination.ComponentsEditor) CancelComponentPlacement();
        _destination = destination;
        Raise(nameof(Destination));
        Raise(nameof(DestinationName));
        Raise(nameof(Breadcrumbs));
        Raise(nameof(IsViewportDestination));
    }

    private IReadOnlyList<FaceWorkspaceBreadcrumb> BuildBreadcrumbs()
    {
        var root = new FaceWorkspaceBreadcrumb(FaceName, _destination == FaceWorkspaceDestination.Overview ? null : NavigateToOverviewCommand);
        return _destination switch
        {
            FaceWorkspaceDestination.Overview => [root],
            FaceWorkspaceDestination.Artwork => [root, new("Artwork", null)],
            FaceWorkspaceDestination.ArtworkGeometry => [root, new("Artwork", NavigateToArtworkCommand), new("Geometry", null)],
            FaceWorkspaceDestination.ArtworkCalibration => [root, new("Artwork", NavigateToArtworkCommand), new("Calibration", null)],
            FaceWorkspaceDestination.Components => [root, new("Components", null)],
            FaceWorkspaceDestination.ComponentsEditor => [root, new("Components", NavigateToComponentsCommand), new("Edit", null)],
            FaceWorkspaceDestination.Illumination => [root, new("Illumination", null)],
            FaceWorkspaceDestination.IlluminationLamps => [root, new("Illumination", NavigateToIlluminationCommand), new("Lamps", null)],
            _ => [root, new("Face Editor", null)]
        };
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    internal void RefreshSummaries()
    {
        Raise(nameof(ArtworkSourceSummary)); Raise(nameof(IsImageArtworkSource)); Raise(nameof(IsPanel2DArtworkSource)); Raise(nameof(CanUsePanel2DSource)); Raise(nameof(Panel2DSourceAvailability)); Raise(nameof(ArtworkRawImagePath)); Raise(nameof(ArtworkSourcePixelWidth)); Raise(nameof(ArtworkSourcePixelHeight)); Raise(nameof(ArtworkRegistration)); Raise(nameof(ArtworkGeometrySummary)); Raise(nameof(ArtworkOutputSummary)); Raise(nameof(ArtworkCalibrationSummary));
        if (UsePanel2DSourceCommand is RelayCommand usePanel2D) usePanel2D.RaiseCanExecuteChanged();
        if (RebuildComponentsFromSourceCommand is RelayCommand rebuildComponents) rebuildComponents.RaiseCanExecuteChanged();
        Raise(nameof(ComponentsSummary)); Raise(nameof(IlluminationSummary));
        Raise(nameof(BuildStatusSummary)); Raise(nameof(ArtworkBuildSummary));
        Raise(nameof(ComponentsProvenanceSummary)); Raise(nameof(IlluminationBuildSummary));
        Raise(nameof(BuildErrorSummary));
    }
}
