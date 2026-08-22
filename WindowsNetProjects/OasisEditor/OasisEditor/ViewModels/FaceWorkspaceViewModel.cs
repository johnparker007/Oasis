using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace OasisEditor;

public enum FaceWorkspaceDestination
{
    Overview,
    Artwork,
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
        NavigateToArtworkCalibrationCommand = Command(FaceWorkspaceDestination.ArtworkCalibration);
        NavigateToComponentsCommand = Command(FaceWorkspaceDestination.Components);
        NavigateToComponentsEditorCommand = Command(FaceWorkspaceDestination.ComponentsEditor);
        NavigateToIlluminationCommand = Command(FaceWorkspaceDestination.Illumination);
        NavigateToIlluminationLampsCommand = Command(FaceWorkspaceDestination.IlluminationLamps);
        NavigateToFaceEditorCommand = Command(FaceWorkspaceDestination.FaceEditor);
        BuildFaceCommand = new RelayCommand(() => RunBuild(false));
        RebuildFaceCommand = new RelayCommand(() => RunBuild(true));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FaceWorkspaceDestination Destination => _destination;
    public string FaceName => _document.Document.Title;
    public string DestinationName => _destination switch
    {
        FaceWorkspaceDestination.ArtworkCalibration => "Calibration",
        FaceWorkspaceDestination.ComponentsEditor => "Edit",
        FaceWorkspaceDestination.IlluminationLamps => "Lamps",
        FaceWorkspaceDestination.FaceEditor => "Face Editor",
        _ => _destination.ToString()
    };
    public IReadOnlyList<FaceWorkspaceBreadcrumb> Breadcrumbs => BuildBreadcrumbs();
    public bool IsViewportDestination => _destination is FaceWorkspaceDestination.ArtworkCalibration
        or FaceWorkspaceDestination.ComponentsEditor or FaceWorkspaceDestination.IlluminationLamps
        or FaceWorkspaceDestination.FaceEditor;
    public ICommand NavigateToOverviewCommand { get; }
    public ICommand NavigateToArtworkCommand { get; }
    public ICommand NavigateToArtworkCalibrationCommand { get; }
    public ICommand NavigateToComponentsCommand { get; }
    public ICommand NavigateToComponentsEditorCommand { get; }
    public ICommand NavigateToIlluminationCommand { get; }
    public ICommand NavigateToIlluminationLampsCommand { get; }
    public ICommand NavigateToFaceEditorCommand { get; }
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

    public string ArtworkBuildSummary => $"{FormatProvenance(_document.GetFaceDocument().Provenance.Artwork)} • Output: {Status(FaceGeneratedProduct.ArtworkOutput)}";
    public string ComponentsProvenanceSummary => FormatProvenance(_document.GetFaceDocument().Provenance.Components);
    public string IlluminationBuildSummary => $"{FormatProvenance(_document.GetFaceDocument().Provenance.Illumination)} • Mask: {Status(FaceGeneratedProduct.LampMask)} • Trays: {Status(FaceGeneratedProduct.Trays)} • Runtime: {Status(FaceGeneratedProduct.RuntimeLighting)}";

    private ICommand Command(FaceWorkspaceDestination destination) => new RelayCommand(() => NavigateTo(destination));

    private void RunBuild(bool force)
    {
        _document.BuildFace(force);
        RefreshSummaries();
    }

    private string Status(FaceGeneratedProduct product) => _document.GetFaceDocument().BuildState.Get(product).Status.ToString();
    private static string FormatProvenance(FaceSubsystemProvenanceModel value) =>
        value.IsLocallyModified ? $"{value.Origin} • locally modified" : value.Origin.ToString();

    public string ArtworkSourceSummary
    {
        get
        {
            var artwork = _document.GetFaceDocument().Artwork;
            if (artwork is null) return "No authored artwork information";
            var source = artwork.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape ? "Panel2D / Face Source Shape" : "Image";
            var path = artwork.Source.Panel2DDocumentPath ?? artwork.Source.AssetPath;
            return string.IsNullOrWhiteSpace(path) ? source : $"{source}: {Path.GetFileName(path)}";
        }
    }

    public string ArtworkOutputSummary
    {
        get
        {
            var artwork = _document.GetFaceDocument().Artwork;
            if (artwork is null) return "Generated output not configured";
            var dimensions = artwork.OutputWidth > 0 && artwork.OutputHeight > 0 ? $"{artwork.OutputWidth} × {artwork.OutputHeight}" : "dimensions unavailable";
            var output = string.IsNullOrWhiteSpace(artwork.GeneratedAssetPath) ? "output path unavailable" : artwork.GeneratedAssetPath;
            return $"{dimensions} • {output}";
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
        Raise(nameof(ArtworkSourceSummary)); Raise(nameof(ArtworkOutputSummary)); Raise(nameof(ArtworkCalibrationSummary));
        Raise(nameof(ComponentsSummary)); Raise(nameof(IlluminationSummary));
        Raise(nameof(BuildStatusSummary)); Raise(nameof(ArtworkBuildSummary));
        Raise(nameof(ComponentsProvenanceSummary)); Raise(nameof(IlluminationBuildSummary));
    }
}
