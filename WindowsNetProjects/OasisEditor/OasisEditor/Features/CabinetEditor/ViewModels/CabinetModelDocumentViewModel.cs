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
    private readonly DocumentTabViewModel _document;
    private readonly Func<IReadOnlyList<DocumentTabViewModel>>? _openDocumentsAccessor;
    private readonly FaceDocumentArtworkPreviewRenderer _previewRenderer = new();
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
        }
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

    public void RefreshFromDocument(CabinetDocument document)
    {
        OnPropertyChanged(nameof(SelectedFrontSide));
        OnPropertyChanged(nameof(SelectedFaceRotation));
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

            var targetOverride = _document.GetCabinetDocument().GetTargetOverride(target.Id);
            if (TryCreatePreviewGeometry(target.Target, targetOverride, preview, out var geometry))
            {
                previewGroup.Children.Add(geometry);
            }
        }

        Viewport.FacePreviewModel = previewGroup.Children.Count == 0 ? null : previewGroup;
    }

    private static bool TryCreatePreviewGeometry(CabinetFaceTarget target, CabinetTargetOverride targetOverride, BitmapSource bitmap, out GeometryModel3D geometry)
    {
        geometry = default!;
        if (!target.IsValid || target.Corners.Count != 4)
        {
            return false;
        }

        var positions = GetRenderQuadCorners(target.Corners, targetOverride);
        var triangleIndices = CabinetTargetOverride.NormalizeFrontSide(targetOverride.FrontSide) == CabinetTargetOverride.InvertedFrontSide
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
        var material = new DiffuseMaterial(new ImageBrush(bitmap));
        geometry = new GeometryModel3D(mesh, material);
        return true;
    }

    private static Point3D[] GetRenderQuadCorners(IReadOnlyList<Point3D> sourceCorners, CabinetTargetOverride targetOverride) => CabinetTargetOverride.NormalizeFaceRotation(targetOverride.FaceRotation) switch
    {
        90 => new[] { sourceCorners[3], sourceCorners[0], sourceCorners[1], sourceCorners[2] },
        180 => new[] { sourceCorners[2], sourceCorners[3], sourceCorners[0], sourceCorners[1] },
        270 => new[] { sourceCorners[1], sourceCorners[2], sourceCorners[3], sourceCorners[0] },
        _ => new[] { sourceCorners[0], sourceCorners[1], sourceCorners[2], sourceCorners[3] }
    };

    private static string? NormalizeTargetId(string? targetId) => string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim();

    private bool CanLoad() => !IsLoading && !string.IsNullOrWhiteSpace(ModelPath);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
