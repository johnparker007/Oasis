using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetModelDocumentViewModel : INotifyPropertyChanged
{
    private readonly ICabinetModelLoader _modelLoader;
    private readonly Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private readonly FaceDocumentArtworkPreviewRenderer _previewRenderer = new();
    private string _loadStatus = "No cabinet model loaded.";
    private string? _errorMessage;
    private bool _isLoading;

    public CabinetModelDocumentViewModel(ICabinetModelLoader modelLoader, string? modelPath = null, Func<IReadOnlyList<DocumentTabViewModel>>? openDocumentsAccessor = null)
    {
        _modelLoader = modelLoader;
        _openDocumentsAccessor = openDocumentsAccessor;
        ModelPath = modelPath ?? string.Empty;
        Viewport = new CabinetViewportViewModel();
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
        var validTargets = FaceTargets.Where(target => target.IsValid).ToDictionary(target => target.Id, StringComparer.Ordinal);
        if (validTargets.Count == 0 || _openDocumentsAccessor is null)
        {
            Viewport.FacePreviewModel = null;
            return;
        }

        var previewGroup = new Model3DGroup();
        foreach (var faceDocument in _openDocumentsAccessor()
            .Where(document => document.Document.DocumentType == EditorDocumentType.Face)
            .Select(document => document.GetFaceDocument()))
        {
            var targetId = NormalizeTargetId(faceDocument.AssignedCabinetFaceTargetId);
            if (targetId is null || !validTargets.TryGetValue(targetId, out var target))
            {
                continue;
            }

            var preview = _previewRenderer.RenderPreview(faceDocument);
            if (preview is null)
            {
                continue;
            }

            if (TryCreatePreviewGeometry(target.Target, preview, out var geometry))
            {
                previewGroup.Children.Add(geometry);
            }
        }

        Viewport.FacePreviewModel = previewGroup.Children.Count == 0 ? null : previewGroup;
    }

    private static bool TryCreatePreviewGeometry(CabinetFaceTarget target, BitmapSource bitmap, out GeometryModel3D geometry)
    {
        geometry = default!;
        if (!target.IsValid || target.Corners.Count != 4)
        {
            return false;
        }

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection(target.Corners),
            TriangleIndices = new Int32Collection(new[] { 0, 1, 2, 0, 2, 3 }),
            TextureCoordinates = new PointCollection(new[]
            {
                new Point(0, 0),
                new Point(1, 0),
                new Point(1, 1),
                new Point(0, 1)
            })
        };
        var material = new DiffuseMaterial(new ImageBrush(bitmap));
        geometry = new GeometryModel3D(mesh, material) { BackMaterial = material };
        return true;
    }

    private static string? NormalizeTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private bool CanLoad() => !IsLoading && !string.IsNullOrWhiteSpace(ModelPath);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
