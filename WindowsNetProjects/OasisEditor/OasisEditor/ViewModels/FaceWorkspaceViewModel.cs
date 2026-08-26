using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Imaging;
using OasisEditor.Progress;

namespace OasisEditor;

public enum FaceWorkspaceDestination
{
    Overview,
    Artwork,
    ArtworkGeometry,
    ArtworkOverrideGeometry,
    ArtworkCalibration,
    ArtworkOverride,
    Components,
    ComponentsEditor,
    Illumination,
    IlluminationLamps,
    LayoutView
}

public sealed record FaceWorkspaceBreadcrumb(string Label, ICommand? Command);

/// <summary>Document-local, non-persisted navigation and read-only presentation for a Face.</summary>
public sealed class FaceWorkspaceViewModel : INotifyPropertyChanged
{
    internal const int GeometryPreviewMaximumDecodeDimension = 2560;
    private readonly DocumentTabViewModel _document;
    private FaceWorkspaceDestination _destination = FaceWorkspaceDestination.Overview;
    private FaceComponentKind? _componentPlacementKind;
    private bool _isLampPlacementActive;
    private double _overridePreviewOpacity = .5d;
    private BitmapImage? _basePreview;
    private BitmapImage? _overridePreview;
    private string? _basePreviewPath;
    private string? _overridePreviewKey;
    private string? _overridePreviewInputKey;
    private CancellationTokenSource? _overridePreviewCancellation;
    private long _overridePreviewGeneration;
    private readonly SemaphoreSlim _overridePreviewGate = new(1,1);
    private BitmapImage? _geometryRawImage;
    private string? _geometryRawImageKey;
    private CancellationTokenSource? _geometryRawImageCancellation;
    private long _geometryRawImageGeneration;
    private bool _isBuildInProgress;
    private bool _isOverridePreviewLoading;

