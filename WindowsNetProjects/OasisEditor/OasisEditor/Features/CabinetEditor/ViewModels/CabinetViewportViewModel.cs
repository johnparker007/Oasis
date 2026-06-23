using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace OasisEditor.Features.CabinetEditor.ViewModels;

public sealed class CabinetViewportViewModel : INotifyPropertyChanged
{
    private Model3DGroup? _model;
    private Rect3D _modelBounds = Rect3D.Empty;
    private Point3D _cameraPosition;
    private Vector3D _cameraLookDirection;
    private Vector3D _cameraUpDirection = new(0, 1, 0);
    private double _cameraFieldOfView = 45d;

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
            OnPropertyChanged(nameof(AxisLength));
            OnPropertyChanged(nameof(XAxisEnd));
            OnPropertyChanged(nameof(YAxisEnd));
            OnPropertyChanged(nameof(ZAxisEnd));
        }
    }

    public double GridWidth => GetSceneHelperSize();
    public double GridLength => GetSceneHelperSize();
    public double GridMinorDistance => Math.Max(0.1, GetSceneHelperSize() / 20d);
    public double AxisLength => Math.Max(1d, GetSceneHelperSize() / 4d);
    public Point3D XAxisEnd => new(AxisLength, 0, 0);
    public Point3D YAxisEnd => new(0, AxisLength, 0);
    public Point3D ZAxisEnd => new(0, 0, AxisLength);

    public Point3D CameraPosition
    {
        get => _cameraPosition;
        set
        {
            if (_cameraPosition == value) return;
            _cameraPosition = value;
            OnPropertyChanged();
        }
    }

    public Vector3D CameraLookDirection
    {
        get => _cameraLookDirection;
        set
        {
            if (_cameraLookDirection == value) return;
            _cameraLookDirection = value;
            OnPropertyChanged();
        }
    }

    public Vector3D CameraUpDirection
    {
        get => _cameraUpDirection;
        set
        {
            if (_cameraUpDirection == value) return;
            _cameraUpDirection = value;
            OnPropertyChanged();
        }
    }

    public double CameraFieldOfView
    {
        get => _cameraFieldOfView;
        set
        {
            if (Math.Abs(_cameraFieldOfView - value) <= double.Epsilon) return;
            _cameraFieldOfView = value;
            OnPropertyChanged();
        }
    }
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
