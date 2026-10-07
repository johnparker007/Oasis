using System;
using System.Collections.Generic;
using System.Linq;

namespace Oasis.Scripting
{
    /// <summary>Closed, engine-neutral values. Collections copy their input and expose no mutation.</summary>
    public abstract class OasisScriptValue
    {
        protected OasisScriptValue(OasisScriptType type) { Type = type; }
        public OasisScriptType Type { get; }
        internal abstract bool SameValue(OasisScriptValue other);
    }
    public sealed class OasisScriptNumberValue : OasisScriptValue
    {
        public OasisScriptNumberValue(double value) : base(OasisScriptType.Number)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public double Value { get; }
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptNumberValue number && Value == number.Value;
    }
    public sealed class OasisScriptBoolValue : OasisScriptValue
    {
        public OasisScriptBoolValue(bool value) : base(OasisScriptType.Bool) { Value = value; }
        public bool Value { get; }
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptBoolValue boolean && Value == boolean.Value;
    }
    public sealed class OasisScriptStringValue : OasisScriptValue
    {
        public OasisScriptStringValue(string value) : base(OasisScriptType.String) { Value = value ?? throw new ArgumentNullException(nameof(value)); }
        public string Value { get; }
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptStringValue text && Value == text.Value;
    }
    public sealed class OasisScriptVec3Value : OasisScriptValue
    {
        public OasisScriptVec3Value(double x, double y, double z) : base(OasisScriptType.Vec3)
        { if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentOutOfRangeException(nameof(x)); X = x; Y = y; Z = z; }
        public double X { get; } public double Y { get; } public double Z { get; }
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptVec3Value vector && X == vector.X && Y == vector.Y && Z == vector.Z;
    }
    public sealed class OasisScriptReferenceValue : OasisScriptValue
    {
        public OasisScriptReferenceValue(OasisScriptType type, string id) : base(type)
        {
            if (type.Kind < OasisScriptTypeKind.ObjectRef || type.Kind > OasisScriptTypeKind.SevenSegmentRef) throw new ArgumentException("Expected a reference type.", nameof(type));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Reference ID is required.", nameof(id));
            Id = id;
        }
        public string Id { get; }
        public static OasisScriptReferenceValue Object(string id) => new OasisScriptReferenceValue(OasisScriptType.ObjectRef, id);
        public static OasisScriptReferenceValue Anchor(string id) => new OasisScriptReferenceValue(OasisScriptType.AnchorRef, id);
        public static OasisScriptReferenceValue Trigger(string id) => new OasisScriptReferenceValue(OasisScriptType.TriggerRef, id);
        public static OasisScriptReferenceValue Input(string id) => new OasisScriptReferenceValue(OasisScriptType.InputRef, id);
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptReferenceValue reference && Type.Equals(reference.Type) && Id == reference.Id;
    }
    public sealed class OasisScriptListValue : OasisScriptValue
    {
        public OasisScriptListValue(OasisScriptType elementType, IEnumerable<OasisScriptValue> values) : base(OasisScriptType.ListOf(elementType))
        {
            var copy = values.ToArray();
            if (copy.Any(x => x == null || !x.Type.Equals(elementType))) throw new ArgumentException("List elements must have the declared type.", nameof(values));
            Values = Array.AsReadOnly(copy);
        }
        public IReadOnlyList<OasisScriptValue> Values { get; }
        internal override bool SameValue(OasisScriptValue other)
        {
            if (!(other is OasisScriptListValue list) || !Type.Equals(list.Type) || Values.Count != list.Values.Count) return false;
            for (var i = 0; i < Values.Count; i++) if (!Values[i].SameValue(list.Values[i])) return false;
            return true;
        }
    }
    public sealed class OasisScriptRangeValue : OasisScriptValue
    {
        public OasisScriptRangeValue(double start, double end) : base(OasisScriptType.Range)
        {
            if (double.IsNaN(start) || double.IsNaN(end) || double.IsInfinity(start) || double.IsInfinity(end) || start != Math.Truncate(start) || end != Math.Truncate(end) || start > end)
                throw new ArgumentOutOfRangeException(nameof(start), "Range requires ordered finite integral bounds.");
            Start = start; End = end;
        }
        public double Start { get; } public double End { get; }
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptRangeValue range && Start == range.Start && End == range.End;
    }
    public sealed class OasisScriptVoidValue : OasisScriptValue
    {
        private OasisScriptVoidValue() : base(OasisScriptType.Void) { }
        public static OasisScriptVoidValue Instance { get; } = new OasisScriptVoidValue();
        internal override bool SameValue(OasisScriptValue other) => other is OasisScriptVoidValue;
    }