    public FaceWorkspaceViewModel(DocumentTabViewModel document)
    {
        _document = document;
        NavigateToOverviewCommand = Command(FaceWorkspaceDestination.Overview);
        NavigateToArtworkCommand = Command(FaceWorkspaceDestination.Artwork);
        NavigateToArtworkGeometryCommand = Command(FaceWorkspaceDestination.ArtworkGeometry);
        NavigateToArtworkCalibrationCommand = Command(FaceWorkspaceDestination.ArtworkCalibration);
        NavigateToArtworkOverrideCommand = Command(FaceWorkspaceDestination.ArtworkOverride);
        NavigateToArtworkOverrideGeometryCommand = Command(FaceWorkspaceDestination.ArtworkOverrideGeometry);
        NavigateToComponentsCommand = Command(FaceWorkspaceDestination.Components);
        NavigateToComponentsEditorCommand = Command(FaceWorkspaceDestination.ComponentsEditor);
        NavigateToIlluminationCommand = Command(FaceWorkspaceDestination.Illumination);
        NavigateToIlluminationLampsCommand = Command(FaceWorkspaceDestination.IlluminationLamps);
        NavigateToLayoutViewCommand = Command(FaceWorkspaceDestination.LayoutView);
        UseImageCommand = new RelayCommand(ChooseImage);
        UsePanel2DSourceCommand = new RelayCommand(UsePanel2DSource, () => CanUsePanel2DSource);
        ReloadImageCommand = new RelayCommand(() => { _document.ReloadArtworkImage(); InvalidateGeometryRawImage(); RefreshSummaries(); });
        ResetRegistrationCommand = new RelayCommand(ResetRegistration);
        BuildFaceCommand = new RelayCommand(() => RunBuild(false), () => !_isBuildInProgress);
        RebuildFaceCommand = new RelayCommand(() => RunBuild(true), () => !_isBuildInProgress);
        OpenGenerationSettingsCommand = new RelayCommand(OpenGenerationSettings);
        AddReelCommand = Placement(FaceComponentKind.Reel);
        AddButtonCommand = Placement(FaceComponentKind.Button);
        AddSevenSegmentCommand = Placement(FaceComponentKind.SevenSegmentDisplay);
        AddAlphaDisplayCommand = Placement(FaceComponentKind.AlphaDisplay);
        RebuildComponentsFromSourceCommand = new RelayCommand(RebuildComponentsFromSource,()=>CanRebuildComponentsFromSource);
        UseAuthoredMaskCommand = new RelayCommand(ChooseLampMask);
        AddLampCommand = new RelayCommand(() => { NavigateTo(FaceWorkspaceDestination.IlluminationLamps); _isLampPlacementActive=true; Raise(nameof(IsLampPlacementActive)); Raise(nameof(LampEditorStatus)); });
        CreateOverrideFromBaseCommand=new RelayCommand(CreateOverrideFromBase);
        ImportOverrideCommand=new RelayCommand(()=>ChooseOverride(false)); ReplaceOverrideCommand=new RelayCommand(()=>ChooseOverride(true));
        ReloadOverrideCommand=new RelayCommand(()=>{if(!_document.ReloadArtworkOverride(out var error))ShowOverrideError(error);RefreshSummaries();});
        ToggleOverrideCommand=new RelayCommand(ToggleOverride); RemoveOverrideCommand=new RelayCommand(()=>{_document.SetArtworkOverride(null,"Remove Artwork Override");RefreshSummaries();});
        ResetOverrideAlignmentCommand=new RelayCommand(()=>CommitOverrideAlignment(0,0,1,1,"Reset Artwork Override alignment"));
        DoneOverrideAlignmentCommand=NavigateToArtworkCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FaceWorkspaceDestination Destination => _destination;
    public string FaceName => _document.Document.Title;
    public string DestinationName => _destination switch
    {
        FaceWorkspaceDestination.ArtworkGeometry => "Geometry",
        FaceWorkspaceDestination.ArtworkCalibration => "Calibration",
        FaceWorkspaceDestination.ArtworkOverride => "Override",
        FaceWorkspaceDestination.ArtworkOverrideGeometry => "Geometry",
        FaceWorkspaceDestination.ComponentsEditor => "Edit",
        FaceWorkspaceDestination.IlluminationLamps => "Lamps",
        FaceWorkspaceDestination.LayoutView => "Layout View",
        _ => _destination.ToString()
    };
    public IReadOnlyList<FaceWorkspaceBreadcrumb> Breadcrumbs => BuildBreadcrumbs();
    public bool IsViewportDestination => _destination is FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry or FaceWorkspaceDestination.ArtworkCalibration or FaceWorkspaceDestination.ArtworkOverride
        or FaceWorkspaceDestination.ComponentsEditor or FaceWorkspaceDestination.IlluminationLamps
        or FaceWorkspaceDestination.LayoutView;
    public ICommand NavigateToOverviewCommand { get; }
    public ICommand NavigateToArtworkCommand { get; }
    public ICommand NavigateToArtworkGeometryCommand { get; }
    public ICommand NavigateToArtworkCalibrationCommand { get; }
    public ICommand NavigateToArtworkOverrideCommand { get; }
    public ICommand NavigateToArtworkOverrideGeometryCommand { get; }
    public ICommand NavigateToComponentsCommand { get; }
    public ICommand NavigateToComponentsEditorCommand { get; }
    public ICommand NavigateToIlluminationCommand { get; }
    public ICommand NavigateToIlluminationLampsCommand { get; }
    public ICommand NavigateToLayoutViewCommand { get; }
    public ICommand UseImageCommand { get; }
    public ICommand UsePanel2DSourceCommand { get; }
    public ICommand ReloadImageCommand { get; }
    public ICommand ResetRegistrationCommand { get; }
    public ICommand BuildFaceCommand { get; }
    public ICommand RebuildFaceCommand { get; }
    public ICommand OpenGenerationSettingsCommand { get; }
    public ICommand AddReelCommand { get; }
    public ICommand AddButtonCommand { get; }
    public ICommand AddSevenSegmentCommand { get; }
    public ICommand AddAlphaDisplayCommand { get; }
    public ICommand RebuildComponentsFromSourceCommand { get; }
    public ICommand AddLampCommand { get; }
    public ICommand UseAuthoredMaskCommand { get; }
    public ICommand CreateOverrideFromBaseCommand { get; }
    public ICommand ImportOverrideCommand { get; }
    public ICommand ReplaceOverrideCommand { get; }
    public ICommand ReloadOverrideCommand { get; }
    public ICommand ToggleOverrideCommand { get; }
    public ICommand RemoveOverrideCommand { get; }
    public ICommand ResetOverrideAlignmentCommand { get; }
    public ICommand DoneOverrideAlignmentCommand { get; }
    public ICommand DoneGeometryCommand => IsEditingOverrideGeometry ? NavigateToArtworkOverrideCommand : NavigateToArtworkCommand;
    public double OverridePreviewOpacity { get=>_overridePreviewOpacity; set { _overridePreviewOpacity=Math.Clamp(value,0d,1d);Raise(nameof(OverridePreviewOpacity)); } }
    public bool IsLampPlacementActive => _isLampPlacementActive;
    public string LampEditorStatus => _isLampPlacementActive ? "Place Lamp: click and drag bounds; Escape cancels." : "Select lamps in the viewport or Hierarchy, or choose Add Lamp.";
    public bool CanRebuildComponentsFromSource => _document.GetFaceDocument().Provenance.Components.Origin==FaceSubsystemOrigin.Derived && _document.CanAttemptSourcePanel();
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
            if (stale > 0) return $"Build status: {stale} output{(stale == 1 ? " needs" : "s need")} building";
            return states.All(state => state.Status == FaceBuildStatus.NotConfigured) ? "Build status: Not configured" : "Build status: Current";
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

    private void ChooseLampMask()
    {
        var dialog=new OpenFileDialog{Title="Use authored lamp-mask image",Filter="Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*",CheckFileExists=true};
        if(dialog.ShowDialog()!=true)return;
        if(!_document.ImportLampMaskImage(dialog.FileName,out var error))MessageBox.Show(error??"The lamp mask could not be imported.","Lamp Mask",MessageBoxButton.OK,MessageBoxImage.Error);
        RefreshSummaries();
    }

    private void CreateOverrideFromBase(){if(!_document.CreateArtworkOverrideFromBase(out var error))ShowOverrideError(error);else NavigateTo(FaceWorkspaceDestination.ArtworkOverride);RefreshSummaries();}
    private void ChooseOverride(bool replace){var dialog=new OpenFileDialog{Title=replace?"Replace Artwork Override":"Import Artwork Override",Filter="Image files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*",CheckFileExists=true};if(dialog.ShowDialog()!=true)return;if(!_document.ImportArtworkOverride(dialog.FileName,replace,out var error)){ShowOverrideError(error);return;}NavigateToImportedOverrideGeometry();}
    internal void NavigateToImportedOverrideGeometry(){RefreshSummaries();NavigateTo(FaceWorkspaceDestination.ArtworkOverrideGeometry);}
    private static void ShowOverrideError(string? error)=>MessageBox.Show(error??"The Artwork Override operation failed.","Artwork Override",MessageBoxButton.OK,MessageBoxImage.Error);
    private void ToggleOverride(){var value=ArtworkOverride;if(value is null)return;CommitOverride(DocumentTabViewModel.CopyOverride(value,enabled:!value.Enabled),value.Enabled?"Disable Artwork Override":"Enable Artwork Override");}
    private void CommitOverride(FaceArtworkOverrideModel value,string description){_document.SetArtworkOverride(value,description);RefreshSummaries();}
    public void CommitOverrideAlignment(double x,double y,double width,double height,string description="Align Artwork Override"){var value=ArtworkOverride;if(value is null)return;CommitOverride(DocumentTabViewModel.CopyOverride(value,x:x,y:y,width:width,height:height),description);}

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

    public void CancelLampPlacement()
    { if(!_isLampPlacementActive)return; _isLampPlacementActive=false; Raise(nameof(IsLampPlacementActive)); Raise(nameof(LampEditorStatus)); }

    public void CompleteLampPlacement(double x,double y,double width,double height)
    {
        if(!_isLampPlacementActive)return;
        var element=FaceElementFactory.CreateLampWindow(new System.Windows.Point(x,y));
        if(width>0&&height>0) element=new FaceLampWindowElement { ObjectId=element.ObjectId,Name=element.Name,X=x,Y=y,Width=width,Height=height,IsVisible=true };
        _document.CommandService.Execute(FaceMutationCommands.CreateAddLampWindowCommand(_document.DocumentId,_document,element));
        CancelLampPlacement();
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

    private async void RunBuild(bool force)
    {
        if (_isBuildInProgress) return;
        _isBuildInProgress = true;
        RaiseBuildCanExecuteChanged();
        try
        {
            var workItem = _document.PrepareFaceBuild(force);
            var prepared = await _document.ProgressDialogService.RunAsync(
                new EditorProgressRequest(
                    force ? "Rebuilding Face" : "Building Face",
                    force ? "Rebuilding Face outputs..." : "Building stale Face outputs...",
                    EditorProgressMode.Determinate,
                    CanCancel: true),
                (progress, token) => Task.FromResult(DocumentTabViewModel.ExecutePreparedFaceBuild(workItem, progress, token)));
            _document.CommitPreparedFaceBuild(prepared);
            RefreshSummaries();
        }
        catch (OperationCanceledException)
        {
            // Completed files may remain on disk, but no detached build state is committed.
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, force ? "Rebuild Face Failed" : "Build Face Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isBuildInProgress = false;
            RaiseBuildCanExecuteChanged();
        }
    }

    private void RaiseBuildCanExecuteChanged()
    {
        if (BuildFaceCommand is RelayCommand build) build.RaiseCanExecuteChanged();
        if (RebuildFaceCommand is RelayCommand rebuild) rebuild.RaiseCanExecuteChanged();
    }

    private void OpenGenerationSettings()
    {
        var dialog = new FaceGenerationSettingsDialog(_document.GetFaceDocument().GenerationSettings, "Save")
        {
            Owner = Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() != true) return;
        _document.CommandService.Execute(FaceMutationCommands.CreateSetGenerationSettingsCommand(
            _document.DocumentId, _document, dialog.Settings));
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
            if (artwork is null) return "Not configured";
            var source = artwork.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape ? "Panel2D / Face Source Shape" : "Image";
            var path = artwork.Source.Panel2DDocumentPath ?? artwork.Source.AssetPath;
            var dimensions = artwork.Source.PixelWidth > 0 ? $" • {artwork.Source.PixelWidth} × {artwork.Source.PixelHeight}" : string.Empty;
            return string.IsNullOrWhiteSpace(path) ? source : $"{source}: {path}{dimensions}";
        }
    }


    public bool IsImageArtworkSource => _document.GetFaceDocument().Artwork?.Source.Kind == FaceArtworkSourceKind.Image;
    public bool IsPanel2DArtworkSource => _document.GetFaceDocument().Artwork?.Source.Kind == FaceArtworkSourceKind.Panel2DFaceSourceShape;
    public bool CanChooseImageArtwork => !IsImageArtworkSource;
    public bool HasRetainedPanel2DArtworkSource
    {
        get
        {
            var face = _document.GetFaceDocument();
            return !string.IsNullOrWhiteSpace(face.SourcePanel2DDocumentId)
                && !string.IsNullOrWhiteSpace(face.SourceFaceShapeId);
        }
    }
    public bool CanShowUsePanel2DSource => IsImageArtworkSource && HasRetainedPanel2DArtworkSource;
    public bool CanUsePanel2DSource
    {
        get
        {
            return _document.CanUsePanel2DArtworkSource(out _);
        }
    }
    public string Panel2DSourceAvailability
    {
        get
        {
            return _document.CanUsePanel2DArtworkSource(out var reason)
                ? string.Empty
                : IsImageArtworkSource && HasRetainedPanel2DArtworkSource ? reason ?? "The retained Panel2D source is unavailable." : string.Empty;
        }
    }
    public string? ArtworkRawImagePath => _document.GetArtworkSourceAbsolutePath();
    public int ArtworkSourcePixelWidth => _document.GetFaceDocument().Artwork?.Source.PixelWidth ?? 0;
    public int ArtworkSourcePixelHeight => _document.GetFaceDocument().Artwork?.Source.PixelHeight ?? 0;
    public FacePerspectiveRegistrationModel ArtworkRegistration => _document.GetFaceDocument().Artwork?.Geometry.PerspectiveRegistration ?? FacePerspectiveRegistrationModel.FullImage;
    public void CommitRegistration(FacePerspectiveRegistrationModel value) { _document.SetArtworkRegistration(value); RefreshSummaries(); }
    public void ResetRegistration() { _document.SetArtworkRegistration(FacePerspectiveRegistrationModel.FullImage, "Reset artwork registration"); RefreshSummaries(); }

    // One destination-sensitive editing surface lets the existing semantic-quad viewport edit either authored recipe.
    public bool IsEditingOverrideGeometry => _destination == FaceWorkspaceDestination.ArtworkOverrideGeometry;
    public string? GeometryRawImagePath => IsEditingOverrideGeometry ? _document.GetArtworkAssetAbsolutePath(ArtworkOverride?.AssetPath) : ArtworkRawImagePath;
    public BitmapImage? GeometryRawImage => _geometryRawImage;
    public int GeometrySourcePixelWidth => IsEditingOverrideGeometry ? ArtworkOverride?.PixelWidth ?? 0 : ArtworkSourcePixelWidth;
    public int GeometrySourcePixelHeight => IsEditingOverrideGeometry ? ArtworkOverride?.PixelHeight ?? 0 : ArtworkSourcePixelHeight;
    public FacePerspectiveRegistrationModel GeometryRegistration => IsEditingOverrideGeometry ? ArtworkOverride?.PerspectiveRegistration ?? FacePerspectiveRegistrationModel.FullImage : ArtworkRegistration;
    public void CommitGeometryRegistration(FacePerspectiveRegistrationModel value)
    {
        if (IsEditingOverrideGeometry) { _document.SetArtworkOverrideRegistration(value); InvalidateOverrideAlignmentPreview(); } else _document.SetArtworkRegistration(value);
        RefreshSummaries();
    }
    public void ResetGeometryRegistration()
    {
        if (IsEditingOverrideGeometry) { _document.SetArtworkOverrideRegistration(FacePerspectiveRegistrationModel.FullImage, "Reset Artwork Override geometry"); InvalidateOverrideAlignmentPreview(); }
        else _document.SetArtworkRegistration(FacePerspectiveRegistrationModel.FullImage, "Reset artwork registration");
        RefreshSummaries();
    }

    public string ArtworkGeometrySummary => _document.GetFaceDocument().Artwork is null ? "Not configured" : IsImageArtworkSource
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

    public FaceArtworkOverrideModel? ArtworkOverride=>_document.GetFaceDocument().Artwork?.Override;
    public bool HasArtworkOverride=>ArtworkOverride is not null;
    public string ArtworkOverrideSummary=>ArtworkOverride is not { } value?"None":$"AUTHORED • {(value.Enabled?"Enabled":"Disabled")} • {value.PixelWidth} × {value.PixelHeight} • Alignment {value.X:0.###}, {value.Y:0.###} / {value.Width:P1} × {value.Height:P1} • {value.AssetPath}";
    public string OverrideToggleLabel=>ArtworkOverride?.Enabled==true?"Disable":"Enable";
    public BitmapImage? ArtworkBaseAbsolutePath
    {
        get
        {
            var path=_document.GetArtworkAssetAbsolutePath(_document.GetFaceDocument().Artwork?.BaseAssetPath);
            if(!string.Equals(path,_basePreviewPath,StringComparison.OrdinalIgnoreCase)){_basePreviewPath=path;_basePreview=ReloadableBitmapImageLoader.Load(path);}
            return _basePreview;
        }
    }
    /// <summary>A previously prepared frozen image only; binding evaluation never decodes or rectifies.</summary>
    public BitmapImage? ArtworkOverrideAbsolutePath => _overridePreview;
    public bool IsOverridePreviewLoading => _isOverridePreviewLoading;

    private string? CurrentOverridePreviewKey()
    {
        var value=ArtworkOverride;var path=_document.GetArtworkAssetAbsolutePath(value?.AssetPath);
        return value is null||string.IsNullOrWhiteSpace(path)?null:
            FaceArtworkOverridePreviewService.CreateCacheKey(path,value.ContentRevision,value.PerspectiveRegistration);
    }

    private void InvalidateOverrideAlignmentPreview()
    {
        _overridePreviewCancellation?.Cancel();_overridePreviewCancellation?.Dispose();_overridePreviewCancellation=null;
        _overridePreviewGeneration++;_overridePreview=null;_overridePreviewKey=null;_overridePreviewInputKey=CurrentOverridePreviewKey();
        SetOverridePreviewLoading(false);Raise(nameof(ArtworkOverrideAbsolutePath));
        if(_destination==FaceWorkspaceDestination.ArtworkOverride)RequestOverrideAlignmentPreview();
    }

    private async void RequestOverrideAlignmentPreview()
    {
        var value=ArtworkOverride;var path=_document.GetArtworkAssetAbsolutePath(value?.AssetPath);var key=CurrentOverridePreviewKey();
        if(value is null||string.IsNullOrWhiteSpace(path)||key is null||!File.Exists(path))return;
        if(string.Equals(_overridePreviewKey,key,StringComparison.Ordinal))return;
        _overridePreviewCancellation?.Cancel();_overridePreviewCancellation?.Dispose();
        var cancellation=new CancellationTokenSource();_overridePreviewCancellation=cancellation;var generation=++_overridePreviewGeneration;_overridePreviewInputKey=key;
        SetOverridePreviewLoading(true);
        try
        {
            await _overridePreviewGate.WaitAsync(cancellation.Token);
            FaceArtworkOverridePreviewResult result;
            try { result=await Task.Run(()=>FaceArtworkOverridePreviewService.Generate(path,value,cancellation.Token),cancellation.Token); }
            finally { _overridePreviewGate.Release(); }
            if(cancellation.IsCancellationRequested||generation!=_overridePreviewGeneration||_destination!=FaceWorkspaceDestination.ArtworkOverride||!string.Equals(key,CurrentOverridePreviewKey(),StringComparison.Ordinal))return;
            using var stream=new MemoryStream(result.PngBytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
            _overridePreview=bitmap;_overridePreviewKey=key;SetOverridePreviewLoading(false);Raise(nameof(ArtworkOverrideAbsolutePath));
        }
        catch(OperationCanceledException){}
        catch(Exception){if(generation==_overridePreviewGeneration){_overridePreview=null;SetOverridePreviewLoading(false);Raise(nameof(ArtworkOverrideAbsolutePath));}}
    }

    private void SetOverridePreviewLoading(bool value)
    {if(_isOverridePreviewLoading==value)return;_isOverridePreviewLoading=value;Raise(nameof(IsOverridePreviewLoading));}

    private void InvalidateGeometryRawImage()
    {
        _geometryRawImageCancellation?.Cancel();_geometryRawImageCancellation?.Dispose();_geometryRawImageCancellation=null;
        _geometryRawImageGeneration++;_geometryRawImage=null;_geometryRawImageKey=null;Raise(nameof(GeometryRawImage));
        if(_destination is FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry)RequestGeometryRawImage();
    }

    private async void RequestGeometryRawImage()
    {
        var path=GeometryRawImagePath;var revision=IsEditingOverrideGeometry?ArtworkOverride?.ContentRevision??0:0;var key=$"{path}|{revision}";
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||string.Equals(key,_geometryRawImageKey,StringComparison.Ordinal))return;
        _geometryRawImageCancellation?.Cancel();_geometryRawImageCancellation?.Dispose();var cancellation=new CancellationTokenSource();_geometryRawImageCancellation=cancellation;var generation=++_geometryRawImageGeneration;
        try
        {
            var image=await Task.Run(()=>ReloadableBitmapImageLoader.Load(path,GeometryPreviewMaximumDecodeDimension),cancellation.Token);
            if(cancellation.IsCancellationRequested||generation!=_geometryRawImageGeneration||_destination is not (FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry))return;
            _geometryRawImage=image;_geometryRawImageKey=key;Raise(nameof(GeometryRawImage));
        }
        catch(OperationCanceledException){}
        catch(Exception){if(generation==_geometryRawImageGeneration){_geometryRawImage=null;Raise(nameof(GeometryRawImage));}}
    }

    public Thickness OverridePreviewMargin=>new((ArtworkOverride?.X??0)*1000,(ArtworkOverride?.Y??0)*1000,0,0);
    public double OverridePreviewWidth=>(ArtworkOverride?.Width??1)*1000;
    public double OverridePreviewHeight=>(ArtworkOverride?.Height??1)*1000;
    internal void RefreshArtworkPreviews(bool refreshBase, bool refreshOverride)
    {
        if(refreshBase){_basePreview=null;_basePreviewPath=null;Raise(nameof(ArtworkBaseAbsolutePath));}
        if(refreshOverride){InvalidateOverrideAlignmentPreview();InvalidateGeometryRawImage();}
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
            var mask = face.MaskLayer is null ? "no mask" : face.MaskLayer.SourceKind==FaceLampMaskSourceKind.AuthoredImage ? $"authored image: {face.MaskLayer.AuthoredAssetPath}" : $"derived mask {face.MaskLayer.Width} × {face.MaskLayer.Height}";
            return $"{lamps} lamps • {mask} • {face.Trays.Count} trays • {face.LampEmitters.Count} emitters";
        }
    }

