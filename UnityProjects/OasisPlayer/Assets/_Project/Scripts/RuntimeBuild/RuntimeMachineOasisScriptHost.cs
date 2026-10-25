using System;
using System.Collections.Generic;
using Oasis.Scripting;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Player-side script commands resolve only Machine-scoped object handles.</summary>
    public sealed class RuntimeMachineOasisScriptHost : IOasisScriptValueHost, IOasisScriptDispatchHost, IDisposable
    {
        private RuntimeMachine _machine;

        public RuntimeMachineOasisScriptHost(RuntimeMachine machine) { _machine = machine ?? throw new ArgumentNullException(nameof(machine)); }

        public OasisScriptHostResult ResolveObject(string authoredId) => ExecuteValue("oasis reference resolution", () =>
        {
            if (_machine.IsDynamicObject(authoredId)) throw new ArgumentException("Runtime-created objects must be referenced through the OasisObjectRef returned by oasis.instantiate.");
            return _machine.GetObjectReference(authoredId);
        });
        public OasisScriptHostResult SetActive(OasisScriptReferenceValue objectRef, bool active) => Execute("oasis.set_active", () => _machine.Commands.SetActive(ObjectId(objectRef), active));
        public OasisScriptHostResult Teleport(OasisScriptReferenceValue objectRef, OasisScriptReferenceValue anchorRef) => Execute("oasis.teleport", () => _machine.Commands.Teleport(ObjectId(objectRef), ReferenceId(anchorRef, OasisScriptType.AnchorRef)));
        public OasisScriptHostResult TeleportPose(OasisScriptReferenceValue objectRef, OasisScriptVec3Value position, OasisScriptVec3Value rotation) => Execute("oasis.teleport_pose", () => _machine.Commands.Teleport(ObjectId(objectRef), new RuntimePose(Vector(position), Vector(rotation))));
        public OasisScriptHostResult SetVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity) => Execute("oasis.set_velocity", () => _machine.Commands.SetVelocity(ObjectId(objectRef), Vector(velocity)));
        public OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity) => Execute("oasis.set_angular_velocity", () => _machine.Commands.SetAngularVelocity(ObjectId(objectRef), Vector(velocity)));
        public OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue objectRef, OasisScriptVec3Value impulse) => Execute("oasis.apply_impulse", () => _machine.Commands.ApplyImpulse(ObjectId(objectRef), Vector(impulse)));
        public OasisScriptHostResult ResetObject(OasisScriptReferenceValue objectRef) => Execute("oasis.reset", () => _machine.Commands.ResetObject(ObjectId(objectRef)));
        public OasisScriptHostResult StartTimer(string name, double seconds) => Execute("timer.start", () => _machine.Commands.StartTimer(name, TimerDuration(seconds)));
        public OasisScriptHostResult StopTimer(string name) => Execute("timer.stop", () => _machine.Commands.StopTimer(name));
        public void BeginDispatch() { if (_machine != null && _machine.IsActive) _machine.BeginScriptEvent(); }
        public void EndDispatch() { if (_machine != null) _machine.EndScriptEvent(); }

        public OasisScriptHostResult Query(string operation, IReadOnlyList<OasisScriptValue> arguments)
        {
            return ExecuteValue(operation ?? "script query", () => QueryValue(operation, arguments));
        }

        private OasisScriptValue QueryValue(string operation, IReadOnlyList<OasisScriptValue> arguments)
        {
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            switch (operation)
            {
                case "oasis.instantiate":
                {
                    var alias = ReferenceId(ArgRef(arguments, 0), OasisScriptType.PrefabRef);
                    var instance = _machine.InstantiatePrefab(alias, new RuntimePose(Vector(ArgVec3(arguments, 1)), Vector(ArgVec3(arguments, 2))));
                    return _machine.GetObjectReference(instance.Id);
                }
                case "oasis.destroy":
                {
                    var id = ObjectId(ArgRef(arguments, 0));
                    if (!_machine.IsDynamicObject(id)) throw new InvalidOperationException("Only objects created by oasis.instantiate can be destroyed.");
                    if (!_machine.DestroyDynamicObject(id)) throw new InvalidOperationException("Object reference is stale or has already been destroyed.");
                    return OasisScriptVoidValue.Instance;
                }
                case "oasis.is_alive":
                {
                    var reference = ArgRef(arguments, 0);
                    ValidateObjectScope(reference);
                    return new OasisScriptBoolValue(_machine.IsLiveObjectReference(reference));
                }
                case "frame.position":
                {
                    var anchorId = ReferenceId(ArgRef(arguments, 0), OasisScriptType.AnchorRef);
                    return Vec3(_machine.Queries.FramePosition(anchorId, Vector(ArgVec3(arguments, 1))));
                }
                case "frame.rotation":
                {
                    var anchorId = ReferenceId(ArgRef(arguments, 0), OasisScriptType.AnchorRef);
                    return Vec3(_machine.Queries.FrameRotation(anchorId, Vector(ArgVec3(arguments, 1))));
                }
                case "collider.sphere_radius":
                    return new OasisScriptNumberValue(_machine.Queries.SphereRadius(ReferenceId(ArgRef(arguments, 0), OasisScriptType.PrefabRef)));
                case "surface.contains_sphere":
                    return new OasisScriptBoolValue(_machine.Queries.SurfaceContainsSphere(ReferenceId(ArgRef(arguments, 0), OasisScriptType.SurfaceRef), Vector(ArgVec3(arguments, 1)), Scalar(ArgNumber(arguments, 2).Value)));
                default: throw new InvalidOperationException($"Unsupported script query '{operation}'.");
            }
        }

        public void Dispose() { _machine = null; }

        private OasisScriptHostResult Execute(string command, Action action)
        {
            if (_machine == null || !_machine.IsActive) return OasisScriptHostResult.Fail(command + ": Machine session has been unloaded or detached.");
            try { action(); return OasisScriptHostResult.Ok; }
            catch (Exception exception) when (exception is ArgumentException || exception is KeyNotFoundException || exception is InvalidOperationException)
            { return OasisScriptHostResult.Fail(command + ": " + exception.Message); }
            catch (Exception exception) { return OasisScriptHostResult.Fail(command + ": unexpected " + exception.GetType().Name + ": " + exception.Message); }
        }

        private OasisScriptHostResult ExecuteValue(string command, Func<OasisScriptValue> action)
        {
            if (_machine == null || !_machine.IsActive) return OasisScriptHostResult.Fail(command + ": Machine session has been unloaded or detached.");
            try
            {
                var value = action();
                return value is OasisScriptVoidValue ? OasisScriptHostResult.Ok : OasisScriptHostResult.Return(value);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is KeyNotFoundException || exception is InvalidOperationException)
            { return OasisScriptHostResult.Fail(command + ": " + exception.Message); }
            catch (Exception exception) { return OasisScriptHostResult.Fail(command + ": unexpected " + exception.GetType().Name + ": " + exception.Message); }
        }

        private string ObjectId(OasisScriptReferenceValue value)
        {
            if (value == null || !value.Type.Equals(OasisScriptType.OasisObjectRef)) throw new ArgumentException("Expected OasisObjectRef reference.");
            ValidateObjectScope(value);
            if (!_machine.IsLiveObjectReference(value)) throw new KeyNotFoundException($"Object reference '{value.Id}' is stale or no longer registered.");
            return value.Id;
        }

        private void ValidateObjectScope(OasisScriptReferenceValue value)
        {
            if (value == null || !value.Type.Equals(OasisScriptType.OasisObjectRef) || !value.IsLiveObject) throw new ArgumentException("Expected a resolved OasisObjectRef.");
            if (value.SessionId != _machine.SessionId || value.Generation != _machine.Generation) throw new InvalidOperationException("OasisObjectRef belongs to a different Machine session or generation.");
        }

        private static string ReferenceId(OasisScriptReferenceValue value, OasisScriptType expected)
        {
            if (value == null || !expected.Equals(value.Type)) throw new ArgumentException("Expected " + expected + " reference.");
            if (string.IsNullOrWhiteSpace(value.Id)) throw new ArgumentException("Reference ID is required.");
            return value.Id;
        }
        private static OasisScriptReferenceValue ArgRef(IReadOnlyList<OasisScriptValue> values, int index)
        {
            if (index >= values.Count || !(values[index] is OasisScriptReferenceValue reference)) throw new ArgumentException($"Expected reference argument at index {index}.");
            return reference;
        }
        private static OasisScriptVec3Value ArgVec3(IReadOnlyList<OasisScriptValue> values, int index)
        {
            if (index >= values.Count || !(values[index] is OasisScriptVec3Value vector)) throw new ArgumentException($"Expected Vec3 argument at index {index}.");
            return vector;
        }
        private static OasisScriptNumberValue ArgNumber(IReadOnlyList<OasisScriptValue> values, int index)
        {
            if (index >= values.Count || !(values[index] is OasisScriptNumberValue number)) throw new ArgumentException($"Expected Number argument at index {index}.");
            return number;
        }
        private static RuntimeVector3 Vector(OasisScriptVec3Value value)
        {
            if (value == null) throw new ArgumentException("Expected Vec3 value.");
            return new RuntimeVector3(Scalar(value.X), Scalar(value.Y), Scalar(value.Z));
        }
        private static OasisScriptVec3Value Vec3(RuntimeVector3 value) => new OasisScriptVec3Value(value.X, value.Y, value.Z);
        private static float Scalar(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -float.MaxValue || value > float.MaxValue) throw new ArgumentException("Numeric value must be finite and fit the A7 float range.");
            return (float)value;
        }
        private static float TimerDuration(double seconds)
        {
            if (seconds < 0) throw new ArgumentException("Timer duration must be greater than or equal to zero.");
            return Scalar(seconds);
        }
    }
}
