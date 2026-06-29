using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetModelDocumentViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan LivePreviewRefreshInterval = TimeSpan.FromMilliseconds(1000d / 15d);

    private readonly ICabinetModelLoader _modelLoader;
    private readonly DocumentTabViewModel _document;
    private readonly Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private readonly FaceDocumentArtworkPreviewRenderer _previewRenderer = new();
    private readonly DispatcherTimer _livePreviewRefreshTimer;
    private readonly HashSet<Guid> _pendingLivePreviewDocumentIds = new();
    private readonly Dictionary<Guid, CabinetFacePreviewEntry> _facePreviewEntriesByDocumentId = new();
    private readonly Dictionary<CabinetStaticPreviewCacheKey, BitmapSource> _staticPreviewCache = new();
    private string _loadStatus = "No cabinet model loaded.";
    private string? _errorMessage;
    private bool _isLoading;
    private CabinetFaceTargetViewModel? _selectedFaceTarget;

    public CabinetModelDocumentViewModel(ICabinetModelLoader modelLoader, DocumentTabViewModel document, Func<IReadOnlyList<DocumentTabViewModel>>? openDocumentsAccessor = null)
    {
        _modelLoader = modelLoader;
        _document = document;
        _openDocumentsAccessor = openDocumentsAccessor;
        ModelPath = document.GetCabinetDocument().Model.Path;
        Viewport = new CabinetViewportViewModel();
        _livePreviewRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = LivePreviewRefreshInterval
        };
        _livePreviewRefreshTimer.Tick += OnLivePreviewRefreshTimerTick;
        ReloadCommand = new RelayCommand(async () => await LoadAsync(), CanLoad);
        ResetCameraCommand = Viewport.ResetCameraCommand;
        if (!string.IsNullOrWhiteSpace(ModelPath))
        {
            _ = LoadAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CabinetViewportViewModel Viewport { get; }
    public ObservableCollection<CabinetFaceTargetViewModel> FaceTargets { get; } = new();
    public string ModelPath { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(ModelPath) ? "Cabinet Model Viewer" : Path.GetFileName(ModelPath);
    public string LoadStatus { get => _loadStatus; private set { _loadStatus = value; OnPropertyChanged(); } }
    public string? ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasFaceTargets => FaceTargets.Count > 0;
    public string FaceTargetStatus => FaceTargets.Count == 0
        ? "No Oasis face targets found."
        : $"Detected {FaceTargets.Count} Oasis face target{(FaceTargets.Count == 1 ? string.Empty : "s")}.";
    public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); if (ReloadCommand is RelayCommand relay) relay.RaiseCanExecuteChanged(); } }
    public ICommand ReloadCommand { get; }
    public ICommand ResetCameraCommand { get; }
    public IReadOnlyList<string> FrontSideOptions { get; } = new[] { CabinetTargetOverride.NormalFrontSide, CabinetTargetOverride.InvertedFrontSide };
    public IReadOnlyList<int> FaceRotationOptions { get; } = new[] { 0, 90, 180, 270 };
    public IReadOnlyList<string> LampPreviewModeOptions { get; } = new[] { CabinetLampPreviewMode.Live, CabinetLampPreviewMode.BackgroundOnly, CabinetLampPreviewMode.LampsOff, CabinetLampPreviewMode.LampsAllOn };

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
        get => CabinetLampPreviewMode.Normalize(_document.GetCabinetDocument().Preview.LampPreviewMode);
        set => _document.CommandService.Execute(CabinetMutationCommands.CreateSetPreviewLampModeCommand(_document.DocumentId, _document, value));
    }

    public bool HasSelectedFaceTarget => SelectedFaceTarget is not null;

    public string SelectedFrontSide
    {
        get => SelectedFaceTarget is null ? CabinetTargetOverride.NormalFrontSide : _document.GetCabinetDocument().GetTargetOverride(SelectedFaceTarget.Id).FrontSide;
        set
        {
            if (SelectedFaceTarget is null) return;
            _document.CommandService.Execute(CabinetMutationCommands.CreateSetTargetFrontSideCommand(_document.DocumentId, _document, SelectedFaceTarget.Id, value));
        }
    }

    public int SelectedFaceRotation
    {
        get => SelectedFaceTarget is null ? 0 : _document.GetCabinetDocument().GetTargetOverride(SelectedFaceTarget.Id).FaceRotation;
        set
        {
            if (SelectedFaceTarget is null) return;
            _document.CommandService.Execute(CabinetMutationCommands.CreateSetTargetFaceRotationCommand(_document.DocumentId, _document, SelectedFaceTarget.Id, value));
        }
    }

    public bool IsSelectedFaceFlippedHorizontally
    {
        get => SelectedFaceTarget is not null && _document.GetCabinetDocument().GetTargetOverride(SelectedFaceTarget.Id).FaceFlipHorizontal;
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
        InvalidatePreviewCache();
        RefreshFacePreviews();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoad()) return;
        IsLoading = true;
        ErrorMessage = null;
        LoadStatus = $"Loading {DisplayName}...";
        try
        {
            var result = await _modelLoader.LoadAsync(ModelPath, cancellationToken);
            if (!result.Succeeded || result.Model is null)
            {
                Viewport.Model = null;
                Viewport.FacePreviewModel = null;
                FaceTargets.Clear();
                OnPropertyChanged(nameof(HasFaceTargets));
                OnPropertyChanged(nameof(FaceTargetStatus));
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
            RefreshFacePreviews();
            LoadStatus = FaceTargets.Count == 0
                ? $"Loaded {DisplayName}; no Oasis face targets found"
                : $"Loaded {DisplayName}; detected {FaceTargets.Count} Oasis face target{(FaceTargets.Count == 1 ? string.Empty : "s")}";
        }
        catch (OperationCanceledException)
        {
            LoadStatus = "Cabinet model load cancelled.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void RefreshFacePreviews()
    {
        _pendingLivePreviewDocumentIds.Clear();
        _livePreviewRefreshTimer.Stop();
        _facePreviewEntriesByDocumentId.Clear();
        var validTargets = FaceTargets.Where(target => target.IsValid).ToDictionary(target => target.Id, StringComparer.Ordinal);
        if (validTargets.Count == 0 || _openDocumentsAccessor is null)
        {
            Viewport.FacePreviewModel = null;
            return;
        }

        var previewGroup = new Model3DGroup();
        foreach (var document in _openDocumentsAccessor()
            .Where(document => document.Document.DocumentType == EditorDocumentType.Face))
        {
            var faceDocument = document.GetFaceDocument();
            var targetId = NormalizeTargetId(faceDocument.AssignedCabinetFaceTargetId);
            if (targetId is null || !validTargets.TryGetValue(targetId, out var target))
            {
                continue;
            }

            var previewMode = SelectedLampPreviewMode;
            var preview = ResolvePreviewImage(document, faceDocument, previewMode);
            if (preview is null)
            {
                continue;
            }

            var targetOverride = _document.GetCabinetDocument().GetTargetOverride(target.Id);
            if (TryCreatePreviewGeometry(target.Target, targetOverride, preview, out var geometry, out var imageBrush))
            {
                _facePreviewEntriesByDocumentId[document.DocumentId] = new CabinetFacePreviewEntry(document.DocumentId, target.Id, geometry, imageBrush);
                previewGroup.Children.Add(geometry);
            }
        }

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
        var assignedTargetId = NormalizeTargetId(faceDocument.AssignedCabinetFaceTargetId);
        if (!string.Equals(assignedTargetId, entry.TargetId, StringComparison.Ordinal))
        {
            RefreshFacePreviews();
            return;
        }

        var preview = _previewRenderer.RenderPreview(
            faceDocument,
            document.RuntimeState,
            CabinetLampPreviewMode.Live,
            FaceDocumentArtworkPreviewRenderer.LivePreviewRenderOptions);
        if (preview is null)
        {
            return;
        }

        entry.ImageBrush.ImageSource = preview;
    }

    private BitmapSource? ResolvePreviewImage(DocumentTabViewModel document, FaceDocumentModel faceDocument, string previewMode)
    {
        if (previewMode == CabinetLampPreviewMode.Live)
        {
            return _previewRenderer.RenderPreview(
                faceDocument,
                document.RuntimeState,
                previewMode,
                FaceDocumentArtworkPreviewRenderer.LivePreviewRenderOptions);
        }

        var cacheKey = CabinetStaticPreviewCacheKey.Create(document.DocumentId, document.FaceDocumentJson, previewMode);
        if (!_staticPreviewCache.TryGetValue(cacheKey, out var preview))
        {
            preview = _previewRenderer.RenderPreview(
                faceDocument,
                document.RuntimeState,
                previewMode,
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
    }

    private static bool TryCreatePreviewGeometry(CabinetFaceTarget target, CabinetTargetOverride targetOverride, BitmapSource bitmap, out GeometryModel3D geometry, out ImageBrush imageBrush)
    {
        geometry = default!;
        imageBrush = default!;
        if (!target.IsValid || target.Corners.Count != 4)
        {
            return false;
        }

        var positions = GetRenderQuadCorners(target.Corners, targetOverride);
        var isInverted = CabinetTargetOverride.NormalizeFrontSide(targetOverride.FrontSide) == CabinetTargetOverride.InvertedFrontSide;
        var reverseWinding = targetOverride.FaceFlipHorizontal ^ isInverted;
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

    private static Point3D[] GetRenderQuadCorners(IReadOnlyList<Point3D> sourceCorners, CabinetTargetOverride targetOverride)
    {
        var rotated = CabinetTargetOverride.NormalizeFaceRotation(targetOverride.FaceRotation) switch
        {
            90 => new[] { sourceCorners[3], sourceCorners[0], sourceCorners[1], sourceCorners[2] },
            180 => new[] { sourceCorners[2], sourceCorners[3], sourceCorners[0], sourceCorners[1] },
            270 => new[] { sourceCorners[1], sourceCorners[2], sourceCorners[3], sourceCorners[0] },
            _ => new[] { sourceCorners[0], sourceCorners[1], sourceCorners[2], sourceCorners[3] }
        };

        return targetOverride.FaceFlipHorizontal
            ? new[] { rotated[1], rotated[0], rotated[3], rotated[2] }
            : rotated;
    }

    private static string? NormalizeTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private bool CanLoad() => !IsLoading && !string.IsNullOrWhiteSpace(ModelPath);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record CabinetFacePreviewEntry(Guid FaceDocumentId, string TargetId, GeometryModel3D Geometry, ImageBrush ImageBrush);

    private sealed record CabinetStaticPreviewCacheKey(Guid FaceDocumentId, string FaceDocumentJson, string PreviewMode)
    {
        public static CabinetStaticPreviewCacheKey Create(Guid faceDocumentId, string? faceDocumentJson, string previewMode)
        {
            return new CabinetStaticPreviewCacheKey(faceDocumentId, faceDocumentJson ?? string.Empty, previewMode);
        }
    }
}
