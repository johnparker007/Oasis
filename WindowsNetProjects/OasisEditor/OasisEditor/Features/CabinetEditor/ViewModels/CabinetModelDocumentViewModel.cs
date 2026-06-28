using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetModelDocumentViewModel : INotifyPropertyChanged
{
    private readonly ICabinetModelLoader _modelLoader;
    private string _loadStatus = "No cabinet model loaded.";
    private string? _errorMessage;
    private bool _isLoading;

    public CabinetModelDocumentViewModel(ICabinetModelLoader modelLoader, string? modelPath = null)
    {
        _modelLoader = modelLoader;
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

    private bool CanLoad() => !IsLoading && !string.IsNullOrWhiteSpace(ModelPath);
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