    public sealed class OasisScriptHostResult
    {
        private OasisScriptHostResult(string? error) { Error = error; }
        public string? Error { get; }
        public bool Success => Error == null;
        public static OasisScriptHostResult Ok { get; } = new OasisScriptHostResult(null);
        public static OasisScriptHostResult Fail(string message) => new OasisScriptHostResult(message ?? "Host command failed.");
    }

    /// <summary>Reference Type distinguishes domains; no CLR/Unity object enters the script.</summary>
    public interface IOasisScriptHost
    {
        OasisScriptHostResult SetActive(OasisScriptReferenceValue objectRef, bool active);
        OasisScriptHostResult Teleport(OasisScriptReferenceValue objectRef, OasisScriptReferenceValue anchorRef);
        OasisScriptHostResult TeleportPose(OasisScriptReferenceValue objectRef, OasisScriptVec3Value position, OasisScriptVec3Value rotation);
        OasisScriptHostResult SetVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity);
        OasisScriptHostResult SetAngularVelocity(OasisScriptReferenceValue objectRef, OasisScriptVec3Value velocity);
        OasisScriptHostResult ApplyImpulse(OasisScriptReferenceValue objectRef, OasisScriptVec3Value impulse);
        OasisScriptHostResult ResetObject(OasisScriptReferenceValue objectRef);
        OasisScriptHostResult StartTimer(string name, double seconds);
        OasisScriptHostResult StopTimer(string name);
    }

    public sealed class OasisScriptEvent
    {
        private OasisScriptEvent(string name, params OasisScriptValue[] arguments) { Name = name; Arguments = Array.AsReadOnly(arguments); }
        public string Name { get; }
        public IReadOnlyList<OasisScriptValue> Arguments { get; }
        private static OasisScriptReferenceValue Require(OasisScriptReferenceValue value, OasisScriptType type)
        { if (value == null || !value.Type.Equals(type)) throw new ArgumentException("Invalid event reference type."); return value; }
        public static OasisScriptEvent MachineStarted() => new OasisScriptEvent("machine.started");
        public static OasisScriptEvent InputPressed(OasisScriptReferenceValue input) => new OasisScriptEvent("input.pressed", Require(input, OasisScriptType.InputRef));
        public static OasisScriptEvent InputReleased(OasisScriptReferenceValue input) => new OasisScriptEvent("input.released", Require(input, OasisScriptType.InputRef));
        public static OasisScriptEvent TriggerEntered(OasisScriptReferenceValue trigger, OasisScriptReferenceValue obj) => new OasisScriptEvent("trigger.entered", Require(trigger, OasisScriptType.TriggerRef), Require(obj, OasisScriptType.ObjectRef));
        public static OasisScriptEvent TriggerExited(OasisScriptReferenceValue trigger, OasisScriptReferenceValue obj) => new OasisScriptEvent("trigger.exited", Require(trigger, OasisScriptType.TriggerRef), Require(obj, OasisScriptType.ObjectRef));
        public static OasisScriptEvent CollisionEntered(OasisScriptReferenceValue obj, OasisScriptReferenceValue other) => new OasisScriptEvent("collision.entered", Require(obj, OasisScriptType.ObjectRef), Require(other, OasisScriptType.ObjectRef));
        public static OasisScriptEvent CollisionExited(OasisScriptReferenceValue obj, OasisScriptReferenceValue other) => new OasisScriptEvent("collision.exited", Require(obj, OasisScriptType.ObjectRef), Require(other, OasisScriptType.ObjectRef));
        public static OasisScriptEvent TimerElapsed(string name) => new OasisScriptEvent("timer.elapsed", new OasisScriptStringValue(name));
    }

    public sealed class OasisScriptRuntimeDiagnostic
    {
        internal OasisScriptRuntimeDiagnostic(string code, string message, string sourceName, OasisScriptTextSpan span, string eventName)
        { Code = code; Message = message; SourceName = sourceName; Span = span; EventName = eventName; }
        public string Code { get; } public string Message { get; } public string SourceName { get; }
        public OasisScriptTextSpan Span { get; } public string EventName { get; }
        public int Line => Span.Line; public int Column => Span.Column;
        public override string ToString() => $"{SourceName}:{Line}:{Column}: {Code} {Message} ({EventName})";
    }
}
