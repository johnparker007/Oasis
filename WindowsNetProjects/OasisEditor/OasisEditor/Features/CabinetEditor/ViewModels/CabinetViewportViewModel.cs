using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetViewportViewModel : INotifyPropertyChanged
{
    private Model3DGroup? _model;
    private Rect3D _modelBounds = Rect3D.Empty;

    public CabinetViewportViewModel()
    {
        ResetCameraCommand = new RelayCommand(ResetCamera);
        ResetCamera();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Model3DGroup? Model
    {
        get => _model;
        set
        {
            if (ReferenceEquals(_model, value)) return;
            _model = value;
            OnPropertyChanged();
            ModelBounds = value?.Bounds ?? Rect3D.Empty;
            ResetCamera();
        }
    }

    public Rect3D ModelBounds
    {
        get => _modelBounds;
        private set
        {
            _modelBounds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GridWidth));
            OnPropertyChanged(nameof(GridLength));
            OnPropertyChanged(nameof(GridMinorDistance));
        }
    }

    public double GridWidth => GetSceneHelperSize();
    public double GridLength => GetSceneHelperSize();
    public double GridMinorDistance => Math.Max(0.1, GetSceneHelperSize() / 20d);

    public Point3D CameraPosition { get; private set; }
    public Vector3D CameraLookDirection { get; private set; }
    public Vector3D CameraUpDirection { get; private set; } = new(0, 1, 0);
    public double CameraFieldOfView { get; private set; } = 45d;
    public ICommand ResetCameraCommand { get; }

    private void ResetCamera()
    {
        var bounds = ModelBounds;
        var center = bounds.IsEmpty
            ? new Point3D(0, 0, 0)
            : new Point3D(bounds.X + bounds.SizeX / 2d, bounds.Y + bounds.SizeY / 2d, bounds.Z + bounds.SizeZ / 2d);
        var radius = bounds.IsEmpty
            ? 5d
            : Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ);
        if (radius <= 0d) radius = 5d;

        var distance = radius * 2.5d;
        CameraPosition = new Point3D(center.X + distance, center.Y + distance * 0.65d, center.Z + distance);
        CameraLookDirection = center - CameraPosition;
        CameraUpDirection = new Vector3D(0, 1, 0);
        CameraFieldOfView = 45d;
        OnPropertyChanged(nameof(CameraPosition));
        OnPropertyChanged(nameof(CameraLookDirection));
        OnPropertyChanged(nameof(CameraUpDirection));
        OnPropertyChanged(nameof(CameraFieldOfView));
    }

    private double GetSceneHelperSize()
    {
        if (ModelBounds.IsEmpty) return 10d;
        var max = Math.Max(Math.Max(ModelBounds.SizeX, ModelBounds.SizeY), ModelBounds.SizeZ);
        return Math.Max(1d, Math.Ceiling(max * 2d));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
