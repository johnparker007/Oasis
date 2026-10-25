using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using OasisPlayer.Fabric;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class FabricHostOptions
    {
        public string RuntimeDll;
        public string ImpactDll;
        public static FabricHostOptions Current = FromEnvironment();
        public static FabricHostOptions FromEnvironment()
        {
            return new FabricHostOptions { RuntimeDll = Environment.GetEnvironmentVariable("OASIS_FABRIC_RUNTIME_DLL"), ImpactDll = Environment.GetEnvironmentVariable("OASIS_AMBER_IMPACT_DLL") };
        }
        public void Validate()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new PlatformNotSupportedException("Fabric/Amber JPM Impact currently requires Windows x64.");
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("Fabric/Amber JPM Impact requires a 64-bit Player.");
            foreach (var path in new[] { RuntimeDll, ImpactDll })
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || !File.Exists(path))
                    throw new FileNotFoundException("Configure exact local Fabric and Amber Impact DLL paths using --fabric-runtime/--amber-impact or OASIS_FABRIC_RUNTIME_DLL/OASIS_AMBER_IMPACT_DLL.", path);
        }
    }

    /// <summary>Owns one Fabric runner and maps its immutable snapshots onto existing Unity device state.</summary>
    public sealed class RuntimeFabricBehavior : IDisposable
    {
        private readonly RuntimeMachine _machine;
        private readonly Dictionary<string, MachineInputDefinition> _inputs = new Dictionary<string, MachineInputDefinition>(StringComparer.Ordinal);
        private readonly int[] _reelSteps;
        private FabricMachineSnapshot _applied;
        private AudioSource _source; private AudioClip _clip; private bool _disposed, _reportedFault;
        public FabricMachineRunner Runner { get; }
        private RuntimeFabricBehavior(RuntimeMachine machine, byte[] configuration)
        {
            _machine = machine; _reelSteps = ReadReelSteps(configuration); Runner = new FabricMachineRunner();
            foreach (var input in machine.Build.Machine.inputs) if (input != null) { ValidateInput(input); _inputs.Add(input.id, input); }
        }
        public static async Task<RuntimeFabricBehavior> AttachAsync(RuntimeMachine machine, FabricHostOptions options)
        {
            if (machine.Build.Machine.runtime.platform != "Impact") throw new NotSupportedException("Player Fabric integration currently supports JPM Impact only; requested " + machine.Build.Machine.runtime.platform + ".");
            options = options ?? FabricHostOptions.FromEnvironment(); options.Validate();
            var descriptor = machine.Build.Machine.runtime.fabric;
            if (descriptor == null) throw new InvalidDataException("Impact package lacks its exported Fabric configuration and ROM dependencies; rebuild it in the current Editor.");
            var bytes = File.ReadAllBytes(machine.Build.FabricConfigurationPath);
            var behavior = new RuntimeFabricBehavior(machine, bytes);
            try
            {
                var resources = new List<FabricRomResource>();
                foreach (var rom in descriptor.roms) resources.Add(new FabricRomResource((FabricRomRole)rom.role, (uint)rom.slot, ArcadeLayoutManifest.ResolveBuildPath(machine.Build.BuildRoot, rom.path)));
                await behavior.Runner.StartAsync(options.RuntimeDll, new FabricLaunchRequest(descriptor.backendKind, descriptor.machineIdentifier, options.ImpactDll, resources, new FabricByteConfiguration(bytes)));
                if (!machine.IsActive) throw new OperationCanceledException("Machine unloaded during native startup.");
                if (behavior.Runner.Failure != null) throw new InvalidOperationException("Fabric startup failed.", behavior.Runner.Failure);
                machine.AttachBehaviorRuntime(behavior); machine.FabricBehavior = behavior; machine.Events.Subscribe(behavior.HandleEvent);
                var driver = machine.MachineSpace.gameObject.AddComponent<RuntimeFabricDriver>(); driver.Initialize(behavior);
                behavior.CreateAudio(); behavior.ApplyLatest();
                return behavior;
            }
            catch { behavior.Dispose(); throw; }
        }
        private void CreateAudio()
        {
            if (Application.isBatchMode || Runner.Audio == null) return;
            var format = Runner.AudioFormat;
            _clip = AudioClip.Create("Fabric JPM Impact PCM", (int)format.SampleRate, format.ChannelCount, (int)format.SampleRate, true, Runner.Audio.Read);
            _source = _machine.MachineSpace.gameObject.AddComponent<AudioSource>(); _source.clip = _clip; _source.loop = true;
            _source.spatialBlend = 1; _source.minDistance = 1; _source.maxDistance = 15; _source.dopplerLevel = 0; _source.Play();
        }
        public void ApplyLatest()
        {
            if (_disposed || !_machine.IsActive) return;
            if (Runner.Failure != null)
            {
                _machine.EmulationFault = Runner.Failure.Message;
                if (!_reportedFault) { _reportedFault = true; if (_source != null) _source.Stop(); Debug.LogError("Fabric machine '" + _machine.Build.Machine.displayName + "': " + _machine.EmulationFault); }
                return;
            }
            var snapshot = Runner.LatestSnapshot;
            if (snapshot == null || ReferenceEquals(snapshot, _applied)) return;
            ApplySnapshot(_machine, snapshot, _reelSteps); _applied = snapshot;
        }
        private void HandleEvent(RuntimeMachineEvent value)
        {
            if (_disposed || !Runner.IsRunning) return;
            var pressed = value as RuntimeInputPressedEvent; var released = value as RuntimeInputReleasedEvent;
            var id = pressed != null ? pressed.InputId : released != null ? released.InputId : null;
            if (id == null || !_inputs.TryGetValue(id, out var input)) return;
            Runner.SubmitInput(MapInput(input, pressed != null));
        }
        public static FabricInput MapInput(MachineInputDefinition input, bool active)
        {
            ValidateInput(input);
            if (input.coinInput || input.kind == 2) return new FabricInput(input.id, 0, FabricInputKind.Coin, active, (byte)input.coinChannel.Value, (byte)input.coinValue.Value);
            var index = int.Parse(input.buttonNumber, CultureInfo.InvariantCulture);
            return new FabricInput("oasis.switch." + index, index, FabricInputKind.Digital, active);
        }
        private static void ValidateInput(MachineInputDefinition input)
        {
            if (input.coinInput || input.kind == 2)
            {
                if (!input.coinChannel.HasValue || !input.coinValue.HasValue || input.coinChannel < 0 || input.coinChannel > 5 || input.coinValue < 0 || input.coinValue > 12)
                    throw new InvalidDataException("Coin input '" + input.id + "' requires channel 0–5 and denomination code 0–12.");
            }
            else if (!int.TryParse(input.buttonNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index > 255)
                throw new InvalidDataException("Digital input '" + input.id + "' requires a numeric switch mapping 0–255.");
        }
        public static void ApplySnapshot(RuntimeMachine machine, FabricMachineSnapshot snapshot, int[] reelSteps)
        {
            foreach (var lamp in snapshot.Lamps)
                machine.LampState.SetBrightness(lamp.NumericalIndex, lamp.LogicalState ? Math.Max(1, (int)Math.Round(lamp.Brightness * 255)) / 255f : 0);
            foreach (var reel in snapshot.Reels)
            {
                var steps = reel.NumericalIndex >= 0 && reel.NumericalIndex < reelSteps.Length ? reelSteps[reel.NumericalIndex] : 96;
                machine.ReelState.SetPosition(reel.NumericalIndex, NormalizeAmberReel(reel.Position, steps));
            }
            for (var ordinal = 0; ordinal < snapshot.CharacterDisplays.Count; ordinal++)
            {
                var display = snapshot.CharacterDisplays[ordinal]; var masks = new int[display.Characters.Length]; var brightness = new float[masks.Length];
                for (var i = 0; i < masks.Length; i++) { masks[i] = MapAlpha(display.Characters[i]) | (display.Attributes[i] << 16); brightness[i] = display.Brightness; }
                machine.SegmentDisplayState.SetMasks("alpha:" + (ordinal * 16), masks); machine.SegmentDisplayState.SetBrightness("alpha:" + (ordinal * 16), brightness);
            }
            var cells = new List<int>();
            foreach (var display in snapshot.SegmentDisplays) foreach (var mask in display.SegmentMasks) cells.Add(MapSeven(mask));
            for (var cell = 0; cell < cells.Count; cell++) machine.SegmentDisplayState.SetMasks("sevenSegment:" + cell, new[] { cells[cell] });
            // Grouped Face digits reference a base cell; preserve the Editor's contiguous-cell rule.
            foreach (var face in machine.Faces)
                foreach (var entry in face.Manifest.sevenSegmentDisplays ?? Array.Empty<FaceRuntimeSevenSegmentDisplayManifestEntry>())
                {
                    const string prefix = "sevenSegment:";
                    if (entry == null || entry.digitCount <= 1 || string.IsNullOrEmpty(entry.machineReference) || !entry.machineReference.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (!int.TryParse(entry.machineReference.Substring(prefix.Length), out var start) || start < 0) continue;
                    var masks = new int[Math.Min(256, entry.digitCount)];
                    for (var i = 0; i < masks.Length && start + i < cells.Count; i++) masks[i] = cells[start + i];
                    machine.SegmentDisplayState.SetMasks(entry.machineReference, masks);
                }
        }
        public static float NormalizeAmberReel(int position, int steps)
        {
            if (steps < 1 || steps > 4096) throw new ArgumentOutOfRangeException(nameof(steps));
            var wrapped = ((position % steps) + steps) % steps; return ((steps - wrapped) % steps) * 96f / steps;
        }
        public static int[] ReadReelSteps(byte[] bytes)
        {
            if (!BitConverter.IsLittleEndian || bytes == null || bytes.Length != 648 || BitConverter.ToUInt32(bytes, 0) != 0x32424146 || BitConverter.ToUInt32(bytes, 4) != 648 || BitConverter.ToUInt32(bytes, 8) != 2)
                throw new InvalidDataException("Impact configuration must be the Editor's 648-byte FAB2 v2 block.");
            var steps = new[] { 96, 96, 96, 96, 96, 96, 96, 96 }; var count = BitConverter.ToUInt32(bytes, 24);
            if (count > 8) throw new InvalidDataException("Impact configuration has too many reels.");
            for (var i = 0; i < count; i++)
            {
                var offset = 32 + i * 24; var index = BitConverter.ToUInt32(bytes, offset); var value = BitConverter.ToUInt32(bytes, offset + 8);
                if (index > 7 || value < 1 || value > 4096) throw new InvalidDataException("Impact configuration reel index/steps are invalid.");
                steps[index] = (int)value;
            }
            return steps;
        }
        public static int MapAlpha(uint mask)
        {
            var map = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 10, 14, 9, 15, 11, 12, 8, 13 }; var result = 0;
            for (var bit = 0; bit < map.Length; bit++) if ((mask & (1u << bit)) != 0) result |= 1 << map[bit]; return result;
        }
        public static int MapSeven(ulong mask) { var result = 0; for (var bit = 0; bit < 8; bit++) if ((mask & (1UL << bit)) != 0) result |= 1 << (7 - bit); return result; }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _machine.Events.Unsubscribe(HandleEvent);
            if (_source != null) { _source.Stop(); _source.clip = null; UnityEngine.Object.Destroy(_source); }
            Runner.Dispose(); if (_clip != null) UnityEngine.Object.Destroy(_clip);
        }
    }
    public sealed class RuntimeFabricDriver : MonoBehaviour
    {
        private RuntimeFabricBehavior _behavior;
        public void Initialize(RuntimeFabricBehavior behavior) { _behavior = behavior; }
        private void Update() { if (_behavior != null) _behavior.ApplyLatest(); }
    }
}
