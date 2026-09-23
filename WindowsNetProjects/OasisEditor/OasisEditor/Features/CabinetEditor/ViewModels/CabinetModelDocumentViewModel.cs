using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;
using OasisEditor.Rendering;
using SkiaSharp;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetModelDocumentViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan LivePreviewRefreshInterval = TimeSpan.FromMilliseconds(1000d / 15d);

    private readonly ICabinetModelLoader _modelLoader;
    private readonly DocumentTabViewModel _document;
    private readonly Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private readonly Func<EditorProject?>? _projectAccessor;
    private readonly FaceDocumentArtworkPreviewRenderer _previewRenderer = new();
    private readonly CabinetFacePreviewSourceResolver _facePreviewSourceResolver = new();
    private readonly DispatcherTimer _livePreviewRefreshTimer;
    private readonly HashSet<Guid> _pendingLivePreviewDocumentIds = new();
    private readonly Dictionary<Guid, CabinetFacePreviewEntry> _facePreviewEntriesByDocumentId = new();
    private readonly Dictionary<CabinetStaticPreviewCacheKey, BitmapSource> _staticPreviewCache = new();
    private readonly Dictionary<CabinetLiveBaseCacheKey, CabinetLiveBaseTexture> _liveBaseCache = new();
    private string _loadStatus = "No cabinet model loaded.";
    private string? _errorMessage;
    private bool _isLoading;
    private CabinetFaceTargetViewModel? _selectedFaceTarget;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _disposed;
    private bool _initialized;
    private DocumentTabViewModel? _machineCompositionContext;
    private string _selectedLampPreviewMode = CabinetLampPreviewMode.Live;

    public CabinetModelDocumentViewModel(ICabinetModelLoader modelLoader, DocumentTabViewModel document, Func<IReadOnlyList<DocumentTabViewModel>>? openDocumentsAccessor = null, Func<EditorProject?>? projectAccessor = null)
    {
        _modelLoader = modelLoader;
        _document = document;
        _openDocumentsAccessor = openDocumentsAccessor;
        _projectAccessor = projectAccessor;
        ModelPath = document.GetCabinetDocument().Model.Path;
        Viewport = new CabinetViewportViewModel();
        _livePreviewRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = LivePreviewRefreshInterval
        };
        _livePreviewRefreshTimer.Tick += OnLivePreviewRefreshTimerTick;
        ReloadCommand = new RelayCommand(async () => await LoadAsync(), CanLoad);
        ResetCameraCommand = Viewport.ResetCameraCommand;
        ReflectionEditor = new CabinetReflectionEditorViewModel(document);
    }

    public void Initialize()
    {
        if (_initialized || _disposed) return;
        _initialized = true;
        ReflectionEditor.Initialize();
        if (!string.IsNullOrWhiteSpace(ModelPath))
        {
            _ = LoadAsync();
        }
    }

    public void SetMachineCompositionContext(DocumentTabViewModel? machineDocument)
    {
        if (ReferenceEquals(_machineCompositionContext, machineDocument)) return;
        _machineCompositionContext = machineDocument;
        OnPropertyChanged(nameof(PreviewingMachine));
        RefreshFacePreviews();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CabinetViewportViewModel Viewport { get; }
    public ObservableCollection<CabinetFaceTargetViewModel> FaceTargets { get; } = new();
    public CabinetReflectionEditorViewModel ReflectionEditor { get; }
    public string ModelPath { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(ModelPath) ? "Cabinet Model Viewer" : Path.GetFileName(ModelPath);
    public string LoadStatus { get => _loadStatus; private set { _loadStatus = value; OnPropertyChanged(); } }
    public string? ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasFaceTargets => FaceTargets.Count > 0;
    public IReadOnlyList<string> FacePreviewDiagnostics { get; private set; } = [];
    public bool HasFacePreviewDiagnostics => FacePreviewDiagnostics.Count > 0;
    public string FaceTargetStatus => FaceTargets.Count == 0
        ? "No Oasis face targets found."
        : $"Detected {FaceTargets.Count} Oasis face target{(FaceTargets.Count == 1 ? string.Empty : "s")}.";
    public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); if (ReloadCommand is RelayCommand relay) relay.RaiseCanExecuteChanged(); } }
    public ICommand ReloadCommand { get; }
    public ICommand ResetCameraCommand { get; }
    public IReadOnlyList<string> FrontSideOptions { get; } = new[] { CabinetSurfaceTargetSettings.NormalFrontSide, CabinetSurfaceTargetSettings.InvertedFrontSide };
    public IReadOnlyList<int> FaceRotationOptions { get; } = new[] { 0, 90, 180, 270 };
    public IReadOnlyList<string> LampPreviewModeOptions { get; } = new[] { CabinetLampPreviewMode.Live, CabinetLampPreviewMode.BackgroundOnly, CabinetLampPreviewMode.LampsOff, CabinetLampPreviewMode.LampsAllOn };
    public string PreviewingMachine => _machineCompositionContext is null ? "No Machine context" : $"Previewing Machine: {_machineCompositionContext.Title}";

    public CabinetFaceTargetViewModel? SelectedFaceTarget
    {
        get => _selectedFaceTarget;
        set
        {
            if (ReferenceEquals(_selectedFaceTarget, value)) return;
            _selectedFaceTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedFaceTarget));
            OnPropertyChanged(nameof(SelectedFrontSide));
            OnPropertyChanged(nameof(SelectedFaceRotation));
            OnPropertyChanged(nameof(IsSelectedFaceFlippedHorizontally));
        }
    }

    public string SelectedLampPreviewMode
    {
        get => _selectedLampPreviewMode;
        set
        {
            var normalized = CabinetLampPreviewMode.Normalize(value);
            if (string.Equals(_selectedLampPreviewMode, normalized, StringComparison.Ordinal)) return;
            _selectedLampPreviewMode = normalized;
            OnPropertyChanged();
            InvalidatePreviewCache();
            RefreshFacePreviews();
        }
    }

    public bool HasSelectedFaceTarget => SelectedFaceTarget is not null;

    public string SelectedFrontSide
    {
        get => SelectedFaceTarget is null ? CabinetSurfaceTargetSettings.NormalFrontSide : _document.GetCabinetDocument().GetSurfaceTargetSettings(SelectedFaceTarget.Id).FrontSide;
        set
        {
            if (SelectedFaceTarget is null) return;
            _document.CommandService.Execute(CabinetMutationCommands.CreateSetTargetFrontSideCommand(_document.DocumentId, _document, SelectedFaceTarget.Id, value));
        }
    }

    public int SelectedFaceRotation
    {
        get => SelectedFaceTarget is null ? 0 : _document.GetCabinetDocument().GetSurfaceTargetSettings(SelectedFaceTarget.Id).FaceRotation;
        set
        {
            if (SelectedFaceTarget is null) return;
            _document.CommandService.Execute(CabinetMutationCommands.CreateSetTargetFaceRotationCommand(_document.DocumentId, _document, SelectedFaceTarget.Id, value));
        }
    }

    public bool IsSelectedFaceFlippedHorizontally
    {
        get => SelectedFaceTarget is not null && _document.GetCabinetDocument().GetSurfaceTargetSettings(SelectedFaceTarget.Id).FaceFlipHorizontal;
        set
        {
            if (SelectedFaceTarget is null) return;
            _document.CommandService.Execute(CabinetMutationCommands.CreateSetTargetFaceFlipHorizontalCommand(_document.DocumentId, _document, SelectedFaceTarget.Id, value));
        }
    }

    public void RefreshFromDocument(CabinetDocument document)
    {
        OnPropertyChanged(nameof(SelectedFrontSide));
        OnPropertyChanged(nameof(SelectedFaceRotation));
        OnPropertyChanged(nameof(IsSelectedFaceFlippedHorizontally));
        OnPropertyChanged(nameof(SelectedLampPreviewMode));
        ReflectionEditor.Refresh();
        InvalidatePreviewCache();
        RefreshFacePreviews();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !CanLoad()) return;
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        IsLoading = true;
        ErrorMessage = null;
        LoadStatus = $"Loading {DisplayName}...";
        try
        {
            var result = await _modelLoader.LoadAsync(ModelPath, linkedCancellation.Token);
            if (_disposed) return;
            if (!result.Succeeded || result.Model is null)
            {
                ClearLoadedModelDiscovery();
                ErrorMessage = result.ErrorMessage ?? "Unable to load the cabinet model.";
                LoadStatus = "Cabinet model load failed.";
                return;
            }

            Viewport.Model = result.Model;
            FaceTargets.Clear();
            foreach (var target in result.FaceTargets)
            {
                FaceTargets.Add(new CabinetFaceTargetViewModel(target));
            }
            OnPropertyChanged(nameof(HasFaceTargets));
            OnPropertyChanged(nameof(FaceTargetStatus));
            SelectedFaceTarget = FaceTargets.FirstOrDefault();
            ReflectionEditor.SetDiscovery(result.ReflectionTargets, result.FaceTargets);
            RefreshFacePreviews();
            LoadStatus = FaceTargets.Count == 0
                ? $"Loaded {DisplayName}; no Oasis face targets found"
                : $"Loaded {DisplayName}; detected {FaceTargets.Count} Oasis face target{(FaceTargets.Count == 1 ? string.Empty : "s")}";
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) LoadStatus = "Cabinet model load cancelled.";
        }
        finally
        {
            if (!_disposed) IsLoading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
        _livePreviewRefreshTimer.Stop();
        _livePreviewRefreshTimer.Tick -= OnLivePreviewRefreshTimerTick;
        foreach (var liveBase in _liveBaseCache.Values) liveBase.Dispose();
        _liveBaseCache.Clear();
        _staticPreviewCache.Clear();
        _facePreviewEntriesByDocumentId.Clear();
        _pendingLivePreviewDocumentIds.Clear();
        ClearLoadedModelDiscovery();
        ReflectionEditor.Dispose();
    }

    private void ClearLoadedModelDiscovery()
    {
        Viewport.Model = null;
        Viewport.FacePreviewModel = null;
        FaceTargets.Clear();
        SelectedFaceTarget = null;
        ReflectionEditor.ClearDiscovery();
        OnPropertyChanged(nameof(HasFaceTargets));
        OnPropertyChanged(nameof(FaceTargetStatus));
    }

    public void RefreshFacePreviews()
    {
        _pendingLivePreviewDocumentIds.Clear(); _livePreviewRefreshTimer.Stop(); _facePreviewEntriesByDocumentId.Clear();
        FacePreviewDiagnostics = [];
        OnPropertyChanged(nameof(FacePreviewDiagnostics));
        OnPropertyChanged(nameof(HasFacePreviewDiagnostics));
        var diagnostics = new List<string>();
        var validTargets = FaceTargets.Where(target => target.IsValid).ToDictionary(target => target.Id, StringComparer.Ordinal);
        var project = _projectAccessor?.Invoke();
        if (validTargets.Count == 0 || _openDocumentsAccessor is null || project is null || _machineCompositionContext is null)
        { Viewport.FacePreviewModel = null; return; }
        var machine = _machineCompositionContext.GetMachineDocument();
        if (string.IsNullOrWhiteSpace(machine.CabinetAssetPath)
            || !string.Equals(Path.GetFullPath(_document.FilePath), new ProjectAssetPathService().ResolveProjectRelativePath(project, machine.CabinetAssetPath), StringComparison.OrdinalIgnoreCase))
        { Viewport.FacePreviewModel = null; return; }
        var previewGroup = new Model3DGroup();
        foreach (var assignment in machine.SurfaceAssignments)
        {
            if (!validTargets.TryGetValue(assignment.TargetId, out var target)) continue;
            if (!_facePreviewSourceResolver.TryResolve(project, assignment.FaceAssetPath, _openDocumentsAccessor(), out var source, out var diagnostic))
            {
                if (!string.IsNullOrWhiteSpace(diagnostic)) diagnostics.Add(diagnostic);
                continue;
            }
            var preview = ResolvePreviewImage(source, SelectedLampPreviewMode, out var livePreviewTexture);
            if (preview is null) continue;
            var targetSettings = _document.GetCabinetDocument().GetSurfaceTargetSettings(target.Id);
            if (!TryCreatePreviewGeometry(target.Target, targetSettings, preview, out var geometry, out var imageBrush)) continue;
            if (source.OpenDocument is { } faceTab)
                _facePreviewEntriesByDocumentId[faceTab.DocumentId] = new CabinetFacePreviewEntry(faceTab.DocumentId, target.Id, geometry, imageBrush, livePreviewTexture);
            previewGroup.Children.Add(geometry);
        }
        FacePreviewDiagnostics = diagnostics;
        OnPropertyChanged(nameof(FacePreviewDiagnostics));
        OnPropertyChanged(nameof(HasFacePreviewDiagnostics));
        Viewport.FacePreviewModel = previewGroup.Children.Count == 0 ? null : previewGroup;
    }

    public void QueueFaceRuntimePreviewRefresh(Guid faceDocumentId)
    {
        if (SelectedLampPreviewMode != CabinetLampPreviewMode.Live)
        {
            return;
        }

        if (!_facePreviewEntriesByDocumentId.ContainsKey(faceDocumentId))
        {
            return;
        }

        _pendingLivePreviewDocumentIds.Add(faceDocumentId);
        if (!_livePreviewRefreshTimer.IsEnabled)
        {
            _livePreviewRefreshTimer.Start();
        }
    }

    private void OnLivePreviewRefreshTimerTick(object? sender, EventArgs e)
    {
        _livePreviewRefreshTimer.Stop();
        if (_pendingLivePreviewDocumentIds.Count == 0)
        {
            return;
        }

        var pendingDocumentIds = _pendingLivePreviewDocumentIds.ToArray();
        _pendingLivePreviewDocumentIds.Clear();
        foreach (var documentId in pendingDocumentIds)
        {
            RefreshLivePreviewImage(documentId);
        }
    }

    private void RefreshLivePreviewImage(Guid faceDocumentId)
    {
        if (_openDocumentsAccessor is null
            || !_facePreviewEntriesByDocumentId.TryGetValue(faceDocumentId, out var entry))
        {
            return;
        }

        var document = _openDocumentsAccessor()
            .FirstOrDefault(candidate => candidate.DocumentId == faceDocumentId && candidate.Document.DocumentType == EditorDocumentType.Face);
        if (document is null)
        {
            return;
        }

        var faceDocument = document.GetFaceDocument();

        if (entry.LivePreviewTexture is null)
        {
            return;
        }

        var totalStopwatch = Stopwatch.StartNew();
        var frame = ComposeLivePreviewFrame(document, faceDocument, out var stats);
        if (frame is null)
        {
            return;
        }

        var textureStopwatch = Stopwatch.StartNew();
        var textureRecreated = entry.LivePreviewTexture.Update(frame);
        textureStopwatch.Stop();

        var materialStopwatch = Stopwatch.StartNew();
        if (textureRecreated)
        {
            entry.ImageBrush.ImageSource = entry.LivePreviewTexture.Bitmap;
        }
        materialStopwatch.Stop();
        totalStopwatch.Stop();
        Trace.WriteLineIf(totalStopwatch.ElapsedMilliseconds > 16,
            $"Cabinet3D Live preview update face={faceDocumentId} staticBaseCacheHit={stats.StaticBaseCacheHit} staticBaseRenderMs={stats.StaticBaseRenderMilliseconds:0.00} lampOverlayRenderMs={stats.LampOverlayRenderMilliseconds:0.00} finalComposeOrCopyMs={stats.FinalComposeOrCopyMilliseconds:0.00} textureUploadMs={textureStopwatch.Elapsed.TotalMilliseconds:0.00} materialMs={materialStopwatch.Elapsed.TotalMilliseconds:0.00} totalMs={totalStopwatch.Elapsed.TotalMilliseconds:0.00} textureRecreated={textureRecreated}");
    }

    private BitmapSource? ResolvePreviewImage(CabinetFacePreviewSource source, string previewMode, out CabinetLivePreviewTexture? livePreviewTexture)
    {
        livePreviewTexture = null;
        if (previewMode == CabinetLampPreviewMode.Live && source.OpenDocument is { } document)
        {
            var frame = ComposeLivePreviewFrame(document, source.FaceDocument, out _);
            if (frame is null)
            {
                return null;
            }

            livePreviewTexture = CabinetLivePreviewTexture.Create(frame);
            return livePreviewTexture.Bitmap;
        }

        var effectivePreviewMode = previewMode == CabinetLampPreviewMode.Live ? CabinetLampPreviewMode.LampsOff : previewMode;
        var cacheKey = new CabinetStaticPreviewCacheKey(source.CacheIdentity, source.ContentIdentity, effectivePreviewMode);
        if (!_staticPreviewCache.TryGetValue(cacheKey, out var preview))
        {
            preview = _previewRenderer.RenderPreview(
                source.FaceDocument,
                source.RuntimeState,
                effectivePreviewMode,
                FaceDocumentArtworkPreviewRenderer.StaticPreviewRenderOptions);
            if (preview is not null)
            {
                _staticPreviewCache[cacheKey] = preview;
            }
        }

        return preview;
    }

    private void InvalidatePreviewCache()
    {
        _staticPreviewCache.Clear();
        foreach (var liveBase in _liveBaseCache.Values)
        {
            liveBase.Dispose();
        }

        _liveBaseCache.Clear();
    }

    private SKBitmap? ComposeLivePreviewFrame(DocumentTabViewModel document, FaceDocumentModel faceDocument, out CabinetLivePreviewFrameStats stats)
    {
        stats = CabinetLivePreviewFrameStats.Empty;
        if (!FaceCompositor.TryResolveTarget(faceDocument, FaceDocumentArtworkPreviewRenderer.LivePreviewRenderOptions, out var target, out _))
        {
            return null;
        }

        var cacheKey = CabinetLiveBaseCacheKey.Create(document.DocumentId, document.FaceDocumentJson, target);
        var staticBaseCacheHit = _liveBaseCache.TryGetValue(cacheKey, out var liveBase);
        var staticBaseRenderMilliseconds = 0d;
        if (!staticBaseCacheHit)
        {
            var staticBaseStopwatch = Stopwatch.StartNew();
            using var staticBaseResult = FaceCompositor.Shared.ComposeStaticBase(faceDocument, document.RuntimeState, FaceDocumentArtworkPreviewRenderer.LivePreviewRenderOptions);
            staticBaseStopwatch.Stop();
            staticBaseRenderMilliseconds = staticBaseStopwatch.Elapsed.TotalMilliseconds;
            if (!staticBaseResult.Rendered || staticBaseResult.Bitmap is null)
            {
                return null;
            }

            RemoveStaleLiveBaseCacheEntries(document.DocumentId, cacheKey);
            liveBase = new CabinetLiveBaseTexture(staticBaseResult.Bitmap.Copy(), target);
            _liveBaseCache[cacheKey] = liveBase;
        }

        var finalCopyStopwatch = Stopwatch.StartNew();
        liveBase.CopyStaticBaseToWorkingBitmap();
        finalCopyStopwatch.Stop();

        var lampOverlayStopwatch = Stopwatch.StartNew();
        using (var canvas = new SKCanvas(liveBase.WorkingBitmap))
        {
            FaceCompositor.ApplyTargetTransform(canvas, liveBase.Target);
            FaceCompositor.Shared.RenderLampOverlay(canvas, faceDocument, document.RuntimeState);
            canvas.Flush();
        }
        lampOverlayStopwatch.Stop();

        stats = new CabinetLivePreviewFrameStats(
            staticBaseCacheHit,
            staticBaseRenderMilliseconds,
            lampOverlayStopwatch.Elapsed.TotalMilliseconds,
            finalCopyStopwatch.Elapsed.TotalMilliseconds);
        return liveBase.WorkingBitmap;
    }

    private void RemoveStaleLiveBaseCacheEntries(Guid faceDocumentId, CabinetLiveBaseCacheKey currentKey)
    {
        var staleKeys = _liveBaseCache.Keys
            .Where(key => key.FaceDocumentId == faceDocumentId && !Equals(key, currentKey))
            .ToArray();
        foreach (var staleKey in staleKeys)
        {
            _liveBaseCache[staleKey].Dispose();
            _liveBaseCache.Remove(staleKey);
        }
    }

    private static bool TryCreatePreviewGeometry(CabinetFaceTarget target, CabinetSurfaceTargetSettings targetSettings, BitmapSource bitmap, out GeometryModel3D geometry, out ImageBrush imageBrush)
    {
        geometry = default!;
        imageBrush = default!;
        if (!target.IsValid || target.Corners.Count != 4)
        {
            return false;
        }

        var positions = GetRenderQuadCorners(target.Corners, targetSettings);
        var isInverted = CabinetSurfaceTargetSettings.NormalizeFrontSide(targetSettings.FrontSide) == CabinetSurfaceTargetSettings.InvertedFrontSide;
        var reverseWinding = targetSettings.FaceFlipHorizontal ^ isInverted;
        var triangleIndices = reverseWinding
            ? new[] { 0, 2, 1, 0, 3, 2 }
            : new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(positions),
            TriangleIndices = new Int32Collection(triangleIndices),
            TextureCoordinates = new PointCollection(new[]
            {
                new Point(0, 0),
                new Point(1, 0),
                new Point(1, 1),
                new Point(0, 1)
            })
        };
        imageBrush = new ImageBrush(bitmap);
        var material = new DiffuseMaterial(imageBrush);
        geometry = new GeometryModel3D(mesh, material);
        return true;
    }

    private static Point3D[] GetRenderQuadCorners(IReadOnlyList<Point3D> sourceCorners, CabinetSurfaceTargetSettings targetSettings)
    {
        var rotated = CabinetSurfaceTargetSettings.NormalizeFaceRotation(targetSettings.FaceRotation) switch
        {
            90 => new[] { sourceCorners[3], sourceCorners[0], sourceCorners[1], sourceCorners[2] },
            180 => new[] { sourceCorners[2], sourceCorners[3], sourceCorners[0], sourceCorners[1] },
            270 => new[] { sourceCorners[1], sourceCorners[2], sourceCorners[3], sourceCorners[0] },
            _ => new[] { sourceCorners[0], sourceCorners[1], sourceCorners[2], sourceCorners[3] }
        };

        return targetSettings.FaceFlipHorizontal
            ? new[] { rotated[1], rotated[0], rotated[3], rotated[2] }
            : rotated;
    }

    private static string? NormalizeTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private bool CanLoad() => !IsLoading && !string.IsNullOrWhiteSpace(ModelPath);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record CabinetFacePreviewEntry(Guid FaceDocumentId, string TargetId, GeometryModel3D Geometry, ImageBrush ImageBrush, CabinetLivePreviewTexture? LivePreviewTexture);

    private sealed record CabinetLivePreviewFrameStats(
        bool StaticBaseCacheHit,
        double StaticBaseRenderMilliseconds,
        double LampOverlayRenderMilliseconds,
        double FinalComposeOrCopyMilliseconds)
    {
        public static CabinetLivePreviewFrameStats Empty { get; } = new(false, 0d, 0d, 0d);
    }

    private sealed class CabinetLiveBaseTexture : IDisposable
    {
        public CabinetLiveBaseTexture(SKBitmap staticBaseBitmap, FaceCompositorTarget target)
        {
            StaticBaseBitmap = staticBaseBitmap ?? throw new ArgumentNullException(nameof(staticBaseBitmap));
            Target = target;
            WorkingBitmap = new SKBitmap(staticBaseBitmap.Info);
        }

        public SKBitmap StaticBaseBitmap { get; }
        public SKBitmap WorkingBitmap { get; }
        public FaceCompositorTarget Target { get; }

        public unsafe void CopyStaticBaseToWorkingBitmap()
        {
            var sourcePixels = (byte*)StaticBaseBitmap.GetPixels().ToPointer();
            var destinationPixels = (byte*)WorkingBitmap.GetPixels().ToPointer();
            if (sourcePixels == null || destinationPixels == null)
            {
                return;
            }

            var height = Math.Min(StaticBaseBitmap.Height, WorkingBitmap.Height);
            var sourceRowBytes = StaticBaseBitmap.RowBytes;
            var destinationRowBytes = WorkingBitmap.RowBytes;
            var copyRowBytes = Math.Min(sourceRowBytes, destinationRowBytes);
            for (var y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(
                    sourcePixels + (y * sourceRowBytes),
                    destinationPixels + (y * destinationRowBytes),
                    destinationRowBytes,
                    copyRowBytes);
            }
        }

        public void Dispose()
        {
            StaticBaseBitmap.Dispose();
            WorkingBitmap.Dispose();
        }
    }

    private sealed class CabinetLivePreviewTexture
    {
        private CabinetLivePreviewTexture(WriteableBitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public WriteableBitmap Bitmap { get; private set; }

        public static CabinetLivePreviewTexture Create(SKBitmap source)
        {
            var texture = new CabinetLivePreviewTexture(CreateBitmap(source.Width, source.Height));
            texture.WritePixels(source);
            return texture;
        }

        public bool Update(SKBitmap source)
        {
            if (Bitmap.PixelWidth != source.Width || Bitmap.PixelHeight != source.Height)
            {
                Bitmap = CreateBitmap(source.Width, source.Height);
                WritePixels(source);
                return true;
            }

            WritePixels(source);
            return false;
        }

        private void WritePixels(SKBitmap source)
        {
            var pixels = source.GetPixels();
            if (pixels == IntPtr.Zero)
            {
                return;
            }

            Bitmap.WritePixels(
                new Int32Rect(0, 0, source.Width, source.Height),
                pixels,
                source.ByteCount,
                source.RowBytes);
        }

        private static WriteableBitmap CreateBitmap(int width, int height)
        {
            return new WriteableBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32, null);
        }
    }

    private sealed record CabinetStaticPreviewCacheKey(string SourceIdentity, string ContentIdentity, string PreviewMode);

    private sealed record CabinetLiveBaseCacheKey(Guid FaceDocumentId, string FaceDocumentJson, int TextureWidth, int TextureHeight)
    {
        public static CabinetLiveBaseCacheKey Create(Guid faceDocumentId, string? faceDocumentJson, FaceCompositorTarget target)
        {
            return new CabinetLiveBaseCacheKey(faceDocumentId, faceDocumentJson ?? string.Empty, target.Width, target.Height);
        }
    }
}
