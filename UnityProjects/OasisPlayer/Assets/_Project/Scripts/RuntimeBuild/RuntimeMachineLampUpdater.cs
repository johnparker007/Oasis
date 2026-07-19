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

    public sealed class RuntimeLampDevelopmentControls : MonoBehaviour
    {
        [SerializeField] private int lampNumber = 1;
        [SerializeField] private int cycleStartLampNumber = 1;
        [SerializeField] private int cycleEndLampNumber = 8;
        [SerializeField] private float cycleSecondsPerLamp = 0.5f;
        private RuntimeMachine _machine;
        private float _cycleTimer;
        private int _cycleLamp;

        public void Initialize(RuntimeMachine machine)
        {
            _machine = machine;
            _cycleLamp = Mathf.Clamp(cycleStartLampNumber, RuntimeLampState.MinimumLampNumber, RuntimeLampState.MaximumLampNumber);
        }

        private void Update()
        {
            if (_machine == null) return;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) lampNumber = Mathf.Max(RuntimeLampState.MinimumLampNumber, lampNumber - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) lampNumber = Mathf.Min(RuntimeLampState.MaximumLampNumber, lampNumber + 1);
            if (Input.GetKeyDown(KeyCode.Alpha0)) _machine.LampState.SetBrightness(lampNumber, 0f);
            if (Input.GetKeyDown(KeyCode.Alpha1)) _machine.LampState.SetBrightness(lampNumber, 0.25f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _machine.LampState.SetBrightness(lampNumber, 0.5f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _machine.LampState.SetBrightness(lampNumber, 1f);
            if (Input.GetKeyDown(KeyCode.C)) _machine.LampState.ClearAll();
            if (Input.GetKey(KeyCode.L)) CycleLamps();
        }

        private void CycleLamps()
        {
            _cycleTimer += Time.deltaTime;
            if (_cycleTimer < Mathf.Max(0.05f, cycleSecondsPerLamp)) return;
            _cycleTimer = 0f;
            _machine.LampState.ClearAll();
            _machine.LampState.SetBrightness(_cycleLamp, 1f);
            _cycleLamp++;
            if (_cycleLamp > cycleEndLampNumber) _cycleLamp = cycleStartLampNumber;
        }
    }
}
