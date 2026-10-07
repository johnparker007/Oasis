using System;
using System.Collections.Generic;
using Oasis.Scripting;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Player-side script commands use only the established A7 public boundary.</summary>
    public sealed class RuntimeMachineOasisScriptHost : IOasisScriptHost, IDisposable
    {
        private RuntimeMachine _machine;

        public RuntimeMachineOasisScriptHost(RuntimeMachine machine)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        }

        public OasisScriptHostResult SetActive(OasisScriptReferenceValue objectRef, bool active)
        {
            return Execute("object.set_active", () => _machine.Commands.SetActive(ReferenceId(objectRef, OasisScriptType.ObjectRef), active));
        }

        public OasisScriptHostResult Teleport(OasisScriptReferenceValue objectRef, OasisScriptReferenceValue anchorRef)
        {
            return Execute("object.teleport", () => _machine.Commands.Teleport(ReferenceId(objectRef, OasisScriptType.ObjectRef), ReferenceId(anchorRef, OasisScriptType.AnchorRef)));
        }

        public OasisScriptHostResult TeleportPose(OasisScriptReferenceValue objectRef, OasisScriptVec3Value position, OasisScriptVec3Value rotation)
        {
            return Execute("object.teleport_pose", () => _machine.Commands.Teleport(ReferenceId(objectRef, OasisScriptType.ObjectRef), new RuntimePose(Vector(position), Vector(rotation))));
        }

        public OasisScriptHostResult SetVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity)
        {
            return Execute("object.set_velocity", () => _machine.Commands.SetVelocity(ReferenceId(objectRef, OasisScriptType.ObjectRef), Vector(velocity)));
        }

        public OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity)
        {
            return Execute("object.set_angular_velocity", () => _machine.Commands.SetAngularVelocity(ReferenceId(objectRef, OasisScriptType.ObjectRef), Vector(velocity)));
        }

        public OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue objectRef, OasisScriptVec3Value impulse)
        {
            return Execute("object.apply_impulse", () => _machine.Commands.ApplyImpulse(ReferenceId(objectRef, OasisScriptType.ObjectRef), Vector(impulse)));
        }

        public OasisScriptHostResult ResetObject(OasisScriptReferenceValue objectRef)
        {
            return Execute("object.reset", () => _machine.Commands.ResetObject(ReferenceId(objectRef, OasisScriptType.ObjectRef)));
        }

        public OasisScriptHostResult StartTimer(string name, double seconds)
        {
            return Execute("timer.start", () => _machine.Commands.StartTimer(name, TimerDuration(seconds)));
        }

        public OasisScriptHostResult StopTimer(string name)
        {
            // Stopping an absent A7 timer is intentionally successful.
            return Execute("timer.stop", () => _machine.Commands.StopTimer(name));
        }

        public void Dispose() { _machine = null; }

        private OasisScriptHostResult Execute(string command, Action action)
        {
            if (_machine == null || !_machine.IsActive)
                return OasisScriptHostResult.Fail(command + ": Machine session has been unloaded or detached.");
            try
            {
                action();
                return OasisScriptHostResult.Ok;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is KeyNotFoundException || exception is InvalidOperationException)
            {
                return OasisScriptHostResult.Fail(command + ": " + exception.Message);
            }
            catch (Exception exception)
            {
                return OasisScriptHostResult.Fail(command + ": unexpected " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static string ReferenceId(OasisScriptReferenceValue value, OasisScriptType expected)
        {
            if (value == null || !expected.Equals(value.Type))
                throw new ArgumentException("Expected " + expected + " reference.");
            if (!RuntimeIdentity.IsValid(value.Id))
                throw new ArgumentException("Invalid " + expected + " ID '" + value.Id + "'.");
            return value.Id;
        }

        private static RuntimeVector3 Vector(OasisScriptVec3Value value)
        {
            if (value == null) throw new ArgumentException("Expected Vec3 value.");
            return new RuntimeVector3(Scalar(value.X), Scalar(value.Y), Scalar(value.Z));
        }

        private static float Scalar(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -float.MaxValue || value > float.MaxValue)
                throw new ArgumentException("Numeric value must be finite and fit the A7 float range.");
            return (float)value;
        }

        private static float TimerDuration(double seconds)
        {
            // Check before narrowing: a negative double can otherwise become float -0.
            if (seconds < 0) throw new ArgumentException("Timer duration must be greater than or equal to zero.");
            return Scalar(seconds);
        }
    }
}
