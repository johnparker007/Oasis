using System.Windows.Media.Media3D;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Features.CabinetEditor.Services;

public sealed class CabinetModelLoadResult
{
    private CabinetModelLoadResult(bool succeeded, Model3DGroup? model, Rect3D bounds, IReadOnlyList<CabinetFaceTarget> faceTargets, IReadOnlyList<CabinetReflectionReceiverTarget> reflectionTargets, string? errorMessage)
    {
        Succeeded = succeeded;
        Model = model;
        Bounds = bounds;
        FaceTargets = faceTargets;
        ReflectionTargets = reflectionTargets;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }
    public Model3DGroup? Model { get; }
    public Rect3D Bounds { get; }
    public IReadOnlyList<CabinetFaceTarget> FaceTargets { get; }
    public IReadOnlyList<CabinetReflectionReceiverTarget> ReflectionTargets { get; }
    public string? ErrorMessage { get; }

    public static CabinetModelLoadResult Success(Model3DGroup model, IReadOnlyList<CabinetFaceTarget>? faceTargets = null, IReadOnlyList<CabinetReflectionReceiverTarget>? reflectionTargets = null) => new(true, model, model.Bounds, faceTargets ?? Array.Empty<CabinetFaceTarget>(), reflectionTargets ?? Array.Empty<CabinetReflectionReceiverTarget>(), null);
    public static CabinetModelLoadResult Failure(string errorMessage) => new(false, null, Rect3D.Empty, Array.Empty<CabinetFaceTarget>(), Array.Empty<CabinetReflectionReceiverTarget>(), errorMessage);
}