    public void NavigateTo(FaceWorkspaceDestination destination)
    {
        if (_destination == destination) return;
        if (destination != FaceWorkspaceDestination.ArtworkCalibration) _document.CancelCalibrationPlacement();
        if (destination != FaceWorkspaceDestination.ComponentsEditor) CancelComponentPlacement();
        if (destination != FaceWorkspaceDestination.IlluminationLamps) CancelLampPlacement();
        if(destination!=FaceWorkspaceDestination.ArtworkOverride){_overridePreviewCancellation?.Cancel();_overridePreviewGeneration++;SetOverridePreviewLoading(false);}
        if(destination is not (FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry)){_geometryRawImageCancellation?.Cancel();_geometryRawImageGeneration++;}
        _destination = destination;
        if(destination==FaceWorkspaceDestination.ArtworkOverride)RequestOverrideAlignmentPreview();
        if(destination is FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry)RequestGeometryRawImage();
        Raise(nameof(Destination));
        Raise(nameof(DestinationName));
        Raise(nameof(Breadcrumbs));
        Raise(nameof(IsViewportDestination)); Raise(nameof(IsEditingOverrideGeometry)); Raise(nameof(DoneGeometryCommand)); Raise(nameof(GeometryRawImagePath)); Raise(nameof(GeometryRawImage)); Raise(nameof(GeometrySourcePixelWidth)); Raise(nameof(GeometrySourcePixelHeight)); Raise(nameof(GeometryRegistration));
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
            FaceWorkspaceDestination.ArtworkOverride => [root, new("Artwork", NavigateToArtworkCommand), new("Override", null)],
            FaceWorkspaceDestination.ArtworkOverrideGeometry => [root, new("Artwork", NavigateToArtworkCommand), new("Override", NavigateToArtworkOverrideCommand), new("Geometry", null)],
            FaceWorkspaceDestination.Components => [root, new("Components", null)],
            FaceWorkspaceDestination.ComponentsEditor => [root, new("Components", NavigateToComponentsCommand), new("Edit", null)],
            FaceWorkspaceDestination.Illumination => [root, new("Illumination", null)],
            FaceWorkspaceDestination.IlluminationLamps => [root, new("Illumination", NavigateToIlluminationCommand), new("Lamps", null)],
            _ => [root, new("Layout View", null)]
        };
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    internal void RefreshSummaries()
    {
        RefreshArtworkSourceState();
        RefreshArtworkProcessingState();
        RefreshBuildState();
        Raise(nameof(ComponentsSummary)); Raise(nameof(IlluminationSummary));
        Raise(nameof(ComponentsProvenanceSummary)); Raise(nameof(IlluminationBuildSummary));
        if (RebuildComponentsFromSourceCommand is RelayCommand rebuildComponents) rebuildComponents.RaiseCanExecuteChanged();
    }

