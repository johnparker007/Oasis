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
        ReloadImageCommand = new RelayCommand(() => { _document.ReloadArtworkImage(); RefreshSummaries(); });
        ResetRegistrationCommand = new RelayCommand(ResetRegistration);
        BuildFaceCommand = new RelayCommand(() => RunBuild(false));
        RebuildFaceCommand = new RelayCommand(() => RunBuild(true));
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
    public ICommand ReloadImageCommand { get; }
    public ICommand ResetRegistrationCommand { get; }
    public ICommand BuildFaceCommand { get; }
    public ICommand RebuildFaceCommand { get; }

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

    private ICommand Command(FaceWorkspaceDestination destination) => new RelayCommand(() => NavigateTo(destination));

    private void RunBuild(bool force)
    {
        _document.BuildFace(force);
        RefreshSummaries();
    }

    private string Status(FaceGeneratedProduct product) => _document.GetFaceDocument().BuildState.Get(product).Status.ToString();
    private static string FormatProvenance(FaceSubsystemProvenanceModel value) =>
        value.IsLocallyModified ? $"{value.Origin} • locally modified" : value.Origin.ToString();
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
    public string? ArtworkRawImagePath => _document.GetArtworkSourceAbsolutePath();
    public int ArtworkSourcePixelWidth => _document.GetFaceDocument().Artwork?.Source.PixelWidth ?? 0;
    public int ArtworkSourcePixelHeight => _document.GetFaceDocument().Artwork?.Source.PixelHeight ?? 0;
    public FacePerspectiveRegistrationModel ArtworkRegistration => _document.GetFaceDocument().Artwork?.Geometry.PerspectiveRegistration ?? FacePerspectiveRegistrationModel.FullImage;
    public void CommitRegistration(FacePerspectiveRegistrationModel value) { _document.SetArtworkRegistration(value); RefreshSummaries(); }
    public void ResetRegistration() { _document.SetArtworkRegistration(FacePerspectiveRegistrationModel.FullImage, "Reset artwork registration"); RefreshSummaries(); }

    public string ArtworkGeometrySummary => $"Perspective rectification • {_document.GetFaceDocument().Artwork?.OutputWidth} × {_document.GetFaceDocument().Artwork?.OutputHeight}";
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
            var elements = _document.GetFaceDocument().Elements;
            return $"{elements.OfType<FaceReelDisplayElement>().Count()} reels • {elements.OfType<FaceButtonElement>().Count()} buttons • " +
                   $"{elements.OfType<FaceSevenSegmentDisplayElement>().Count()} seven-segment • {elements.OfType<FaceAlphaDisplayElement>().Count()} alpha displays";
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
        Raise(nameof(ArtworkSourceSummary)); Raise(nameof(IsImageArtworkSource)); Raise(nameof(ArtworkRawImagePath)); Raise(nameof(ArtworkSourcePixelWidth)); Raise(nameof(ArtworkSourcePixelHeight)); Raise(nameof(ArtworkRegistration)); Raise(nameof(ArtworkOutputSummary)); Raise(nameof(ArtworkCalibrationSummary));
        Raise(nameof(ComponentsSummary)); Raise(nameof(IlluminationSummary));
        Raise(nameof(BuildStatusSummary)); Raise(nameof(ArtworkBuildSummary));
        Raise(nameof(ComponentsProvenanceSummary)); Raise(nameof(IlluminationBuildSummary));
        Raise(nameof(BuildErrorSummary));
    }
}
