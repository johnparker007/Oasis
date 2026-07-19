using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeMachineLampUpdater : MonoBehaviour
    {
        public RuntimeMachine Machine { get; private set; }

        public void Initialize(RuntimeMachine machine)
        {
            Machine = machine;
        }

        private void LateUpdate()
        {
            if (Machine == null) return;
            Machine.ApplyDynamicState();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public enum RuntimeLampDiagnosticMode
    {
        Off,
        AutomaticSweep,
        Manual
    }

    public enum RuntimeLampDiagnosticStage
    {
        Off,
        AllOn,
        AllOff,
        Sweep,
        Complete
    }

    public sealed class RuntimeLampDiagnosticSequence
    {
        private RuntimeLampDiagnosticSettings _settings;
        private float _stageElapsedSeconds;
        private int _completedFlashOnCount;
        private int _sweepLamp;
        private bool _sweepStarted;
        private bool _isRunning;

        public RuntimeLampDiagnosticSequence(RuntimeLampDiagnosticSettings settings)
        {
            Configure(settings);
        }

        public RuntimeLampDiagnosticSettings Settings { get { return _settings; } }
        public RuntimeLampDiagnosticStage CurrentStage { get; private set; }
        public int CurrentLamp { get { return CurrentStage == RuntimeLampDiagnosticStage.Sweep ? _sweepLamp : 0; } }
        public int CompletedCycles { get; private set; }
        public bool IsRunning { get { return _isRunning; } }
        public bool IsSweepStarted { get { return _sweepStarted; } }

        public void Configure(RuntimeLampDiagnosticSettings settings)
        {
            _settings = settings.Clamped();
            Reset();
        }

        public void Reset()
        {
            _stageElapsedSeconds = 0f;
            _completedFlashOnCount = 0;
            _sweepLamp = _settings.FirstLamp;
            _sweepStarted = false;
            _isRunning = false;
            CurrentStage = RuntimeLampDiagnosticStage.Off;
            CompletedCycles = 0;
        }

        public bool Start(RuntimeLampState lampState)
        {
            if (_settings.Mode != RuntimeLampDiagnosticMode.AutomaticSweep || lampState == null)
            {
                CurrentStage = RuntimeLampDiagnosticStage.Off;
                _isRunning = false;
                return false;
            }

            _isRunning = true;
            _stageElapsedSeconds = 0f;
            _completedFlashOnCount = 0;
            _sweepLamp = _settings.FirstLamp;
            _sweepStarted = false;
            CurrentStage = RuntimeLampDiagnosticStage.AllOn;
            lampState.SetAllBrightness(1f);
            return true;
        }

        public RuntimeLampDiagnosticTransition Advance(RuntimeLampState lampState, float unscaledDeltaSeconds)
        {
            if (!_isRunning || lampState == null || _settings.Mode != RuntimeLampDiagnosticMode.AutomaticSweep)
            {
                return RuntimeLampDiagnosticTransition.None;
            }

            _stageElapsedSeconds += Math.Max(0f, unscaledDeltaSeconds);
            switch (CurrentStage)
            {
                case RuntimeLampDiagnosticStage.AllOn:
                    if (_stageElapsedSeconds < _settings.AllOnSeconds) return RuntimeLampDiagnosticTransition.None;
                    _stageElapsedSeconds = 0f;
                    _completedFlashOnCount++;
                    CurrentStage = RuntimeLampDiagnosticStage.AllOff;
                    lampState.ClearAll();
                    return RuntimeLampDiagnosticTransition.AllOff;

                case RuntimeLampDiagnosticStage.AllOff:
                    if (_stageElapsedSeconds < _settings.AllOffSeconds) return RuntimeLampDiagnosticTransition.None;
                    _stageElapsedSeconds = 0f;
                    if (_completedFlashOnCount < _settings.AllFlashCount)
                    {
                        CurrentStage = RuntimeLampDiagnosticStage.AllOn;
                        lampState.SetAllBrightness(1f);
                        return RuntimeLampDiagnosticTransition.AllOn;
                    }

                    CurrentStage = RuntimeLampDiagnosticStage.Sweep;
                    _sweepLamp = _settings.FirstLamp;
                    _sweepStarted = true;
                    lampState.ClearAll();
                    lampState.SetBrightness(_sweepLamp, 1f);
                    return RuntimeLampDiagnosticTransition.BeginSweep;

                case RuntimeLampDiagnosticStage.Sweep:
                    if (_stageElapsedSeconds < _settings.SecondsPerLamp) return RuntimeLampDiagnosticTransition.None;
                    _stageElapsedSeconds = 0f;
                    if (_sweepLamp >= _settings.LastLamp)
                    {
                        lampState.ClearAll();
                        CompletedCycles++;
                        if (!_settings.Repeat)
                        {
                            CurrentStage = RuntimeLampDiagnosticStage.Complete;
                            _isRunning = false;
                            return RuntimeLampDiagnosticTransition.Complete;
                        }

                        _completedFlashOnCount = 0;
                        _sweepLamp = _settings.FirstLamp;
                        _sweepStarted = false;
                        CurrentStage = RuntimeLampDiagnosticStage.AllOn;
                        lampState.SetAllBrightness(1f);
                        return RuntimeLampDiagnosticTransition.RepeatAllOn;
                    }

                    lampState.ClearAll();
                    _sweepLamp++;
                    lampState.SetBrightness(_sweepLamp, 1f);
                    return RuntimeLampDiagnosticTransition.SweepLamp;
            }

            return RuntimeLampDiagnosticTransition.None;
        }
    }

    public enum RuntimeLampDiagnosticTransition
    {
        None,
        AllOn,
        AllOff,
        BeginSweep,
        SweepLamp,
        RepeatAllOn,
        Complete
    }

    [Serializable]
    public struct RuntimeLampDiagnosticSettings
    {
        public RuntimeLampDiagnosticMode Mode;
        public int FirstLamp;
        public int LastLamp;
        public float SecondsPerLamp;
        public float AllOnSeconds;
        public float AllOffSeconds;
        public int AllFlashCount;
        public bool Repeat;

        public static RuntimeLampDiagnosticSettings DefaultAutomatic()
        {
            return new RuntimeLampDiagnosticSettings
            {
                Mode = RuntimeLampDiagnosticMode.AutomaticSweep,
                FirstLamp = RuntimeLampState.MinimumLampNumber,
                LastLamp = RuntimeLampState.MaximumLampNumber,
                SecondsPerLamp = 0.1f,
                AllOnSeconds = 0.5f,
                AllOffSeconds = 0.5f,
                AllFlashCount = 2,
                Repeat = true
            };
        }

        public RuntimeLampDiagnosticSettings Clamped()
        {
            var first = Mathf.Clamp(FirstLamp, RuntimeLampState.MinimumLampNumber, RuntimeLampState.MaximumLampNumber);
            var last = Mathf.Clamp(LastLamp, RuntimeLampState.MinimumLampNumber, RuntimeLampState.MaximumLampNumber);
            if (last < first)
            {
                var temp = first;
                first = last;
                last = temp;
            }

            return new RuntimeLampDiagnosticSettings
            {
                Mode = Mode,
                FirstLamp = first,
                LastLamp = last,
                SecondsPerLamp = Mathf.Max(0.001f, SecondsPerLamp),
                AllOnSeconds = Mathf.Max(0.001f, AllOnSeconds),
                AllOffSeconds = Mathf.Max(0.001f, AllOffSeconds),
                AllFlashCount = Mathf.Max(1, AllFlashCount),
                Repeat = Repeat
            };
        }
    }

    public sealed class RuntimeLampDevelopmentControls : MonoBehaviour
    {
        [SerializeField] private RuntimeLampDiagnosticMode mode = RuntimeLampDiagnosticMode.AutomaticSweep;
        [SerializeField] private int firstLamp = RuntimeLampState.MinimumLampNumber;
        [SerializeField] private int lastLamp = RuntimeLampState.MaximumLampNumber;
        [SerializeField] private float secondsPerLamp = 0.1f;
        [SerializeField] private float allOnSeconds = 0.5f;
        [SerializeField] private float allOffSeconds = 0.5f;
        [SerializeField] private int allFlashCount = 2;
        [SerializeField] private bool repeat = true;
        [SerializeField] private int manualLampNumber = 1;
        [SerializeField] private RuntimeLampDiagnosticStage currentStage;
        [SerializeField] private int currentLamp;
        [SerializeField] private bool running;
        [SerializeField] private int completedCycles;

        private RuntimeMachine _machine;
        private RuntimeLampDiagnosticSequence _sequence;
        public void Initialize(RuntimeMachine machine)
        {
            _machine = machine;
            _sequence = new RuntimeLampDiagnosticSequence(CreateSettings());
            RuntimeLampDiagnosticReporter.LogReady(machine);
            if (_sequence.Start(machine != null ? machine.LampState : null))
            {
                Debug.Log($"Oasis lamp diagnostic started: all-flash x{_sequence.Settings.AllFlashCount}, then sweep lamps {_sequence.Settings.FirstLamp}–{_sequence.Settings.LastLamp} at {_sequence.Settings.SecondsPerLamp:0.###}s per lamp.");
                Debug.Log("Oasis lamp diagnostic: all lamps ON");
            }
            RefreshStatus();
        }

        private void Update()
        {
            if (_machine == null) return;
            if (_sequence == null) _sequence = new RuntimeLampDiagnosticSequence(CreateSettings());

            if (mode == RuntimeLampDiagnosticMode.Manual)
            {
                RunManualControls();
                RefreshStatus();
                return;
            }

            if (mode == RuntimeLampDiagnosticMode.Off)
            {
                RefreshStatus();
                return;
            }

            var transition = _sequence.Advance(_machine.LampState, Time.unscaledDeltaTime);
            LogTransition(transition);
            RefreshStatus();
        }

        private RuntimeLampDiagnosticSettings CreateSettings()
        {
            return new RuntimeLampDiagnosticSettings
            {
                Mode = mode,
                FirstLamp = firstLamp,
                LastLamp = lastLamp,
                SecondsPerLamp = secondsPerLamp,
                AllOnSeconds = allOnSeconds,
                AllOffSeconds = allOffSeconds,
                AllFlashCount = allFlashCount,
                Repeat = repeat
            }.Clamped();
        }

        private void RunManualControls()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) manualLampNumber = Mathf.Max(RuntimeLampState.MinimumLampNumber, manualLampNumber - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) manualLampNumber = Mathf.Min(RuntimeLampState.MaximumLampNumber, manualLampNumber + 1);
            if (Input.GetKeyDown(KeyCode.Alpha0)) _machine.LampState.SetBrightness(manualLampNumber, 0f);
            if (Input.GetKeyDown(KeyCode.Alpha1)) _machine.LampState.SetBrightness(manualLampNumber, 0.25f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _machine.LampState.SetBrightness(manualLampNumber, 0.5f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _machine.LampState.SetBrightness(manualLampNumber, 1f);
            if (Input.GetKeyDown(KeyCode.C)) _machine.LampState.ClearAll();
        }

        private void LogTransition(RuntimeLampDiagnosticTransition transition)
        {
            switch (transition)
            {
                case RuntimeLampDiagnosticTransition.AllOn:
                    Debug.Log("Oasis lamp diagnostic: all lamps ON");
                    break;
                case RuntimeLampDiagnosticTransition.AllOff:
                    Debug.Log("Oasis lamp diagnostic: all lamps OFF");
                    break;
                case RuntimeLampDiagnosticTransition.BeginSweep:
                    Debug.Log("Oasis lamp diagnostic: beginning sweep");
                    Debug.Log($"Oasis lamp diagnostic: sweeping lamp {_sequence.CurrentLamp}");
                    break;
                case RuntimeLampDiagnosticTransition.SweepLamp:
                    if (_sequence.CurrentLamp % 32 == 0 || _sequence.CurrentLamp == _sequence.Settings.LastLamp)
                    {
                        Debug.Log($"Oasis lamp diagnostic: sweeping lamp {_sequence.CurrentLamp}");
                    }
                    break;
                case RuntimeLampDiagnosticTransition.RepeatAllOn:
                    Debug.Log("Oasis lamp diagnostic: sequence repeating");
                    Debug.Log("Oasis lamp diagnostic: all lamps ON");
                    break;
                case RuntimeLampDiagnosticTransition.Complete:
                    Debug.Log("Oasis lamp diagnostic: complete");
                    break;
            }
        }

        private void RefreshStatus()
        {
            if (_sequence == null)
            {
                currentStage = RuntimeLampDiagnosticStage.Off;
                currentLamp = 0;
                running = false;
                completedCycles = 0;
                return;
            }

            currentStage = _sequence.CurrentStage;
            currentLamp = _sequence.CurrentLamp;
            running = _sequence.IsRunning;
            completedCycles = _sequence.CompletedCycles;
        }

        private void OnDisable()
        {
            if (_machine != null) _machine.LampState.ClearAll();
        }
    }

    public static class RuntimeLampDiagnosticReporter
    {
        public static string BuildReadySummary(RuntimeMachine machine)
        {
            if (machine == null) return "Oasis lamp diagnostic ready: no machine loaded";

            var rendered = 0;
            var bound = 0;
            foreach (var face in machine.Faces)
            {
                if (face == null || face.RenderBinding == null || face.RenderBinding.RuntimeMaterial == null) continue;
                rendered++;
                var material = face.RenderBinding.RuntimeMaterial;
                if (material.HasProperty(RuntimeFaceShaderProperties.LampStateTexture)
                    && material.GetTexture(RuntimeFaceShaderProperties.LampStateTexture) == machine.LampStateTexture.Texture)
                {
                    bound++;
                }
            }

            var texture = machine.LampStateTexture.Texture;
            var textureText = texture != null ? $"{texture.width}x{texture.height}" : "missing";
            return $"Oasis lamp diagnostic ready:\nFaces loaded: {machine.Faces.Count}\nFaces rendered: {rendered}\nLamp-state texture: {textureText}\nFace materials bound to lamp state: {bound}";
        }

        public static void LogReady(RuntimeMachine machine)
        {
            Debug.Log(BuildReadySummary(machine));
            if (machine == null) return;
            var faceIndex = 1;
            foreach (var face in machine.Faces)
            {
                if (face == null || face.RenderBinding == null) continue;
                Debug.Log(RuntimeFaceLookupDiagnostic.BuildSummary(face, faceIndex));
                faceIndex++;
            }
        }
    }

    public struct RuntimeFaceLookupDiagnosticSummary
    {
        public bool HasLampIdData;
        public bool HasLampWeightData;
        public int AssignedPixels;
        public int MinimumLampId;
        public int MaximumLampId;
        public int InvalidIdCount;
        public bool HasNonZeroWeights;
    }

    public static class RuntimeFaceLookupDiagnostic
    {
        public static RuntimeFaceLookupDiagnosticSummary Analyze(RuntimeFace face)
        {
            var summary = new RuntimeFaceLookupDiagnosticSummary();
            if (face == null || face.LampIds0 == null || face.LampIds0.Texture == null)
            {
                return summary;
            }

            summary.HasLampIdData = true;
            summary.HasLampWeightData = face.LampWeights0 != null && face.LampWeights0.Texture != null;
            if (!summary.HasLampWeightData) return summary;

            var ids = face.LampIds0.Texture.GetPixels32();
            var weights = face.LampWeights0.Texture.GetPixels32();
            var length = Math.Min(ids.Length, weights.Length);
            summary.MinimumLampId = RuntimeLampState.MaximumLampNumber + 1;
            for (var i = 0; i < length; i++)
            {
                var assigned = false;
                AnalyzeChannel(ids[i].r, weights[i].r, ref summary, ref assigned);
                AnalyzeChannel(ids[i].g, weights[i].g, ref summary, ref assigned);
                AnalyzeChannel(ids[i].b, weights[i].b, ref summary, ref assigned);
                if (assigned) summary.AssignedPixels++;
            }

            if (summary.MinimumLampId > RuntimeLampState.MaximumLampNumber) summary.MinimumLampId = 0;
            return summary;
        }

        public static string BuildSummary(RuntimeFace face, int faceIndex)
        {
            var summary = Analyze(face);
            var range = summary.MinimumLampId > 0 ? $"{summary.MinimumLampId}–{summary.MaximumLampId}" : "none";
            return $"Face {faceIndex} lamp lookup:\nID data: {FormatBool(summary.HasLampIdData)}\nWeight data: {FormatBool(summary.HasLampWeightData)}\nassigned pixels: {summary.AssignedPixels}\nlamp range: {range}\ninvalid IDs: {summary.InvalidIdCount}\nnon-zero weights: {FormatBool(summary.HasNonZeroWeights)}";
        }

        private static void AnalyzeChannel(byte lampId, byte weight, ref RuntimeFaceLookupDiagnosticSummary summary, ref bool assigned)
        {
            if (lampId == 0) return;
            assigned = true;
            if (lampId < RuntimeLampState.MinimumLampNumber || lampId > RuntimeLampState.MaximumLampNumber)
            {
                summary.InvalidIdCount++;
                return;
            }

            if (lampId < summary.MinimumLampId) summary.MinimumLampId = lampId;
            if (lampId > summary.MaximumLampId) summary.MaximumLampId = lampId;
            if (weight > 0) summary.HasNonZeroWeights = true;
        }

        private static string FormatBool(bool value)
        {
            return value ? "yes" : "no";
        }
    }
#endif
}
