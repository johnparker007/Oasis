#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Inspector-configured development keys; logical transitions still belong to A7.</summary>
    public sealed class RuntimeInputDevelopmentControls : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public KeyCode key;
            public string inputId;
        }

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        private RuntimeMachine _machine;
        private readonly HashSet<string> _pressed = new HashSet<string>(StringComparer.Ordinal);
        private bool _hasFocus = true;

        public void Initialize(RuntimeMachine machine)
        {
            ReleaseInputs();
            _machine = machine;
        }

        public void Configure(Binding[] values)
        {
            ReleaseInputs();
            bindings = values == null ? Array.Empty<Binding>() : (Binding[])values.Clone();
        }

        private void Update() { Poll(Input.GetKey); }

        // Also permits deterministic transition verification without simulating keyboard hardware.
        public void Poll(Func<KeyCode, bool> isHeld)
        {
            if (_machine == null || !_machine.IsActive) { _pressed.Clear(); _machine = null; return; }
            if (!isActiveAndEnabled || !_hasFocus) { ReleaseInputs(); return; }
            if (isHeld == null) throw new ArgumentNullException(nameof(isHeld));
            var held = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings ?? Array.Empty<Binding>())
            {
                // Inspector edits may contain incomplete entries or IDs from another Machine.
                if (binding == null || binding.key == KeyCode.None || string.IsNullOrEmpty(binding.inputId)
                    || !_machine.Inputs.ContainsKey(binding.inputId)) continue;
                if (isHeld(binding.key)) held.Add(binding.inputId);
            }
            // Aggregate keys mapped to one input, so releasing one key cannot release another.
            foreach (var id in new List<string>(_pressed))
            {
                if (!HasActiveMachine()) return;
                if (!held.Contains(id)) { _pressed.Remove(id); _machine.SetInputState(id, false); }
            }
            foreach (var id in held)
            {
                if (!HasActiveMachine()) return;
                if (_pressed.Add(id)) _machine.SetInputState(id, true);
            }
            HasActiveMachine(); // A transition callback may synchronously unload the Machine.
        }

        private bool HasActiveMachine()
        {
            if (_machine != null && _machine.IsActive) return true;
            _pressed.Clear(); _machine = null; return false;
        }

        private void ReleaseInputs()
        {
            var pressed = new List<string>(_pressed);
            _pressed.Clear();
            foreach (var id in pressed)
            {
                if (!HasActiveMachine()) return;
                _machine.SetInputState(id, false);
            }
        }

        private void OnApplicationFocus(bool focused) { _hasFocus = focused; if (!focused) ReleaseInputs(); }
        private void OnDisable() { ReleaseInputs(); }
        private void OnDestroy() { ReleaseInputs(); _machine = null; }
    }
}
#endif
