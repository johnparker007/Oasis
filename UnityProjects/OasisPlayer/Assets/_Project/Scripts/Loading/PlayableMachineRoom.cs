using System;
using UnityEngine;
using OasisPlayer.RuntimeBuild;

namespace OasisPlayer.Loading
{
    /// <summary>Disposable single-Machine room, replaced later by authored arcade placements.</summary>
    public sealed class PlayableMachineRoom : MonoBehaviour
    {
        private RuntimeMachine _machine;
        private Camera _camera;
        private CharacterController _walker;
        private RuntimeInputDevelopmentControls _keys;
        private PoolInteraction _pool;
        private MachineInteractionProfile _profile;
        private Bounds _bounds;
        private float _yaw, _pitch, _poolYaw, _speed = 2.5f, _fall;
        private bool _captured, _engaged, _focused = true, _wasUiOpen;
        private string _message = "";
        private Vector3 _walkCameraPosition;
        private Quaternion _walkCameraRotation;
        public Func<bool> UiIsOpen { get; set; }
        public bool IsEngaged { get { return _engaged; } }
        public Bounds CabinetBounds { get { return _bounds; } }

        public static Bounds MeasureCabinet(RuntimeMachine machine)
        {
            if (machine == null || machine.Cabinet == null) throw new ArgumentException("A loaded Cabinet is required.");
            var found = false;
            var result = new Bounds();
            foreach (var renderer in machine.Cabinet.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { if (!found) { result = renderer.bounds; found = true; } else result.Encapsulate(renderer.bounds); }
            foreach (var collider in machine.Cabinet.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) { if (!found) { result = collider.bounds; found = true; } else result.Encapsulate(collider.bounds); }
            if (!found || result.size.sqrMagnitude < .001f || !Finite(result.center) || !Finite(result.size) || result.size.magnitude > 100)
                throw new InvalidOperationException("Cabinet has no usable finite bounds for a playable room.");
            return result;
        }

        public void Initialize(RuntimeMachine machine, Camera camera, MachineInteractionProfile profile, bool replacePreviewEnvironment = true)
        {
            if (_machine != null) throw new InvalidOperationException("Room is already initialized.");
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _profile = profile;
            _pool = profile == MachineInteractionProfile.Pool ? new PoolInteraction(machine) : null;
            _bounds = MeasureCabinet(machine);
            if (_pool != null)
            {
                if (!_pool.TryGetCue(out _)) throw new InvalidOperationException("Pool profile requires one live cueBall entity with a dynamic Rigidbody.");
                var radius = machine.Queries.SphereRadius("cueBall");
                machine.Queries.SurfaceContainsSphere("playingSurface", RuntimeVector3.Zero, radius);
            }
            // Keep the machine and its physics roots stationary. Fit the room around actual world bounds.
            if (replacePreviewEnvironment)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                    if (root.name == "Environment" || root.name == "Lights") root.SetActive(false);
            var floorY = _bounds.min.y;
            var width = Mathf.Max(8, _bounds.size.x + 6);
            var length = Mathf.Max(8, _bounds.size.z + 6);
            var center = new Vector3(_bounds.center.x, floorY, _bounds.center.z);
            Box("Floor", center + Vector3.down * .15f, new Vector3(width, .3f, length));
            Box("North wall", center + new Vector3(0, 2, length / 2), new Vector3(width, 4, .2f));
            Box("South wall", center + new Vector3(0, 2, -length / 2), new Vector3(width, 4, .2f));
            Box("East wall", center + new Vector3(width / 2, 2, 0), new Vector3(.2f, 4, length));
            Box("West wall", center + new Vector3(-width / 2, 2, 0), new Vector3(.2f, 4, length));
            var lamp = new GameObject("Room light"); lamp.transform.SetParent(transform, false);
            lamp.transform.position = center + Vector3.up * 3;
            var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.range = Mathf.Max(width, length) * 2; light.intensity = 3;
            var walker = new GameObject("Arcade visitor"); walker.transform.SetParent(transform, false);
            walker.transform.position = new Vector3(_bounds.center.x, floorY + .05f, _bounds.min.z - 1.8f);
            _walker = walker.AddComponent<CharacterController>();
            _walker.height = 1.8f; _walker.radius = .3f; _walker.center = Vector3.up * .9f; _walker.stepOffset = .2f;
            _camera.transform.position = walker.transform.position + Vector3.up * 1.65f;
            _camera.transform.rotation = Quaternion.identity;
            _yaw = 0; _pitch = 0;
            _keys = machine.MachineSpace.GetComponent<RuntimeInputDevelopmentControls>();
            if (_keys == null) { _keys = machine.MachineSpace.gameObject.AddComponent<RuntimeInputDevelopmentControls>(); _keys.Initialize(machine); }
            _keys.SetInteractionEnabled(false);
            Physics.SyncTransforms();
        }

        public bool EnterMachine()
        {
            if (_machine == null || !_machine.IsActive || _engaged || _walker == null) return false;
            var p = _walker.transform.position; p.y = _bounds.center.y;
            if (Vector3.Distance(p, _bounds.ClosestPoint(p)) > 2.2f) { _message = "Move closer to the machine."; return false; }
            _walkCameraPosition = _camera.transform.position; _walkCameraRotation = _camera.transform.rotation;
            _engaged = true; _poolYaw = _machine.MachineSpace.eulerAngles.y;
            _keys.SetInteractionEnabled(true);
            return true;
        }

        public void LeaveMachine()
        {
            if (_keys != null) _keys.SetInteractionEnabled(false);
            if (!_engaged) return;
            _engaged = false;
            if (_camera != null) { _camera.transform.position = _walkCameraPosition; _camera.transform.rotation = _walkCameraRotation; }
        }

        private void Update()
        {
            if (_machine == null) return;
            if (!_machine.IsActive) { LeaveMachine(); ReleasePointer(); enabled = false; return; }
            if (UiIsOpen != null && UiIsOpen()) { _wasUiOpen = true; LeaveMachine(); ReleasePointer(); return; }
            if (_wasUiOpen) { _wasUiOpen = false; ReleasePointer(); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { LeaveMachine(); ReleasePointer(); return; }
            if (!_focused) return;
            if (!_captured)
            {
                if (Input.GetMouseButtonDown(0)) { _captured = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                return; // Capturing click never fires a shot.
            }
            if (Input.GetKeyDown(KeyCode.E)) { if (_engaged) LeaveMachine(); else EnterMachine(); }
            if (_engaged)
            {
                if (_profile == MachineInteractionProfile.Pool && _pool.TryGetCue(out var cue))
                {
                    _poolYaw += Input.GetAxisRaw("Mouse X") * 2;
                    _speed = Mathf.Clamp(_speed + Input.mouseScrollDelta.y * .25f, .2f, 6);
                    var direction = Quaternion.Euler(0, _poolYaw, 0) * Vector3.forward;
                    var target = cue.Rigidbody.position;
                    _camera.transform.position = target - direction * 1.3f + Vector3.up * 1.25f;
                    _camera.transform.LookAt(target + direction * .4f);
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)) _pool.TryShoot(direction, _speed, out _message);
                }
                else if (_profile == MachineInteractionProfile.Buttons)
                {
                    _camera.transform.position = new Vector3(_bounds.center.x, _bounds.min.y + Mathf.Min(1.5f, _bounds.size.y * .8f), _bounds.min.z - .8f);
                    _camera.transform.LookAt(_bounds.center);
                }
                return;
            }
            _yaw += Input.GetAxisRaw("Mouse X") * 2;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * 2, -80, 80);
            var movement = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0,
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            _fall = _walker.isGrounded ? -1 : Mathf.Max(-20, _fall + Physics.gravity.y * Time.deltaTime);
            _walker.Move((Quaternion.Euler(0, _yaw, 0) * Vector3.ClampMagnitude(movement, 1) * 2.5f + Vector3.up * _fall) * Time.deltaTime);
            _camera.transform.SetPositionAndRotation(_walker.transform.position + Vector3.up * 1.65f, Quaternion.Euler(_pitch, _yaw, 0));
        }

        private void OnGUI()
        {
            if (_machine == null || !_machine.IsActive || UiIsOpen != null && UiIsOpen()) return;
            var text = !_captured ? "Click to look around.  WASD walk   E use machine   F10 settings" :
                !_engaged ? "WASD walk   Mouse look   E use machine   Esc release mouse" :
                _profile == MachineInteractionProfile.Pool ? $"Mouse aim   Wheel power ({_speed:F1} m/s)   Click/Space shoot   R rerack   N new game   E leave" : "Use the machine's authored keys.   E leave   Esc release mouse";
            GUI.Box(new Rect(12, Screen.height - 85, Mathf.Min(Screen.width - 24, 900), 73), text + "\n" + _message);
            if (_engaged && _profile == MachineInteractionProfile.Pool) GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 10, 20, 25), "+");
        }
        private void Box(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(transform, false); box.transform.position = position; box.transform.localScale = size;
        }
        private void ReleasePointer() { _captured = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focus) { _focused = focus; if (!focus) { LeaveMachine(); ReleasePointer(); } }
        private void OnDisable() { LeaveMachine(); if (_captured) ReleasePointer(); }
        private static bool Finite(Vector3 v) { return !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z); }
    }
}
