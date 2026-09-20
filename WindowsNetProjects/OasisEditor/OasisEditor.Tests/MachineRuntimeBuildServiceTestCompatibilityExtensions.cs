using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Progress;

namespace OasisEditor.Tests;

/// <summary>
/// Keeps the pre-Phase-1 test fixtures concise while always exercising the public
/// Machine-rooted build contract. This is test code, not a production fallback API.
/// </summary>
internal static class MachineRuntimeBuildServiceTestCompatibilityExtensions
{
    public static MachineRuntimeBuildResult BuildFromCabinetDocument(this MachineRuntimeBuildService service, EditorProject project, string cabinetManifestPath, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        Assert.True(CabinetDocumentStorage.TryRead(File.ReadAllText(cabinetManifestPath), out var cabinet));
        return service.BuildFromCabinetDocument(project, cabinetManifestPath, cabinet, progress, cancellationToken);
    }

    public static MachineRuntimeBuildResult BuildFromCabinetDocument(this MachineRuntimeBuildService service, EditorProject project, string cabinetManifestPath, CabinetDocument cabinet, IEditorProgressReporter progress, CancellationToken cancellationToken)
    {
        var cabinetName = ProjectAssetPathService.GetPackageAssetNameFromManifestPath(cabinetManifestPath, EditorAssetType.Cabinet3D) ?? "Machine";
        var machineDirectory = Path.Combine(project.AssetsDirectory, "Machines", cabinetName);
        var machinePath = Path.Combine(machineDirectory, ProjectAssetPathService.MachineManifestFileName);
        Directory.CreateDirectory(machineDirectory);
        var machine = MachineDocument.Create(cabinetName) with
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString("D"),
            CabinetAssetPath = new ProjectAssetPathService().ToProjectRelativePath(project, cabinetManifestPath),
            SurfaceAssignments = (cabinet.FaceAssignments ?? []).Select(value => new MachineSurfaceAssignment(value.TargetId, value.FaceAssetPath)).ToArray(),
            ReelAssignments = (cabinet.ReelAssignments ?? []).Select(value => new MachineReelAssignment(value.MachineReelReference, value.ReelSpecificationId)).ToArray()
        };
        File.WriteAllText(machinePath, MachineDocumentStorage.Write(machine));
        File.WriteAllText(cabinetManifestPath, CabinetDocumentStorage.Serialize(cabinet));
        return service.BuildFromMachineDocument(project, machinePath, progress, cancellationToken);
    }
}
