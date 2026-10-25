#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Oasis.Scripting
{
    /// <summary>Independent mutable state for one validated program. Dispatch is synchronous and non-reentrant.</summary>
    public sealed class OasisScriptSession
    {
        private sealed class RuntimeFailure : Exception
        {
            public RuntimeFailure(string code, string message, OasisScriptTextSpan span) : base(message) { Code = code; Span = span; }
            public string Code { get; } public OasisScriptTextSpan Span { get; }
        }
        private sealed class FunctionReturn : Exception { public FunctionReturn(OasisScriptValue? value) { Value = value; } public OasisScriptValue? Value { get; } }
        private readonly OasisScriptProgram _program;
        private readonly IOasisScriptHost _host;
        private readonly int _limit;
        private readonly Dictionary<string, OasisScriptValue> _constants = new Dictionary<string, OasisScriptValue>(StringComparer.Ordinal);
        private readonly Dictionary<string, OasisScriptValue> _states = new Dictionary<string, OasisScriptValue>(StringComparer.Ordinal);
        private readonly Dictionary<string, OasisScriptFunctionSyntax> _functions = new Dictionary<string, OasisScriptFunctionSyntax>(StringComparer.Ordinal);
        private readonly Stack<Dictionary<string, OasisScriptValue>> _scopes = new Stack<Dictionary<string, OasisScriptValue>>();
        private int _remaining;
        private string _eventName = "initialization";
        private bool _dispatching;
        private int _functionDepth;
        private readonly Stack<OasisScriptType> _functionReturnTypes = new Stack<OasisScriptType>();
        private readonly Stack<int> _functionCallerScopeCounts = new Stack<int>();

        public OasisScriptSession(OasisScriptProgram program, IOasisScriptHost host, OasisScriptExecutionOptions? options = null)
        {
            _program = program ?? throw new ArgumentNullException(nameof(program));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _limit = (options ?? new OasisScriptExecutionOptions()).MaxInstructionsPerEvent;
            _remaining = _limit;
            foreach (var function in program.Functions) _functions.Add(function.Name, function);
            try
            {
                foreach (var constant in program.Constants) { Charge(constant.Initializer.Span); _constants.Add(constant.Name, Evaluate(constant.Initializer, constant.Type)); }
                foreach (var state in program.States) { Charge(state.Initializer.Span); _states.Add(state.Name, Evaluate(state.Initializer, state.Type)); }
            }
            catch (RuntimeFailure failure) { Fault(failure); }
        }
        public bool IsFaulted { get; private set; }
        public OasisScriptRuntimeDiagnostic? LastRuntimeDiagnostic { get; private set; }
        public bool TryGetState(string name, out OasisScriptValue value) => _states.TryGetValue(name, out value!);
        public bool TryGetConstant(string name, out OasisScriptValue value) => _constants.TryGetValue(name, out value!);

        /// <summary>False after the first fault; later calls execute nothing and retain the original diagnostic.</summary>
        public bool Dispatch(OasisScriptEvent scriptEvent)
        {
            if (scriptEvent == null) throw new ArgumentNullException(nameof(scriptEvent));
            if (IsFaulted) return false;
            if (_dispatching) throw new InvalidOperationException("Script event dispatch cannot be reentrant.");
            _dispatching = true; _eventName = scriptEvent.Name; _remaining = _limit;
            var dispatchHost = _host as IOasisScriptDispatchHost;
            try { dispatchHost?.BeginDispatch(); }
            catch (Exception exception) { _dispatching = false; Fault(new RuntimeFailure("OSR1005", $"Host dispatch setup failed: {exception.Message}", new OasisScriptTextSpan(0, 0, 1, 1))); return false; }
            try
            {
                foreach (var handler in _program.EventHandlers)
                {
                    if (handler.EventName != scriptEvent.Name) continue;
                    Charge(handler.Body.Span); // Includes empty handlers/filter attempts in the bound.
                    var matches = true;
                    for (var i = 0; i < handler.Parameters.Count; i++)
                    {
                        var parameter = handler.Parameters[i];
                        if (parameter.Filter != null && !Evaluate(parameter.Filter).SameValue(scriptEvent.Arguments[i])) { matches = false; break; }
                    }
                    if (!matches) continue;
                    PushScope();
                    try
                    {
                        for (var i = 0; i < handler.Parameters.Count; i++)
                            if (handler.Parameters[i].BindingName != null) _scopes.Peek().Add(handler.Parameters[i].BindingName!, scriptEvent.Arguments[i]);
                        ExecuteBlock(handler.Body, false);
                    }
                    finally { _scopes.Pop(); }
                }
                return true;
            }
            catch (RuntimeFailure failure) { Fault(failure); return false; }
            finally { _scopes.Clear(); _dispatching = false; try { dispatchHost?.EndDispatch(); } catch { /* Preserve the original script result/diagnostic. */ } }
        }
        private void Fault(RuntimeFailure failure)
        {
            IsFaulted = true;
            LastRuntimeDiagnostic = new OasisScriptRuntimeDiagnostic(failure.Code, failure.Message, _program.SourceName, failure.Span, _eventName);
        }
        private void Charge(OasisScriptTextSpan span)
        {
            if (--_remaining < 0) throw new RuntimeFailure("OSR1001", $"Execution limit exceeded while handling {_eventName}.", span);
        }
        private void PushScope() => _scopes.Push(new Dictionary<string, OasisScriptValue>(StringComparer.Ordinal));
        private void ExecuteBlock(OasisScriptBlockSyntax block, bool scope = true)
        {
            Charge(block.Span);
            if (scope) PushScope();
            try { foreach (var statement in block.Statements) Execute(statement); }
            finally { if (scope) _scopes.Pop(); }
        }
        private void Execute(OasisScriptStatementSyntax statement)
        {
            // Blocks charge themselves, once.
            if (statement is OasisScriptBlockSyntax nested) { ExecuteBlock(nested); return; }
            Charge(statement.Span);
            switch (statement)
            {
                case OasisScriptLetStatementSyntax let: _scopes.Peek().Add(let.Name, Evaluate(let.Initializer)); break;
                case OasisScriptAssignmentStatementSyntax assignment:
                    if (!_states.TryGetValue(assignment.Name, out var previous))
                        throw new RuntimeFailure("OSR1007", "Invalid state assignment in compiled program.", assignment.Span);
                    var value = Evaluate(assignment.Value, previous.Type);
                    if (!previous.Type.Equals(value.Type)) throw new RuntimeFailure("OSR1007", "Invalid state assignment in compiled program.", assignment.Span);
                    _states[assignment.Name] = value; break;
                case OasisScriptIfStatementSyntax conditional:
                    if (Boolean(Evaluate(conditional.Condition))) ExecuteBlock(conditional.ThenBlock);
                    else if (conditional.ElseBlock != null) ExecuteBlock(conditional.ElseBlock);
                    break;
                case OasisScriptForStatementSyntax loop:
                    var iterable = Evaluate(loop.Iterable);
                    if (iterable is OasisScriptListValue list)
                        foreach (var item in list.Values) Iterate(loop, item);
                    else if (iterable is OasisScriptRangeValue range)
                    {
                        // Exact integer advancement also works above double's unit-resolution domain.
                        var end = new BigInteger(range.End);
                        for (var i = new BigInteger(range.Start); i < end; i++) Iterate(loop, Number((double)i, loop.Span));
                    }
                    else throw new RuntimeFailure("OSR1007", "Invalid iterable in compiled program.", loop.Span);
                    break;
                case OasisScriptExpressionStatementSyntax expression: Evaluate(expression.Expression); break;
                case OasisScriptReturnStatementSyntax ret: throw new FunctionReturn(ret.Expression == null ? null : Evaluate(ret.Expression, _functionReturnTypes.Count > 0 ? _functionReturnTypes.Peek() : null));
                default: throw new RuntimeFailure("OSR1007", "Unsupported compiled statement.", statement.Span);
            }
        }
        private void Iterate(OasisScriptForStatementSyntax loop, OasisScriptValue value)
        {
            Charge(loop.Span); PushScope();
            try { _scopes.Peek().Add(loop.Variable, value); ExecuteBlock(loop.Body, false); }
            finally { _scopes.Pop(); }
        }
        private OasisScriptValue Evaluate(OasisScriptExpressionSyntax expression, OasisScriptType? expectedType = null)
        {
            Charge(expression.Span);
            switch (expression)
            {
                case OasisScriptLiteralExpressionSyntax literal:
                    if (literal.Type.Equals(OasisScriptType.Number)) return Number((double)literal.Value, literal.Span);
                    if (literal.Type.Equals(OasisScriptType.Bool)) return new OasisScriptBoolValue((bool)literal.Value);
                    return new OasisScriptStringValue((string)literal.Value);
                case OasisScriptReferenceLiteralSyntax reference:
                    if (reference.Type == OasisScriptType.OasisObjectRef)
                    {
                        var valueHost = _host as IOasisScriptValueHost;
                        if (valueHost == null) throw new RuntimeFailure("OSR1005", "The host cannot resolve authored Oasis object references.", reference.Span);
                        var resolved = HostValue(valueHost.ResolveObject(reference.Id), OasisScriptType.OasisObjectRef, "oasis reference resolution", reference.Span);
                        if (((OasisScriptReferenceValue)resolved).Id != reference.Id) throw new RuntimeFailure("OSR1005", $"Host resolved oasis:{reference.Id} to a different object ID.", reference.Span);
                        return resolved;
                    }
                    return new OasisScriptReferenceValue(reference.Type, reference.Id);
                case OasisScriptIdentifierExpressionSyntax identifier:
                    var seenScopes = 0;
                    foreach (var scope in _scopes)
                    {
                        if (_functionCallerScopeCounts.Count > 0 && seenScopes >= _scopes.Count - _functionCallerScopeCounts.Peek()) break;
                        if (scope.TryGetValue(identifier.Name, out var local)) return local;
                        seenScopes++;
                    }
                    if (_states.TryGetValue(identifier.Name, out var state)) return state;
                    if (_constants.TryGetValue(identifier.Name, out var constant)) return constant;
                    throw new RuntimeFailure("OSR1007", $"Unknown compiled identifier '{identifier.Name}'.", identifier.Span);
                case OasisScriptListExpressionSyntax list:
                    if (list.Elements.Count == 0)
                    {
                        if (expectedType?.Kind != OasisScriptTypeKind.List) throw new RuntimeFailure("OSR1007", "Empty list has no runtime element type.", list.Span);
                        return new OasisScriptListValue(expectedType.ElementType!, Array.Empty<OasisScriptValue>());
                    }
                    var values = list.Elements.Select(item => Evaluate(item)).ToArray();
                    return new OasisScriptListValue(values[0].Type, values);
                case OasisScriptIndexExpressionSyntax index:
                    var target = (OasisScriptListValue)Evaluate(index.Target); var offset = Numeric(Evaluate(index.Index));
                    if (double.IsNaN(offset) || double.IsInfinity(offset) || offset != Math.Truncate(offset) || offset < 0 || offset >= target.Values.Count)
                        throw new RuntimeFailure("OSR1004", "List index must be finite, integral and within bounds.", index.Index.Span);
                    return target.Values[(int)offset];
                case OasisScriptUnaryExpressionSyntax unary:
                    var operand = Evaluate(unary.Operand);
                    return unary.Operator == "not" ? (OasisScriptValue)new OasisScriptBoolValue(!Boolean(operand)) : Number(-Numeric(operand), unary.Span);
                case OasisScriptBinaryExpressionSyntax binary: return Binary(binary);
                case OasisScriptBuiltinCallExpressionSyntax call: return Invoke(call);
                default: throw new RuntimeFailure("OSR1007", "Unsupported compiled expression.", expression.Span);
            }
        }
        private OasisScriptValue Binary(OasisScriptBinaryExpressionSyntax binary)
        {
            var left = Evaluate(binary.Left);
            if (binary.Operator == "and" && !Boolean(left)) return new OasisScriptBoolValue(false);
            if (binary.Operator == "or" && Boolean(left)) return new OasisScriptBoolValue(true);
            var right = Evaluate(binary.Right);
            switch (binary.Operator)
            {
                case "and": case "or": return new OasisScriptBoolValue(Boolean(right));
                case "==": return new OasisScriptBoolValue(left.SameValue(right));
                case "!=": return new OasisScriptBoolValue(!left.SameValue(right));
            }
            var a = Numeric(left); var b = Numeric(right);
            switch (binary.Operator)
            {
                case "+": return Number(a + b, binary.Span);
                case "-": return Number(a - b, binary.Span);
                case "*": return Number(a * b, binary.Span);
                case "/": if (b == 0) throw new RuntimeFailure("OSR1002", "Division by zero.", binary.Span); return Number(a / b, binary.Span);
                case "%": if (b == 0) throw new RuntimeFailure("OSR1002", "Modulo by zero.", binary.Span); return Number(a % b, binary.Span);
                case "<": return new OasisScriptBoolValue(a < b);
                case "<=": return new OasisScriptBoolValue(a <= b);
                case ">": return new OasisScriptBoolValue(a > b);
                case ">=": return new OasisScriptBoolValue(a >= b);
                default: throw new RuntimeFailure("OSR1007", "Unknown compiled operator.", binary.Span);
            }
        }
        private OasisScriptValue Invoke(OasisScriptBuiltinCallExpressionSyntax call)
        {
            Charge(call.Span);
            if (_functions.TryGetValue(call.Name, out var function)) return InvokeFunction(function, call);
            OasisScriptValue[] args;
            if (call.Name == "list.append" && call.Arguments.Count == 2)
            {
                var source = Evaluate(call.Arguments[0]) as OasisScriptListValue;
                args = new[] { (OasisScriptValue?)source ?? throw new RuntimeFailure("OSR1007", "Expected a list.", call.Span), Evaluate(call.Arguments[1], source.Type.ElementType) };
            }
            else if (call.Name == "list.count" && call.Arguments.Count == 1) args = new[] { Evaluate(call.Arguments[0]) };
            else if (call.Name == "list.contains" && call.Arguments.Count == 2)
            {
                var source = Evaluate(call.Arguments[0]) as OasisScriptListValue;
                args = new[] { (OasisScriptValue?)source ?? throw new RuntimeFailure("OSR1007", "Expected a list.", call.Span), Evaluate(call.Arguments[1], source.Type.ElementType) };
            }
            else args = call.Arguments.Select(item => Evaluate(item)).ToArray();
            foreach (var reference in args.OfType<OasisScriptReferenceValue>())
                if (reference.Type == OasisScriptType.OasisObjectRef && !reference.IsLiveObject)
                    throw new RuntimeFailure("OSR1005", $"Host command '{call.Name}' requires a session-scoped live object reference.", call.Span);
            if (call.Name == "vec3") return new OasisScriptVec3Value(Numeric(args[0]), Numeric(args[1]), Numeric(args[2]));
            if (call.Name == "range")
            {
                try { return new OasisScriptRangeValue(Numeric(args[0]), Numeric(args[1])); }
                catch (ArgumentOutOfRangeException) { throw new RuntimeFailure("OSR1006", "Range bounds must be ordered finite integers.", call.Span); }
            }
            if (call.Name == "math.sqrt")
            {
                var input = Numeric(args[0]);
                if (input < 0) throw new RuntimeFailure("OSR1006", "math.sqrt requires a nonnegative number.", call.Span);
                return Number(Math.Sqrt(input), call.Span);
            }
            if (call.Name == "require")
            {
                if (!Boolean(args[0])) throw new RuntimeFailure("OSR1006", ((OasisScriptStringValue)args[1]).Value, call.Span);
                return OasisScriptVoidValue.Instance;
            }
            if (call.Name == "list.count") return Number(((OasisScriptListValue)args[0]).Values.Count, call.Span);
            if (call.Name == "list.contains") return new OasisScriptBoolValue(((OasisScriptListValue)args[0]).Values.Any(item => item.SameValue(args[1])));
            if (call.Name == "list.append")
            {
                var source = (OasisScriptListValue)args[0];
                if (source.Values.Count >= OasisScriptListValue.MaximumCount) throw new RuntimeFailure("OSR1006", $"Lists are limited to {OasisScriptListValue.MaximumCount} items.", call.Span);
                return new OasisScriptListValue(source.Type.ElementType!, source.Values.Concat(new[] { args[1] }));
            }
            if (call.Name == "oasis.instantiate" || call.Name == "oasis.destroy" || call.Name == "oasis.is_alive" || call.Name.StartsWith("frame.", StringComparison.Ordinal) || call.Name.StartsWith("collider.", StringComparison.Ordinal) || call.Name.StartsWith("surface.", StringComparison.Ordinal))
            {
                var valueHost = _host as IOasisScriptValueHost;
                if (valueHost == null) throw new RuntimeFailure("OSR1005", $"Host does not support '{call.Name}'.", call.Span);
                OasisScriptHostResult query;
                try { query = valueHost.Query(call.Name, Array.AsReadOnly(args)); }
                catch (Exception exception) { throw new RuntimeFailure("OSR1005", $"Host command '{call.Name}' failed: {exception.Message}", call.Span); }
                return HostValue(query, SignatureReturnType(call.Name, args), call.Name, call.Span);
            }
            OasisScriptHostResult result;
            try
            {
                switch (call.Name)
                {
                    case "oasis.set_active": result = _host.SetActive((OasisScriptReferenceValue)args[0], Boolean(args[1])); break;
                    case "oasis.teleport": result = _host.Teleport((OasisScriptReferenceValue)args[0], (OasisScriptReferenceValue)args[1]); break;
                    case "oasis.teleport_pose": result = _host.TeleportPose((OasisScriptReferenceValue)args[0], (OasisScriptVec3Value)args[1], (OasisScriptVec3Value)args[2]); break;
                    case "oasis.set_velocity": result = _host.SetVelocity((OasisScriptReferenceValue)args[0], (OasisScriptVec3Value)args[1]); break;
                    case "oasis.set_angular_velocity": result = _host.SetAngularVelocity((OasisScriptReferenceValue)args[0], (OasisScriptVec3Value)args[1]); break;
                    case "oasis.apply_impulse": result = _host.ApplyImpulse((OasisScriptReferenceValue)args[0], (OasisScriptVec3Value)args[1]); break;
                    case "oasis.reset": result = _host.ResetObject((OasisScriptReferenceValue)args[0]); break;
                    case "timer.start": result = _host.StartTimer(((OasisScriptStringValue)args[0]).Value, Numeric(args[1])); break;
                    case "timer.stop": result = _host.StopTimer(((OasisScriptStringValue)args[0]).Value); break;
                    default: throw new RuntimeFailure("OSR1007", "Unknown compiled built-in.", call.Span);
                }
            }
            catch (RuntimeFailure) { throw; }
            catch (Exception exception) { throw new RuntimeFailure("OSR1005", $"Host command '{call.Name}' failed: {exception.Message}", call.Span); }
            if (result == null || !result.Success) throw new RuntimeFailure("OSR1005", $"Host command '{call.Name}' failed: {result?.Error ?? "missing host result"}", call.Span);
            if (result.Value != null) throw new RuntimeFailure("OSR1005", $"Host command '{call.Name}' returned a value but was declared Void.", call.Span);
            return OasisScriptVoidValue.Instance;
        }
        private OasisScriptValue InvokeFunction(OasisScriptFunctionSyntax function, OasisScriptBuiltinCallExpressionSyntax call)
        {
            if (_functionDepth >= 16) throw new RuntimeFailure("OSR1001", "Function call depth is limited to 16.", call.Span);
            var arguments = new OasisScriptValue[call.Arguments.Count];
            for (var i = 0; i < call.Arguments.Count; i++) arguments[i] = Evaluate(call.Arguments[i], i < function.Parameters.Count ? function.Parameters[i].Type : null);
            _functionDepth++; _functionCallerScopeCounts.Push(_scopes.Count); PushScope();
            try
            {
                for (var i = 0; i < function.Parameters.Count; i++) _scopes.Peek().Add(function.Parameters[i].Name, arguments[i]);
                _functionReturnTypes.Push(function.ReturnType);
                try { ExecuteBlock(function.Body, false); }
                catch (FunctionReturn returned)
                {
                    if (function.ReturnType == OasisScriptType.Void) return OasisScriptVoidValue.Instance;
                    if (returned.Value == null || !returned.Value.Type.Equals(function.ReturnType)) throw new RuntimeFailure("OSR1007", $"Function '{function.Name}' returned an invalid value.", call.Span);
                    return returned.Value;
                }
                if (function.ReturnType == OasisScriptType.Void) return OasisScriptVoidValue.Instance;
                throw new RuntimeFailure("OSR1007", $"Function '{function.Name}' did not return a value.", call.Span);
            }
            finally { if (_functionReturnTypes.Count > 0) _functionReturnTypes.Pop(); _scopes.Pop(); _functionCallerScopeCounts.Pop(); _functionDepth--; }
        }
        private OasisScriptValue HostValue(OasisScriptHostResult? result, OasisScriptType expected, string operation, OasisScriptTextSpan span)
        {
            if (result == null || !result.Success) throw new RuntimeFailure("OSR1005", $"Host command '{operation}' failed: {result?.Error ?? "missing host result"}", span);
            if (expected == OasisScriptType.Void)
            {
                if (result.Value != null) throw new RuntimeFailure("OSR1005", $"Host command '{operation}' returned a value but was declared Void.", span);
                return OasisScriptVoidValue.Instance;
            }
            if (result.Value == null || !result.Value.Type.Equals(expected)) throw new RuntimeFailure("OSR1005", $"Host command '{operation}' returned {result.Value?.Type.ToString() ?? "no value"}; expected {expected}.", span);
            if (expected == OasisScriptType.OasisObjectRef && result.Value is OasisScriptReferenceValue reference && !reference.IsLiveObject) throw new RuntimeFailure("OSR1005", $"Host command '{operation}' returned an unscoped object reference.", span);
            return result.Value;
        }
        private static OasisScriptType SignatureReturnType(string operation, OasisScriptValue[] args) => operation switch
        {
            "oasis.instantiate" => OasisScriptType.OasisObjectRef,
            "oasis.destroy" => OasisScriptType.Void,
            "oasis.is_alive" => OasisScriptType.Bool,
            "frame.position" or "frame.rotation" => OasisScriptType.Vec3,
            "collider.sphere_radius" => OasisScriptType.Number,
            "surface.contains_sphere" => OasisScriptType.Bool,
            _ => OasisScriptType.Invalid
        };
        private static double Numeric(OasisScriptValue value) => ((OasisScriptNumberValue)value).Value;
        private static bool Boolean(OasisScriptValue value) => ((OasisScriptBoolValue)value).Value;
        private static OasisScriptNumberValue Number(double value, OasisScriptTextSpan span)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new RuntimeFailure("OSR1003", "Arithmetic produced a non-finite number.", span);
            return new OasisScriptNumberValue(value);
        }
    }
}
