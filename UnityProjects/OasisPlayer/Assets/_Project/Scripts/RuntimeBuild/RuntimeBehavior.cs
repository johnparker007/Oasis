using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Unity-independent vector used by the behaviour-facing runtime contract.</summary>
    public readonly struct RuntimeVector3
    {
        public RuntimeVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public static RuntimeVector3 Zero { get { return new RuntimeVector3(0, 0, 0); } }
        internal Vector3 ToUnity() { return new Vector3(X, Y, Z); }
        internal static RuntimeVector3 FromUnity(Vector3 value) { return new RuntimeVector3(value.x, value.y, value.z); }
    }

    /// <summary>Position and XYZ Euler rotation in Machine composition space.</summary>
    public readonly struct RuntimePose
    {
        public RuntimePose(RuntimeVector3 position, RuntimeVector3 rotationEulerDegrees) { Position = position; RotationEulerDegrees = rotationEulerDegrees; }
        public RuntimeVector3 Position { get; }
        public RuntimeVector3 RotationEulerDegrees { get; }
    }

    public abstract class RuntimeMachineEvent { }
    public sealed class RuntimeMachineStartedEvent : RuntimeMachineEvent { }
    public sealed class RuntimeInputPressedEvent : RuntimeMachineEvent { public RuntimeInputPressedEvent(string inputId) { InputId = inputId; } public string InputId { get; } }
    public sealed class RuntimeInputReleasedEvent : RuntimeMachineEvent { public RuntimeInputReleasedEvent(string inputId) { InputId = inputId; } public string InputId { get; } }
    public sealed class RuntimeTriggerEnteredEvent : RuntimeMachineEvent { public RuntimeTriggerEnteredEvent(string triggerId, string objectId) { TriggerId = triggerId; ObjectId = objectId; } public string TriggerId { get; } public string ObjectId { get; } }
    public sealed class RuntimeTriggerExitedEvent : RuntimeMachineEvent { public RuntimeTriggerExitedEvent(string triggerId, string objectId) { TriggerId = triggerId; ObjectId = objectId; } public string TriggerId { get; } public string ObjectId { get; } }
    public sealed class RuntimeCollisionEnteredEvent : RuntimeMachineEvent { public RuntimeCollisionEnteredEvent(string objectId, string otherObjectId) { ObjectId = objectId; OtherObjectId = otherObjectId; } public string ObjectId { get; } public string OtherObjectId { get; } }
    public sealed class RuntimeCollisionExitedEvent : RuntimeMachineEvent { public RuntimeCollisionExitedEvent(string objectId, string otherObjectId) { ObjectId = objectId; OtherObjectId = otherObjectId; } public string ObjectId { get; } public string OtherObjectId { get; } }
    public sealed class RuntimeTimerElapsedEvent : RuntimeMachineEvent { public RuntimeTimerElapsedEvent(string timerId) { TimerId = timerId; } public string TimerId { get; } }

    /// <summary>Synchronous, ordered, Machine-session event stream. Subscriber exceptions propagate to the caller.</summary>
    public sealed class RuntimeMachineEventDispatcher
    {
        private readonly List<Action<RuntimeMachineEvent>> _subscribers = new List<Action<RuntimeMachineEvent>>();
        public void Subscribe(Action<RuntimeMachineEvent> subscriber) { if (subscriber == null) throw new ArgumentNullException(nameof(subscriber)); _subscribers.Add(subscriber); }
        public bool Unsubscribe(Action<RuntimeMachineEvent> subscriber) { return subscriber != null && _subscribers.Remove(subscriber); }
        public void Publish(RuntimeMachineEvent value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var snapshot = _subscribers.ToArray();
            for (var i = 0; i < snapshot.Length; i++) snapshot[i](value);
        }
        internal void Clear() { _subscribers.Clear(); }
    }

    public sealed class RuntimeInput
    {
        internal RuntimeInput(string id, string name) { Id = id; Name = name; }
        public string Id { get; }
        public string Name { get; }
        public bool IsPressed { get; internal set; }
    }

    public sealed class RuntimeTrigger
    {
        internal RuntimeTrigger(string id, Collider collider) { Id = id; Collider = collider; }
        public string Id { get; }
        internal Collider Collider { get; }
    }

    /// <summary>Deterministically advanced, named, one-shot Machine timers.</summary>
    public sealed class RuntimeTimerService
    {
        private readonly RuntimeMachineEventDispatcher _events;
        private readonly Dictionary<string, float> _remaining = new Dictionary<string, float>(StringComparer.Ordinal);
        internal RuntimeTimerService(RuntimeMachineEventDispatcher events) { _events = events; }
        public int Count { get { return _remaining.Count; } }
        public void StartTimer(string timerId, float durationSeconds)
        {
            ValidateId(timerId);
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Timer duration must be finite and greater than or equal to zero.");
            _remaining[timerId] = durationSeconds;
        }
        public bool StopTimer(string timerId) { ValidateId(timerId); return _remaining.Remove(timerId); }
        public void Advance(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            var ids = new List<string>(_remaining.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!_remaining.TryGetValue(id, out var remaining)) continue;
                remaining -= deltaSeconds;
                if (remaining > 0) { _remaining[id] = remaining; continue; }
                _remaining.Remove(id);
                _events.Publish(new RuntimeTimerElapsedEvent(id));
            }
        }
        internal void Clear() { _remaining.Clear(); }
        private static void ValidateId(string id) { if (!RuntimeIdentity.IsValid(id)) throw new ArgumentException("Timer ID must contain only letters, digits, '_' or '-'.", nameof(id)); }
    }

    public sealed class RuntimeMachineCommandService
    {
        private readonly RuntimeMachine _machine;
        internal RuntimeMachineCommandService(RuntimeMachine machine) { _machine = machine; }
        public void SetActive(string objectId, bool active) { _machine.GetObject(objectId).Root.SetActive(active); }
        public void Teleport(string objectId, string anchorId)
        {
            var anchor = _machine.GetAnchor(anchorId);
            Teleport(objectId, new RuntimePose(anchor.Position, anchor.RotationEulerDegrees));
        }
        public void Teleport(string objectId, RuntimePose pose)
        {
            var instance = _machine.GetObject(objectId);
            var transform = instance.Root.transform;
            transform.localPosition = pose.Position.ToUnity();
            transform.localRotation = Quaternion.Euler(pose.RotationEulerDegrees.ToUnity());
            if (instance.Rigidbody != null)
            {
                instance.Rigidbody.position = transform.position;
                instance.Rigidbody.rotation = transform.rotation;
            }
            Physics.SyncTransforms();
        }
        public void SetVelocity(string objectId, RuntimeVector3 velocity) { RequireBody(objectId).linearVelocity = velocity.ToUnity(); }
        public void SetAngularVelocity(string objectId, RuntimeVector3 velocity) { RequireBody(objectId).angularVelocity = velocity.ToUnity(); }
        public void ApplyImpulse(string objectId, RuntimeVector3 impulse) { RequireBody(objectId).AddForce(impulse.ToUnity(), ForceMode.Impulse); }
        public void ResetObject(string objectId)
        {
            var instance = _machine.GetObject(objectId);
            instance.Root.SetActive(true);
            var authored = instance.AuthoredInitialTransform;
            instance.Root.transform.localScale = authored.scale.Value;
            Teleport(objectId, new RuntimePose(RuntimeVector3.FromUnity(authored.position.Value), RuntimeVector3.FromUnity(authored.rotationEulerDegrees.Value)));
            if (instance.Rigidbody != null) { instance.Rigidbody.linearVelocity = Vector3.zero; instance.Rigidbody.angularVelocity = Vector3.zero; }
        }
        public void StartTimer(string timerId, float durationSeconds) { _machine.Timers.StartTimer(timerId, durationSeconds); }
        public bool StopTimer(string timerId) { return _machine.Timers.StopTimer(timerId); }
        private Rigidbody RequireBody(string objectId)
        {
            var instance = _machine.GetObject(objectId);
            if (instance.Rigidbody == null) throw new InvalidOperationException($"Runtime Object3D instance '{objectId}' has no Rigidbody.");
            return instance.Rigidbody;
        }
    }

    internal static class RuntimeIdentity
    {
        public static bool IsValid(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (var i = 0; i < id.Length; i++)
            {
                var value = id[i];
                var asciiLetterOrDigit = value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z' || value >= '0' && value <= '9';
                if (!asciiLetterOrDigit && value != '_' && value != '-') return false;
            }
            return true;
        }
    }

    /// <summary>Internal mapping from any descendant physics collider to its registered Object3D identity.</summary>
    public sealed class RuntimeObjectIdentity : MonoBehaviour
    {
        private RuntimeMachine _machine;
        private string _objectId;
        internal void Initialize(RuntimeMachine machine, string objectId) { _machine = machine; _objectId = objectId; }
        public void PublishCollisionEntered(Collider other) { Publish(other, true); }
        public void PublishCollisionExited(Collider other) { Publish(other, false); }
        private void OnCollisionEnter(Collision collision) { PublishCollisionEntered(collision.collider); }
        private void OnCollisionExit(Collision collision) { PublishCollisionExited(collision.collider); }
        private void Publish(Collider other, bool entered)
        {
            if (_machine == null || !_machine.IsActive || other == null) return;
            var identity = other.GetComponentInParent<RuntimeObjectIdentity>();
            if (identity == null || identity._machine != _machine || identity._objectId == _objectId) return;
            _machine.Events.Publish(entered ? (RuntimeMachineEvent)new RuntimeCollisionEnteredEvent(_objectId, identity._objectId) : new RuntimeCollisionExitedEvent(_objectId, identity._objectId));
        }
    }

    public sealed class RuntimeTriggerRelay : MonoBehaviour
    {
        private RuntimeMachine _machine;
        private string _triggerId;
        internal void Initialize(RuntimeMachine machine, string triggerId) { _machine = machine; _triggerId = triggerId; }
        public void PublishEntered(Collider other) { Publish(other, true); }
        public void PublishExited(Collider other) { Publish(other, false); }
        private void OnTriggerEnter(Collider other) { PublishEntered(other); }
        private void OnTriggerExit(Collider other) { PublishExited(other); }
        private void Publish(Collider other, bool entered)
        {
            if (_machine == null || !_machine.IsActive || other == null) return;
            var identity = other.GetComponentInParent<RuntimeObjectIdentity>();
            if (identity == null || !_machine.TryGetObjectIdentity(identity, out var objectId)) return;
            _machine.Events.Publish(entered ? (RuntimeMachineEvent)new RuntimeTriggerEnteredEvent(_triggerId, objectId) : new RuntimeTriggerExitedEvent(_triggerId, objectId));
        }
    }

    /// <summary>Unity adapter that advances Machine timers with scaled game time.</summary>
    public sealed class RuntimeBehaviorDriver : MonoBehaviour
    {
        private RuntimeMachine _machine;
        public void Initialize(RuntimeMachine machine) { _machine = machine; }
        private void Update() { if (_machine != null && _machine.IsActive) _machine.Timers.Advance(Time.deltaTime); }
    }
}
