using System;
using Oasis.Scripting;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>One Machine-owned, synchronous A7 event subscription and script session.</summary>
    public sealed class RuntimeOasisScriptBehavior : IDisposable
    {
        private RuntimeMachine _machine;
        private RuntimeMachineOasisScriptHost _host;
        private OasisScriptSession _session;
        private Action<string> _reportFault;
        private readonly string _machineName;

        private RuntimeOasisScriptBehavior(RuntimeMachine machine, Action<string> reportFault)
        {
            _machine = machine;
            _machineName = string.IsNullOrWhiteSpace(machine.Build.Machine.displayName) ? machine.Build.Machine.machineId : machine.Build.Machine.displayName;
            _reportFault = reportFault ?? (message => Debug.LogError(message));
            _host = new RuntimeMachineOasisScriptHost(machine);
            _session = new OasisScriptSession(machine.Build.ScriptProgram, _host);
        }

        public bool IsFaulted { get { return LastRuntimeDiagnostic != null; } }
        public bool IsDisposed { get; private set; }
        public OasisScriptRuntimeDiagnostic LastRuntimeDiagnostic { get; private set; }

        /// <summary>Call after content setup, before CompleteStartup. Emulation has no script attachment.</summary>
        public static RuntimeOasisScriptBehavior AttachTo(RuntimeMachine machine, Action<string> reportFault = null)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (machine.Build.Machine.runtime.kind != "Oasis") return null;
            if (!machine.IsActive || machine.HasStarted) throw new InvalidOperationException("Oasis Script must attach to an active Machine before CompleteStartup.");
            if (machine.Build.ScriptProgram == null) throw new InvalidOperationException("Oasis Machine requires a compiled behavior/behavior.oasis program.");

            var behavior = new RuntimeOasisScriptBehavior(machine, reportFault);
            try
            {
                if (behavior._session.IsFaulted)
                {
                    behavior.ReportFault();
                    throw new InvalidOperationException("Oasis Script initialization failed in Machine '" + behavior._machineName + "': " + behavior.LastRuntimeDiagnostic);
                }
                machine.AttachBehaviorRuntime(behavior);
                machine.Events.Subscribe(behavior.OnRuntimeEvent);
                return behavior;
            }
            catch
            {
                behavior.Dispose();
                throw;
            }
        }

        private void OnRuntimeEvent(RuntimeMachineEvent value)
        {
            // The A7 publisher may already have snapshotted this callback when unload occurs.
            if (IsDisposed || IsFaulted || !_machine.IsActive) return;
            if (!TryTranslateEvent(_machine, value, out var scriptEvent)) return;
            _session.Dispatch(scriptEvent);
            if (_session.IsFaulted) ReportFault();
        }

        private void ReportFault()
        {
            if (LastRuntimeDiagnostic != null) return;
            LastRuntimeDiagnostic = _session.LastRuntimeDiagnostic;
            var diagnostic = LastRuntimeDiagnostic;
            _reportFault("Oasis Script fault in Machine '" + _machineName + "': " + diagnostic.SourceName + ":" + diagnostic.Line + ":" + diagnostic.Column + " " + diagnostic.Code + " " + diagnostic.Message + " while handling " + diagnostic.EventName);
        }

        internal static bool TryTranslateEvent(RuntimeMachine machine, RuntimeMachineEvent value, out OasisScriptEvent scriptEvent)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            switch (value)
            {
                case RuntimeMachineStartedEvent _:
                    scriptEvent = OasisScriptEvent.MachineStarted(); break;
                case RuntimeInputPressedEvent input:
                    scriptEvent = OasisScriptEvent.InputPressed(OasisScriptReferenceValue.Input(input.InputId)); break;
                case RuntimeInputReleasedEvent input:
                    scriptEvent = OasisScriptEvent.InputReleased(OasisScriptReferenceValue.Input(input.InputId)); break;
                case RuntimeTriggerEnteredEvent trigger:
                    if (!machine.TryGetObject(trigger.ObjectId, out _)) { scriptEvent = null; return false; }
                    scriptEvent = OasisScriptEvent.TriggerEntered(OasisScriptReferenceValue.Trigger(trigger.TriggerId), machine.GetObjectReference(trigger.ObjectId)); break;
                case RuntimeTriggerExitedEvent trigger:
                    if (!machine.TryGetObject(trigger.ObjectId, out _)) { scriptEvent = null; return false; }
                    scriptEvent = OasisScriptEvent.TriggerExited(OasisScriptReferenceValue.Trigger(trigger.TriggerId), machine.GetObjectReference(trigger.ObjectId)); break;
                case RuntimeCollisionEnteredEvent collision:
                    if (!machine.TryGetObject(collision.ObjectId, out _) || !machine.TryGetObject(collision.OtherObjectId, out _)) { scriptEvent = null; return false; }
                    scriptEvent = OasisScriptEvent.CollisionEntered(machine.GetObjectReference(collision.ObjectId), machine.GetObjectReference(collision.OtherObjectId)); break;
                case RuntimeCollisionExitedEvent collision:
                    if (!machine.TryGetObject(collision.ObjectId, out _) || !machine.TryGetObject(collision.OtherObjectId, out _)) { scriptEvent = null; return false; }
                    scriptEvent = OasisScriptEvent.CollisionExited(machine.GetObjectReference(collision.ObjectId), machine.GetObjectReference(collision.OtherObjectId)); break;
                case RuntimeTimerElapsedEvent timer:
                    scriptEvent = OasisScriptEvent.TimerElapsed(timer.TimerId); break;
                default:
                    scriptEvent = null; return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _machine.Events.Unsubscribe(OnRuntimeEvent);
            _host.Dispose();
            _session = null;
            _host = null;
            _machine = null;
            _reportFault = null;
        }
    }
}
