using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeMachineStateUpdater : MonoBehaviour
    {
        public RuntimeMachine Machine { get; private set; }
        public void Initialize(RuntimeMachine machine) { Machine = machine; }
        private void LateUpdate() { if (Machine != null) Machine.ApplyDynamicState(); }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public enum RuntimeLampDiagnosticMode { Off, AdvancingPattern, Manual }

    [Serializable]
    public struct RuntimeLampDiagnosticSettings
    {
        public RuntimeLampDiagnosticMode Mode;
        public float SecondsPerShift;
        public static RuntimeLampDiagnosticSettings DefaultAutomatic() { return new RuntimeLampDiagnosticSettings { Mode = RuntimeLampDiagnosticMode.AdvancingPattern, SecondsPerShift = 0.5f }; }
        public RuntimeLampDiagnosticSettings Clamped() { return new RuntimeLampDiagnosticSettings { Mode = Mode, SecondsPerShift = Mathf.Max(0.001f, SecondsPerShift) }; }
    }

    public sealed class RuntimeLampDiagnosticSequence
    {
        private static readonly float[] Pattern = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, .2f, .4f, .6f, .8f, 1f };
        private RuntimeLampDiagnosticSettings _settings;
        private float _elapsed;
        public RuntimeLampDiagnosticSequence(RuntimeLampDiagnosticSettings settings) { Configure(settings); }
        public RuntimeLampDiagnosticSettings Settings { get { return _settings; } }
        public int ShiftOffset { get; private set; }
        public float ElapsedSeconds { get { return _elapsed; } }
        public bool IsRunning { get; private set; }
        public void Configure(RuntimeLampDiagnosticSettings settings) { _settings = settings.Clamped(); Reset(); }
        public void Reset() { _elapsed = 0f; ShiftOffset = 0; IsRunning = false; }
        public bool Start(RuntimeLampState state) { IsRunning = state != null && _settings.Mode == RuntimeLampDiagnosticMode.AdvancingPattern; if (IsRunning) Apply(state); return IsRunning; }
        public int Advance(RuntimeLampState state, float unscaledDeltaSeconds)
        {
            if (!IsRunning || state == null || _settings.Mode != RuntimeLampDiagnosticMode.AdvancingPattern) return 0;
            _elapsed += Math.Max(0f, unscaledDeltaSeconds);
            var advances = Mathf.FloorToInt(_elapsed / _settings.SecondsPerShift);
            if (advances == 0) return 0;
            _elapsed -= advances * _settings.SecondsPerShift;
            ShiftOffset = PositiveModulo(ShiftOffset + advances, Pattern.Length);
            Apply(state);
            return advances;
        }
        public void Apply(RuntimeLampState state)
        {
            if (state == null) return;
            // Increasing shift moves the five-level leading edge toward increasing lamp numbers.
            for (var lamp = RuntimeLampState.MinimumLampNumber; lamp <= RuntimeLampState.MaximumLampNumber; lamp++)
                state.SetBrightness(lamp, Pattern[PositiveModulo((lamp - RuntimeLampState.MinimumLampNumber) - ShiftOffset, Pattern.Length)]);
        }
        public static int PositiveModulo(int value, int divisor) { var result = value % divisor; return result < 0 ? result + divisor : result; }
    }

    public sealed class RuntimeLampDevelopmentControls : MonoBehaviour
    {
        [SerializeField] private RuntimeLampDiagnosticMode mode = RuntimeLampDiagnosticMode.AdvancingPattern;
        [SerializeField] private float secondsPerShift = 0.5f;
        [SerializeField] private int manualLampNumber = 1;
        [SerializeField] private RuntimeReelLampDiagnosticMode reelLampDiagnosticMode = RuntimeReelLampDiagnosticMode.FollowLampState;
        [SerializeField, Range(1f, 20f)] private float reelLampDiagnosticMultiplier = 1f;
        [SerializeField] private int shiftOffset;
        private RuntimeMachine _machine;
        private RuntimeLampDiagnosticSequence _sequence;
        public void Initialize(RuntimeMachine machine) { _machine = machine; ApplyReelLampDiagnostics(); _sequence = new RuntimeLampDiagnosticSequence(CreateSettings()); RuntimeLampDiagnosticReporter.LogReady(machine); _sequence.Start(machine != null ? machine.LampState : null); RefreshStatus(); }
        private void Update()
        {
            if (_machine == null) return;
            ApplyReelLampDiagnostics();
            if (mode == RuntimeLampDiagnosticMode.Manual) RunManualControls();
            else if (mode == RuntimeLampDiagnosticMode.AdvancingPattern) { if (_sequence == null) { _sequence = new RuntimeLampDiagnosticSequence(CreateSettings()); _sequence.Start(_machine.LampState); } _sequence.Advance(_machine.LampState, Time.unscaledDeltaTime); }
            RefreshStatus();
        }
        private RuntimeLampDiagnosticSettings CreateSettings() { return new RuntimeLampDiagnosticSettings { Mode = mode, SecondsPerShift = secondsPerShift }.Clamped(); }
        private void ApplyReelLampDiagnostics() { if (_machine != null) _machine.SetReelLampDiagnostics(reelLampDiagnosticMode, reelLampDiagnosticMultiplier); }
        private void RunManualControls() { if (Input.GetKeyDown(KeyCode.LeftBracket)) manualLampNumber = Mathf.Max(RuntimeLampState.MinimumLampNumber, manualLampNumber - 1); if (Input.GetKeyDown(KeyCode.RightBracket)) manualLampNumber = Mathf.Min(RuntimeLampState.MaximumLampNumber, manualLampNumber + 1); if (Input.GetKeyDown(KeyCode.Alpha0)) _machine.LampState.SetBrightness(manualLampNumber, 0f); if (Input.GetKeyDown(KeyCode.Alpha1)) _machine.LampState.SetBrightness(manualLampNumber, .25f); if (Input.GetKeyDown(KeyCode.Alpha2)) _machine.LampState.SetBrightness(manualLampNumber, .5f); if (Input.GetKeyDown(KeyCode.Alpha3)) _machine.LampState.SetBrightness(manualLampNumber, 1f); if (Input.GetKeyDown(KeyCode.C)) _machine.LampState.ClearAll(); }
        private void RefreshStatus() { shiftOffset = _sequence != null ? _sequence.ShiftOffset : 0; }
        private void OnDisable() { if (_machine != null) _machine.SetReelLampDiagnostics(RuntimeReelLampDiagnosticMode.FollowLampState, 1f); }
    }

    public sealed class RuntimeReelDevelopmentControls : MonoBehaviour
    {
        // Half a revolution per second is fast enough to make the diagnostic useful visually.
        public const float DefaultRpm = 30f;
        [SerializeField] private float rpm = DefaultRpm;
        private RuntimeMachine _machine;
        public float Rpm { get { return rpm; } set { rpm = value; } }
        public static float PositionsPerSecond(float speedRpm) { return speedRpm * RuntimeReelPositionConverter.PositionsPerRevolution / 60f; }
        public void Initialize(RuntimeMachine machine) { _machine = machine; }
        private void Update() { Advance(Time.unscaledDeltaTime); }
        public void Advance(float unscaledDeltaSeconds)
        {
            if (_machine == null || unscaledDeltaSeconds <= 0f) return;
            var delta = PositionsPerSecond(rpm) * unscaledDeltaSeconds;
            foreach (var face in _machine.Faces) foreach (var binding in face.ReelRenderBindings) _machine.ReelState.SetPosition(binding.ReelIndex, _machine.ReelState.GetPosition(binding.ReelIndex) + delta);
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

            if (summary.MinimumLampId > RuntimeLampState.MaximumLampNumber) summary.MinimumLampId = -1;
            return summary;
        }

        public static string BuildSummary(RuntimeFace face, int faceIndex)
        {
            var summary = Analyze(face);
            var range = summary.MinimumLampId >= 0 ? $"{summary.MinimumLampId}–{summary.MaximumLampId}" : "none";
            return $"Face {faceIndex} lamp lookup:\nID data: {FormatBool(summary.HasLampIdData)}\nWeight data: {FormatBool(summary.HasLampWeightData)}\nassigned pixels: {summary.AssignedPixels}\nlamp range: {range}\ninvalid IDs: {summary.InvalidIdCount}\nnon-zero weights: {FormatBool(summary.HasNonZeroWeights)}";
        }

        private static void AnalyzeChannel(byte encodedLampId, byte weight, ref RuntimeFaceLookupDiagnosticSummary summary, ref bool assigned)
        {
            var lampId = RuntimeFaceLampLookupDecoder.ResolveLampStateIndex(0, encodedLampId);
            if (lampId < 0) return;
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
