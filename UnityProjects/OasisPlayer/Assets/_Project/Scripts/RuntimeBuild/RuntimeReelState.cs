using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Authoritative logical positions for runtime reels (zero based, 96 positions per revolution).</summary>
    public sealed class RuntimeReelState
    {
        public const int MaximumReelCount = 256;
        private readonly float[] _positions = new float[MaximumReelCount];

        public int Version { get; private set; }
        public bool IsValidReelIndex(int reelIndex) { return reelIndex >= 0 && reelIndex < MaximumReelCount; }
        public float GetPosition(int reelIndex) { return IsValidReelIndex(reelIndex) ? _positions[reelIndex] : 0f; }

        public bool SetPosition(int reelIndex, float position)
        {
            if (!IsValidReelIndex(reelIndex)) return false;
            var normalized = float.IsNaN(position) || float.IsInfinity(position)
                ? 0f
                : RuntimeReelPositionConverter.PositiveModulo(position, RuntimeReelPositionConverter.PositionsPerRevolution);
            if (Mathf.Abs(_positions[reelIndex] - normalized) < 0.0001f) return false;
            _positions[reelIndex] = normalized;
            Version++;
            return true;
        }

        public bool ClearAll()
        {
            var changed = false;
            for (var i = 0; i < _positions.Length; i++)
            {
                if (Mathf.Abs(_positions[i]) < 0.0001f) continue;
                _positions[i] = 0f;
                changed = true;
            }
            if (changed) Version++;
            return changed;
        }
    }
}
