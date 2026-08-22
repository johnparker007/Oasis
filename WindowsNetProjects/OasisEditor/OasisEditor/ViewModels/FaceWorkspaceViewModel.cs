using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace OasisEditor;

public enum FaceWorkspaceDestination
{
    Overview,
    Artwork,
    Components,
    Illumination,
    FaceEditor
}

/// <summary>Document-local, non-persisted navigation and read-only presentation for a Face.</summary>
public sealed class FaceWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly DocumentTabViewModel _document;
    private FaceWorkspaceDestination _destination = FaceWorkspaceDestination.Overview;

    public FaceWorkspaceViewModel(DocumentTabViewModel document)
    {
        _document = document;
        NavigateToOverviewCommand = new RelayCommand(() => NavigateTo(FaceWorkspaceDestination.Overview));
        NavigateToArtworkCommand = new RelayCommand(() => NavigateTo(FaceWorkspaceDestination.Artwork));
        NavigateToComponentsCommand = new RelayCommand(() => NavigateTo(FaceWorkspaceDestination.Components));
        NavigateToIlluminationCommand = new RelayCommand(() => NavigateTo(FaceWorkspaceDestination.Illumination));
        NavigateToFaceEditorCommand = new RelayCommand(() => NavigateTo(FaceWorkspaceDestination.FaceEditor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FaceWorkspaceDestination Destination => _destination;
    public string FaceName => _document.Document.Title;
    public string DestinationName => _destination == FaceWorkspaceDestination.FaceEditor ? "Face Editor" : _destination.ToString();
    public ICommand NavigateToOverviewCommand { get; }
    public ICommand NavigateToArtworkCommand { get; }
    public ICommand NavigateToComponentsCommand { get; }
    public ICommand NavigateToIlluminationCommand { get; }
    public ICommand NavigateToFaceEditorCommand { get; }

    public string ArtworkSourceSummary
    {
        get
        {
            var face = _document.GetFaceDocument();
            var artwork = face.Artwork;
            if (artwork is null) return "No authored artwork information";
            var source = artwork.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape
                ? "Panel2D / Face Source Shape"
                : "Image";
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
            var dimensions = artwork.OutputWidth > 0 && artwork.OutputHeight > 0
                ? $"{artwork.OutputWidth} × {artwork.OutputHeight}"
                : "dimensions unavailable";
            var output = string.IsNullOrWhiteSpace(artwork.GeneratedAssetPath)
                ? "output path unavailable"
                : artwork.GeneratedAssetPath;
            return $"{dimensions} • {output}";
        }
    }

    public string ArtworkCalibrationSummary
    {
        get
        {
            var calibration = _document.GetFaceDocument().Artwork?.ProcessingPipeline.Operations
                .OfType<ArtworkCalibrationOperationModel>().FirstOrDefault();
            return calibration is null ? "Artwork Calibration not configured" :
                calibration.Enabled ? "Artwork Calibration enabled" : "Artwork Calibration disabled";
        }
    }

    public string ComponentsSummary
    {
        get
        {
            var elements = _document.GetFaceDocument().Elements;
            return $"{elements.OfType<FaceReelDisplayElement>().Count()} reels • " +
                   $"{elements.OfType<FaceButtonElement>().Count()} buttons • " +
                   $"{elements.OfType<FaceSevenSegmentDisplayElement>().Count()} seven-segment • " +
                   $"{elements.OfType<FaceAlphaDisplayElement>().Count()} alpha displays";
        }
    }

    public string IlluminationSummary
    {
        get
        {
            var face = _document.GetFaceDocument();
            var lamps = face.Elements.OfType<FaceLampWindowElement>().Count();
            var mask = face.MaskLayer is null ? "no mask" :
                $"mask {face.MaskLayer.Width} × {face.MaskLayer.Height}";
            return $"{lamps} lamps • {mask} • {face.Trays.Count} trays • {face.LampEmitters.Count} emitters";
        }
    }

    public void NavigateTo(FaceWorkspaceDestination destination)
    {
        if (_destination == destination) return;
        _destination = destination;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Destination)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationName)));
    }

    internal void RefreshSummaries()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArtworkSourceSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArtworkOutputSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArtworkCalibrationSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComponentsSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IlluminationSummary)));
    }
}
