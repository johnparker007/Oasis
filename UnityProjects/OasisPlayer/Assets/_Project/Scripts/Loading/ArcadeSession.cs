using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Loading
{
    public sealed class ArcadeMachineStation
    {
        public ArcadeMachineStation(string id, RuntimeMachine machine) { Id = id; Machine = machine; }
        public string Id { get; }
        public RuntimeMachine Machine { get; }
    }

    /// <summary>Owns one isolated loader/session per explicit placement; failures roll back the entire room.</summary>
    public sealed class ArcadeSession : IDisposable
    {
        private readonly List<MachinePreviewLoader> _loaders = new List<MachinePreviewLoader>();
        private readonly List<ArcadeMachineStation> _stations = new List<ArcadeMachineStation>();
        private GameObject _root;
        private bool _loading;
        private int _revision;
        public ArcadeLayoutManifest Layout { get; private set; }
        public IReadOnlyList<ArcadeMachineStation> Stations { get { return _stations; } }
        public async Task LoadAsync(string manifestPath)
        {
            if (_root != null || _loading) throw new InvalidOperationException("Arcade session is already loaded or loading.");
            _loading = true;
            var revision = _revision;
            string currentId = "layout";
            try
            {
                var path = Path.GetFullPath(manifestPath);
                Layout = RuntimeManifestJsonReader.ReadArcade(File.ReadAllText(path));
                Layout.Validate();
                var builds = new List<ResolvedRuntimeBuild>();
                foreach (var placement in Layout.machines)
                {
                    currentId = placement.id;
                    var dependency = ArcadeLayoutManifest.ResolveBuildPath(Path.GetDirectoryName(path), placement.build);
                    if (!RuntimeBuildLoader.TryLoad(dependency, out var build, out var error)) throw new InvalidDataException(error);
                    builds.Add(build);
                }
                _root = new GameObject("Arcade Machine sessions");
                var bounds = new List<Bounds>();
                for (var i = 0; i < Layout.machines.Length; i++)
                {
                    var placement = Layout.machines[i]; currentId = placement.id;
                    var root = new GameObject(placement.id); root.transform.SetParent(_root.transform, false);
                    root.transform.SetPositionAndRotation(placement.position.Value, Quaternion.Euler(0, placement.yaw, 0));
                    var loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader()); _loaders.Add(loader);
                    var machine = await loader.LoadAsync(builds[i], root.transform, false, false);
                    if (revision != _revision) { loader.Unload(); throw new OperationCanceledException("Arcade was disposed while loading."); }
                    bounds.Add(PlayableMachineRoom.MeasureCabinet(machine));
                    _stations.Add(new ArcadeMachineStation(placement.id, machine));
                }
                currentId = "layout";
                Layout.ValidateCabinetBounds(bounds);
                for (var i = 0; i < _loaders.Count; i++)
                {
                    currentId = Layout.machines[i].id;
                    _loaders[i].StartBehavior();
                    if (_loaders[i].OasisBehaviorFault != null) throw new InvalidDataException(_loaders[i].OasisBehaviorFault.ToString());
                }
            }
            catch (Exception exception)
            {
                Dispose();
                throw new InvalidOperationException("Arcade placement '" + currentId + "': " + exception.Message, exception);
            }
            finally { _loading = false; }
        }
        public void Dispose()
        {
            _revision++;
            if (_root != null) _root.SetActive(false);
            for (var i = _loaders.Count - 1; i >= 0; i--) _loaders[i].Unload();
            _loaders.Clear(); _stations.Clear();
            if (_root != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(_root); else UnityEngine.Object.DestroyImmediate(_root); }
            _root = null; Layout = null;
        }
    }
}
