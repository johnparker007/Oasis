using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeLampState
    {
        public const int MinimumLampNumber = 1;
        public const int MaximumLampNumber = 255;
        private readonly float[] _brightness = new float[MaximumLampNumber + 1];
        private int _version;
        private bool _dirty;

        public int Version { get { return _version; } }
        public bool IsDirty { get { return _dirty; } }
        public int Capacity { get { return _brightness.Length; } }

        public bool IsValidLampNumber(int lampNumber) { return lampNumber >= MinimumLampNumber && lampNumber <= MaximumLampNumber; }
        public float GetBrightness(int lampNumber) { return IsValidLampNumber(lampNumber) ? _brightness[lampNumber] : 0f; }
        public void MarkClean() { _dirty = false; }

        public bool SetBrightness(int lampNumber, float brightness)
        {
            if (!IsValidLampNumber(lampNumber)) return false;
            var normalized = NormalizeBrightness(brightness);
            if (Mathf.Abs(_brightness[lampNumber] - normalized) < 0.0001f) return false;
            _brightness[lampNumber] = normalized;
            _version++;
            _dirty = true;
            return true;
        }

        public bool SetBrightness(byte lampNumber, byte brightness)
        {
            return SetBrightness((int)lampNumber, brightness / 255f);
        }

        public bool SetBrightnessValues(System.Collections.Generic.IEnumerable<RuntimeLampBrightness> values)
        {
            if (values == null) return false;
            var changed = false;
            foreach (var value in values) changed |= SetBrightness(value.LampNumber, value.Brightness);
            return changed;
        }

        public bool ClearAll()
        {
            var changed = false;
            for (var i = MinimumLampNumber; i <= MaximumLampNumber; i++)
            {
                if (_brightness[i] <= 0f) continue;
                _brightness[i] = 0f;
                changed = true;
            }
            if (changed) { _version++; _dirty = true; }
            return changed;
        }

        internal void CopyBrightnessTo(Color[] pixels)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            for (var i = 0; i < pixels.Length; i++)
            {
                var brightness = i < _brightness.Length ? _brightness[i] : 0f;
                pixels[i] = new Color(brightness, 0f, 0f, 1f);
            }
        }

        private static float NormalizeBrightness(float brightness)
        {
            return float.IsNaN(brightness) || float.IsInfinity(brightness) ? 0f : Mathf.Clamp01(brightness);
        }
    }

    public struct RuntimeLampBrightness
    {
        public RuntimeLampBrightness(int lampNumber, float brightness) { LampNumber = lampNumber; Brightness = brightness; }
        public int LampNumber { get; private set; }
        public float Brightness { get; private set; }
    }

    public sealed class RuntimeLampStateTexture : IDisposable
    {
        private readonly Color[] _pixels;
        private int _uploadCount;
        private bool _disposed;
        public RuntimeLampStateTexture(RuntimeLampState lampState)
        {
            if (lampState == null) throw new ArgumentNullException(nameof(lampState));
            Texture = new Texture2D(lampState.Capacity, 1, TextureFormat.RGBA32, false, true);
            Texture.name = "OasisRuntimeLampState";
            Texture.wrapMode = TextureWrapMode.Clamp;
            Texture.filterMode = FilterMode.Point;
            _pixels = new Color[lampState.Capacity];
            Upload(lampState, true);
        }
        public Texture2D Texture { get; private set; }
        public int UploadCount { get { return _uploadCount; } }
        public bool IsDisposed { get { return _disposed; } }
        public bool Upload(RuntimeLampState lampState) { return Upload(lampState, false); }
        public bool Upload(RuntimeLampState lampState, bool force)
        {
            if (_disposed || lampState == null || (!force && !lampState.IsDirty)) return false;
            lampState.CopyBrightnessTo(_pixels);
            Texture.SetPixels(_pixels);
            Texture.Apply(false, false);
            lampState.MarkClean();
            _uploadCount++;
            return true;
        }
        public void Dispose()
        {
            if (_disposed) return;
            if (Texture != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(Texture); else UnityEngine.Object.DestroyImmediate(Texture); }
            Texture = null; _disposed = true;
        }
    }

    public static class RuntimeFaceLampLookupDecoder
    {
        public const int InvalidLampId = 0;
        public const int ChannelCount = 3;
        public static float DecodeByteWeight(int value) { return Mathf.Clamp(value, 0, 255) / 255f; }
        public static int ResolveLampStateIndex(int trayId, int lampId) { return lampId >= 1 && lampId <= 255 ? lampId : InvalidLampId; }
        public static float Accumulate(float[] lampBrightness, int[] lampIds, int[] weights)
        {
            var total = 0f;
            for (var i = 0; i < ChannelCount; i++) { var id = lampIds[i]; if (id > 0 && id < lampBrightness.Length) total += lampBrightness[id] * DecodeByteWeight(weights[i]); }
            return total;
        }
    }
}
