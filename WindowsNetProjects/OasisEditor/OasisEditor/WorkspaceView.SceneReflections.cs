using System.Numerics;
using Oasis.Rendering3D;
using OasisEditor.Avalonia.Rendering3D;
using OasisEditor.Features.CabinetEditor.Models;
using OasisEditor.Features.CabinetEditor.Services;

namespace OasisEditor.Avalonia;

public sealed partial class WorkspaceView
{
    private IReadOnlyList<string> AttachSceneReflections(Portable3DCompositionResult scene, List<SceneRenderMesh> meshes)
    {
        var diagnostics = new List<string>();
        if (scene.CabinetDocument is not { Reflections.Length: > 0 } cabinet) return diagnostics;
        // Automatic planes follow the actual imported target; editing a model never leaves stale projection geometry.
        cabinet = cabinet with { Reflections = cabinet.Reflections.Select(definition => definition with
        {
            Sources = definition.Sources.Select(source => source.PlaneSource == CabinetReflectionPlaneSource.Automatic
                && scene.CabinetFaceTargets.FirstOrDefault(t => t.Id == source.SourceSurfaceTargetId) is { } target
                && CabinetReflectionPlaneDeriver.TryDerive(target, out var plane, out _) ? source with { Plane = plane } : source).ToArray()
        }).ToArray() };
        var masks = new Dictionary<string, SceneTexture>();
        foreach (var mask in cabinet.Reflections.Select(r => r.VisibilityMask).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
        {
            try
            {
                var manifest = _sceneKind == "Cabinet3D" ? _cabinets.FilePath : _machines.Document?.CabinetAsset is { } reference
                    ? new AssetReferenceResolver().Resolve(_session.RequireEditorProject(), SceneLibraryRoot, reference) : null;
                if (manifest is null) throw new InvalidOperationException("Cabinet package is unavailable.");
                var path = WorkspacePathPolicy.ResolveConfined(Path.GetDirectoryName(manifest)!, mask!, requireExists: true);
                masks[mask!] = new(File.ReadAllBytes(path), WrapS:33071, WrapT:33071);
            }
            catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException)
            { diagnostics.Add("Reflection mask: " + error.Message); }
        }
        for (var i = 0; i < meshes.Count; i++)
        {
            var mesh = meshes[i];
            if (mesh.Kind != "visual") continue;
            var source = scene.Meshes.FirstOrDefault(part => part.OwnerId == "cabinet" && mesh.Id.StartsWith(part.OwnerId + "/" + part.NodeId + "/", StringComparison.Ordinal));
            if (source is null || !int.TryParse(mesh.Id.Split('/').Last(), out var slot)) continue;
            if (CabinetReflectionAdapter.TryAttach(mesh,source.NodePath,slot,cabinet,scene.CabinetIntrinsicTransform,
                _reflectionBaseTextures,_reflectionLampTextures,masks,out var attached,out var diagnostic)) meshes[i] = attached;
            else if (diagnostic is not null) diagnostics.Add(diagnostic);
        }
        return diagnostics.Distinct().ToArray();
    }
}
