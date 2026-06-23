using System.Windows.Media.Media3D;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class CabinetModelLoadResult
{
    private CabinetModelLoadResult(bool succeeded, Model3DGroup? model, Rect3D bounds, string? errorMessage)
    {
        Succeeded = succeeded;
        Model = model;
        Bounds = bounds;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }
    public Model3DGroup? Model { get; }
    public Rect3D Bounds { get; }
    public string? ErrorMessage { get; }

    public static CabinetModelLoadResult Success(Model3DGroup model) => new(true, model, model.Bounds, null);
    public static CabinetModelLoadResult Failure(string errorMessage) => new(false, null, Rect3D.Empty, errorMessage);
}