    private void RefreshArtworkSourceState()
    {
        var previewKey=CurrentOverridePreviewKey();
        if(!string.Equals(previewKey,_overridePreviewInputKey,StringComparison.Ordinal))InvalidateOverrideAlignmentPreview();
        if(_destination is FaceWorkspaceDestination.ArtworkGeometry or FaceWorkspaceDestination.ArtworkOverrideGeometry)RequestGeometryRawImage();
        Raise(nameof(ArtworkSourceSummary)); Raise(nameof(GeometryRawImagePath)); Raise(nameof(GeometryRawImage)); Raise(nameof(GeometrySourcePixelWidth)); Raise(nameof(GeometrySourcePixelHeight)); Raise(nameof(GeometryRegistration)); Raise(nameof(OverridePreviewMargin)); Raise(nameof(OverridePreviewWidth)); Raise(nameof(OverridePreviewHeight)); Raise(nameof(ArtworkOverride)); Raise(nameof(HasArtworkOverride)); Raise(nameof(ArtworkOverrideSummary)); Raise(nameof(OverrideToggleLabel)); Raise(nameof(ArtworkBaseAbsolutePath)); Raise(nameof(ArtworkOverrideAbsolutePath)); Raise(nameof(IsImageArtworkSource)); Raise(nameof(IsPanel2DArtworkSource)); Raise(nameof(CanChooseImageArtwork)); Raise(nameof(HasRetainedPanel2DArtworkSource)); Raise(nameof(CanShowUsePanel2DSource)); Raise(nameof(CanUsePanel2DSource)); Raise(nameof(Panel2DSourceAvailability)); Raise(nameof(ArtworkRawImagePath)); Raise(nameof(ArtworkSourcePixelWidth)); Raise(nameof(ArtworkSourcePixelHeight)); Raise(nameof(ArtworkRegistration)); Raise(nameof(ArtworkGeometrySummary));
        if (UsePanel2DSourceCommand is RelayCommand usePanel2D)
            usePanel2D.RaiseCanExecuteChanged();
    }

    internal void RefreshArtworkProcessingState()
    {
        Raise(nameof(ArtworkCalibrationSummary)); Raise(nameof(ArtworkCorrectionSummary)); Raise(nameof(ArtworkOutputSummary));
    }

    internal void RefreshBuildState()
    {
        Raise(nameof(BuildStatusSummary)); Raise(nameof(ArtworkBuildSummary));
        Raise(nameof(IlluminationBuildSummary));
        Raise(nameof(BuildErrorSummary));
    }
}
