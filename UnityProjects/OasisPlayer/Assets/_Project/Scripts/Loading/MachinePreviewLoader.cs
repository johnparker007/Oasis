using System;
using System.Linq;
using System.Threading.Tasks;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Loading;

public sealed class MachinePreviewLoader
{
    private readonly ICabinetModelLoader _modelLoader;
    private GameObject _current;
    public MachinePreviewLoader(ICabinetModelLoader modelLoader) { _modelLoader = modelLoader; }
    public async Task LoadAsync(ResolvedRuntimeBuild build)
    {
        Unload();
        var spawns = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == null && t.name == "MachineSpawn").ToArray();
        if (spawns.Length != 1) throw new InvalidOperationException(spawns.Length == 0 ? "MachinePreview scene is missing root transform 'MachineSpawn'." : "MachinePreview scene contains duplicate root transforms named 'MachineSpawn'.");
        var correctionRoot = new GameObject("OasisCabinetCorrectionRoot");
        correctionRoot.transform.SetParent(spawns[0], false);
        correctionRoot.transform.localScale = Vector3.one * Mathf.Max(0.0001f, build.Cabinet.scale);
        correctionRoot.transform.localRotation = build.Cabinet.upAxis == "Z" ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
        _current = correctionRoot;
        await _modelLoader.LoadAsync(build.GlbPath, correctionRoot.transform);
    }
    public void Unload() { if (_current != null) { _modelLoader.Unload(_current); _current = null; } }
}
