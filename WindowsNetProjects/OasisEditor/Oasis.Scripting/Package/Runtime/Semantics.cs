#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Oasis.Scripting
{
    public sealed class OasisScriptBuiltinSignature
    {
        public OasisScriptBuiltinSignature(string name, OasisScriptType returnType, params OasisScriptType[] parameters) { Name = name; ReturnType = returnType; Parameters = Array.AsReadOnly(parameters.ToArray()); }
        public string Name { get; }
        public OasisScriptType ReturnType { get; }
        public IReadOnlyList<OasisScriptType> Parameters { get; }
    }

    public sealed class OasisScriptBuiltinRegistry
    {
        private readonly IReadOnlyDictionary<string, OasisScriptBuiltinSignature> _signatures;
        public OasisScriptBuiltinRegistry(IEnumerable<OasisScriptBuiltinSignature> signatures) { _signatures = new ReadOnlyDictionary<string, OasisScriptBuiltinSignature>(signatures.ToDictionary(x => x.Name, StringComparer.Ordinal)); }
        public bool TryGet(string name, out OasisScriptBuiltinSignature signature) => _signatures.TryGetValue(name, out signature!);
        public IEnumerable<OasisScriptBuiltinSignature> Signatures => _signatures.Values;
        public static OasisScriptBuiltinRegistry CreateDefault() => new OasisScriptBuiltinRegistry(new[]
        {
            new OasisScriptBuiltinSignature("vec3", OasisScriptType.Vec3, OasisScriptType.Number, OasisScriptType.Number, OasisScriptType.Number),
            new OasisScriptBuiltinSignature("range", OasisScriptType.Range, OasisScriptType.Number, OasisScriptType.Number),
            new OasisScriptBuiltinSignature("object.set_active", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.Bool),
            new OasisScriptBuiltinSignature("object.teleport", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.AnchorRef),
            new OasisScriptBuiltinSignature("object.teleport_pose", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.Vec3, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("object.set_velocity", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("object.set_angular_velocity", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("object.apply_impulse", OasisScriptType.Void, OasisScriptType.ObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("object.reset", OasisScriptType.Void, OasisScriptType.ObjectRef),
            new OasisScriptBuiltinSignature("timer.start", OasisScriptType.Void, OasisScriptType.String, OasisScriptType.Number),
            new OasisScriptBuiltinSignature("timer.stop", OasisScriptType.Void, OasisScriptType.String)
        });
    }

    public sealed class OasisScriptVariable
    {
        internal OasisScriptVariable(string name, OasisScriptType type, OasisScriptExpressionSyntax initializer) { Name = name; Type = type; Initializer = initializer; }
        public string Name { get; } public OasisScriptType Type { get; } public OasisScriptExpressionSyntax Initializer { get; }
    }
    public sealed class OasisScriptEventParameter
    {
        internal OasisScriptEventParameter(OasisScriptType type, string? bindingName, OasisScriptExpressionSyntax? filter) { Type = type; BindingName = bindingName; Filter = filter; }
        public OasisScriptType Type { get; } public string? BindingName { get; } public OasisScriptExpressionSyntax? Filter { get; } public bool IsBinding => BindingName != null;
    }
    public sealed class OasisScriptEventHandler
    {
        internal OasisScriptEventHandler(int sourceOrder, string eventName, IReadOnlyList<OasisScriptEventParameter> parameters, OasisScriptBlockSyntax body) { SourceOrder = sourceOrder; EventName = eventName; Parameters = Array.AsReadOnly(parameters.ToArray()); Body = body; }
        public int SourceOrder { get; } public string EventName { get; } public IReadOnlyList<OasisScriptEventParameter> Parameters { get; } public OasisScriptBlockSyntax Body { get; }
    }
    public sealed class OasisScriptProgram
    {
        internal OasisScriptProgram(OasisScriptProgramSyntax syntax, IReadOnlyList<OasisScriptVariable> constants, IReadOnlyList<OasisScriptVariable> states, IReadOnlyList<OasisScriptEventHandler> handlers, string sourceName) { SourceName = sourceName; Syntax = syntax; Constants = Array.AsReadOnly(constants.ToArray()); States = Array.AsReadOnly(states.ToArray()); EventHandlers = Array.AsReadOnly(handlers.ToArray()); }
        public string SourceName { get; }
        public OasisScriptProgramSyntax Syntax { get; } public IReadOnlyList<OasisScriptVariable> Constants { get; } public IReadOnlyList<OasisScriptVariable> States { get; } public IReadOnlyList<OasisScriptEventHandler> EventHandlers { get; }
    }
    public sealed class OasisScriptExecutionOptions
    {
        public OasisScriptExecutionOptions(int maxInstructionsPerEvent = 10000) { if (maxInstructionsPerEvent <= 0) throw new ArgumentOutOfRangeException(nameof(maxInstructionsPerEvent)); MaxInstructionsPerEvent = maxInstructionsPerEvent; }
        public int MaxInstructionsPerEvent { get; }
    }

    public sealed class OasisScriptSemanticAnalyzer
    {
        private enum SymbolKind { Const, State, Local }
        private sealed class Symbol
        {
            public Symbol(string name, OasisScriptType type, SymbolKind kind) { Name = name; Type = type; Kind = kind; }
            public string Name;
            public OasisScriptType Type;
            public SymbolKind Kind;
            public bool IsState => Kind == SymbolKind.State;
        }
        private static readonly IReadOnlyDictionary<string, OasisScriptType[]> Events = new Dictionary<string, OasisScriptType[]>(StringComparer.Ordinal)
        {
            ["machine.started"] = new OasisScriptType[0], ["input.pressed"] = new[] { OasisScriptType.InputRef }, ["input.released"] = new[] { OasisScriptType.InputRef },
            ["trigger.entered"] = new[] { OasisScriptType.TriggerRef, OasisScriptType.ObjectRef }, ["trigger.exited"] = new[] { OasisScriptType.TriggerRef, OasisScriptType.ObjectRef },
            ["collision.entered"] = new[] { OasisScriptType.ObjectRef, OasisScriptType.ObjectRef }, ["collision.exited"] = new[] { OasisScriptType.ObjectRef, OasisScriptType.ObjectRef }, ["timer.elapsed"] = new[] { OasisScriptType.String }
        };
        private readonly string _sourceName; private readonly OasisScriptBuiltinRegistry _builtins; private readonly List<OasisScriptDiagnostic> _diagnostics = new List<OasisScriptDiagnostic>(); private readonly Dictionary<string, Symbol> _globals = new Dictionary<string, Symbol>(StringComparer.Ordinal); private readonly Stack<Dictionary<string, Symbol>> _scopes = new Stack<Dictionary<string, Symbol>>();
        public OasisScriptSemanticAnalyzer(string sourceName, OasisScriptBuiltinRegistry builtins) { _sourceName = sourceName; _builtins = builtins; }
        public IReadOnlyList<OasisScriptDiagnostic> Diagnostics => _diagnostics;
        private void Report(string code, string message, OasisScriptTextSpan span) => _diagnostics.Add(new OasisScriptDiagnostic(code, OasisScriptDiagnosticSeverity.Error, message, _sourceName, span));

        public OasisScriptProgram Analyze(OasisScriptProgramSyntax syntax)
        {
            var constants = new List<OasisScriptVariable>(); var states = new List<OasisScriptVariable>(); var handlers = new List<OasisScriptEventHandler>();
            foreach (var declaration in syntax.Declarations)
            {
                if (declaration is OasisScriptVariableDeclarationSyntax variable)
                {
                    var type = TypeOf(variable.Initializer, true);
                    if (type == OasisScriptType.Void) Report(DiagnosticCodes.TypeMismatch, "A Void built-in cannot be used as a value.", variable.Initializer.Span);
                    if (_globals.ContainsKey(variable.Name)) Report(DiagnosticCodes.DuplicateDeclaration, $"'{variable.Name}' is already declared.", variable.Span);
                    else { _globals.Add(variable.Name, new Symbol(variable.Name, type, variable.IsState ? SymbolKind.State : SymbolKind.Const)); var model = new OasisScriptVariable(variable.Name, type, variable.Initializer); if (variable.IsState) states.Add(model); else constants.Add(model); }
                }
                else if (declaration is OasisScriptEventHandlerSyntax handler) handlers.Add(AnalyzeHandler(handler, handlers.Count));
            }
            return new OasisScriptProgram(syntax, constants, states, handlers, _sourceName);
        }

        private OasisScriptEventHandler AnalyzeHandler(OasisScriptEventHandlerSyntax syntax, int sourceOrder)
        {
            if (!Events.TryGetValue(syntax.EventName, out var signature)) { Report(DiagnosticCodes.InvalidEventPattern, $"Unknown event '{syntax.EventName}'.", syntax.Span); signature = new OasisScriptType[syntax.Parameters.Count]; for (var i = 0; i < signature.Length; i++) signature[i] = OasisScriptType.Invalid; }
            if (signature.Length != syntax.Parameters.Count) Report(DiagnosticCodes.InvalidEventPattern, $"Event '{syntax.EventName}' expects {signature.Length} parameter(s), not {syntax.Parameters.Count}.", syntax.Span);
            PushScope(); var parameters = new List<OasisScriptEventParameter>();
            for (var i = 0; i < syntax.Parameters.Count; i++)
            {
                var expected = i < signature.Length ? signature[i] : OasisScriptType.Invalid; var parameter = syntax.Parameters[i];
                if (parameter is OasisScriptEventBindingSyntax binding) { DeclareLocal(binding.Name, expected, binding.Span); parameters.Add(new OasisScriptEventParameter(expected, binding.Name, null)); }
                else { var filter = ((OasisScriptEventFilterSyntax)parameter).Value; var actual = TypeOf(filter, false); if (!actual.Equals(expected) && actual != OasisScriptType.Invalid && expected != OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidEventPattern, $"Event filter must be {expected}, not {actual}.", filter.Span); if (!(filter is OasisScriptLiteralExpressionSyntax) && !(filter is OasisScriptReferenceLiteralSyntax)) Report(DiagnosticCodes.InvalidEventPattern, "Event filters must be literal values.", filter.Span); parameters.Add(new OasisScriptEventParameter(expected, null, filter)); }
            }
            AnalyzeBlock(syntax.Body, false); PopScope(); return new OasisScriptEventHandler(sourceOrder, syntax.EventName, parameters, syntax.Body);
        }

        private void AnalyzeBlock(OasisScriptBlockSyntax block, bool makeScope = true)
        {
            if (makeScope) PushScope();
            foreach (var statement in block.Statements)
            {
                if (statement is OasisScriptLetStatementSyntax let) { var type = TypeOf(let.Initializer, false); if (type == OasisScriptType.Void) Report(DiagnosticCodes.TypeMismatch, "A Void built-in cannot be used as a value.", let.Initializer.Span); DeclareLocal(let.Name, type, let.Span); }
                else if (statement is OasisScriptAssignmentStatementSyntax assignment) { var value = TypeOf(assignment.Value, false); var target = Lookup(assignment.Name); if (target == null) Report(DiagnosticCodes.UnknownIdentifier, $"Unknown identifier '{assignment.Name}'.", assignment.Span); else if (!target.IsState) Report(DiagnosticCodes.InvalidAssignmentTarget, $"Only state variables can be assigned; '{assignment.Name}' is immutable.", assignment.Span); else if (!target.Type.Equals(value) && value != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, $"Cannot assign {value} to state {target.Type}.", assignment.Value.Span); }
                else if (statement is OasisScriptIfStatementSyntax conditional) { Require(TypeOf(conditional.Condition, false), OasisScriptType.Bool, conditional.Condition.Span, "if condition"); AnalyzeBlock(conditional.ThenBlock); if (conditional.ElseBlock != null) AnalyzeBlock(conditional.ElseBlock); }
                else if (statement is OasisScriptForStatementSyntax loop) { var iterable = TypeOf(loop.Iterable, false); var element = iterable.Kind == OasisScriptTypeKind.List ? iterable.ElementType! : iterable.Kind == OasisScriptTypeKind.Range ? OasisScriptType.Number : OasisScriptType.Invalid; if (element == OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidIterable, "for requires a List<T> or range value.", loop.Iterable.Span); PushScope(); DeclareLocal(loop.Variable, element, loop.Span); AnalyzeBlock(loop.Body, false); PopScope(); }
                else if (statement is OasisScriptExpressionStatementSyntax expressionStatement) { var type = TypeOf(expressionStatement.Expression, false); if (type != OasisScriptType.Void && type != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, "Only Void-returning built-ins may be used as expression statements.", expressionStatement.Span); }
                else if (statement is OasisScriptBlockSyntax nested) AnalyzeBlock(nested);
            }
            if (makeScope) PopScope();
        }

        private OasisScriptType TypeOf(OasisScriptExpressionSyntax expression, bool globalInitializer)
        {
            if (expression is OasisScriptLiteralExpressionSyntax literal) return literal.Type;
            if (expression is OasisScriptReferenceLiteralSyntax reference) return reference.Type;
            if (expression is OasisScriptIdentifierExpressionSyntax identifier)
            {
                var symbol = Lookup(identifier.Name);
                if (symbol == null) { Report(DiagnosticCodes.UnknownIdentifier, $"Unknown identifier '{identifier.Name}'.", identifier.Span); return OasisScriptType.Invalid; }
                if (globalInitializer && symbol.Kind == SymbolKind.State)
                {
                    Report(DiagnosticCodes.InvalidGlobalInitializerReference, $"Top-level initializers cannot depend on mutable state '{identifier.Name}'; only previously declared const values are allowed.", identifier.Span);
                    return OasisScriptType.Invalid;
                }
                return symbol.Type;
            }
            if (expression is OasisScriptListExpressionSyntax list) { if (list.Elements.Count == 0) { Report(DiagnosticCodes.TypeMismatch, "An empty list has no inferable element type.", list.Span); return OasisScriptType.Invalid; } var first = TypeOf(list.Elements[0], globalInitializer); for (var i = 1; i < list.Elements.Count; i++) Require(TypeOf(list.Elements[i], globalInitializer), first, list.Elements[i].Span, "list element"); return OasisScriptType.ListOf(first); }
            if (expression is OasisScriptIndexExpressionSyntax index) { var target = TypeOf(index.Target, globalInitializer); Require(TypeOf(index.Index, globalInitializer), OasisScriptType.Number, index.Index.Span, "list index"); if (TryConstantNumber(index.Index, out var indexValue) && indexValue != Math.Truncate(indexValue)) Report(DiagnosticCodes.TypeMismatch, "A list index must be integral.", index.Index.Span); if (target.Kind != OasisScriptTypeKind.List) { Report(DiagnosticCodes.TypeMismatch, "Only lists can be indexed.", index.Target.Span); return OasisScriptType.Invalid; } return target.ElementType!; }
            if (expression is OasisScriptUnaryExpressionSyntax unary) { var expected = unary.Operator == "not" ? OasisScriptType.Bool : OasisScriptType.Number; Require(TypeOf(unary.Operand, globalInitializer), expected, unary.Operand.Span, $"'{unary.Operator}' operand"); return expected; }
            if (expression is OasisScriptBinaryExpressionSyntax binary) return TypeBinary(binary, globalInitializer);
            if (expression is OasisScriptBuiltinCallExpressionSyntax call) return TypeCall(call, globalInitializer);
            return OasisScriptType.Invalid;
        }

        private OasisScriptType TypeBinary(OasisScriptBinaryExpressionSyntax binary, bool globalInitializer)
        {
            var left = TypeOf(binary.Left, globalInitializer); var right = TypeOf(binary.Right, globalInitializer);
            if (binary.Operator == "and" || binary.Operator == "or") { Require(left, OasisScriptType.Bool, binary.Left.Span, "boolean operand"); Require(right, OasisScriptType.Bool, binary.Right.Span, "boolean operand"); return OasisScriptType.Bool; }
            if (binary.Operator == "==" || binary.Operator == "!=") { if (!left.Equals(right) && left != OasisScriptType.Invalid && right != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, $"Equality operands must have the same type, not {left} and {right}.", binary.Span); return OasisScriptType.Bool; }
            Require(left, OasisScriptType.Number, binary.Left.Span, "numeric operand"); Require(right, OasisScriptType.Number, binary.Right.Span, "numeric operand");
            return binary.Operator == "<" || binary.Operator == "<=" || binary.Operator == ">" || binary.Operator == ">=" ? OasisScriptType.Bool : OasisScriptType.Number;
        }

        private OasisScriptType TypeCall(OasisScriptBuiltinCallExpressionSyntax call, bool globalInitializer)
        {
            if (!_builtins.TryGet(call.Name, out var signature)) { Report(DiagnosticCodes.UnknownBuiltin, $"Unknown built-in '{call.Name}'.", call.Span); foreach (var arg in call.Arguments) TypeOf(arg, globalInitializer); return OasisScriptType.Invalid; }
            if (signature.Parameters.Count != call.Arguments.Count) Report(DiagnosticCodes.InvalidArgumentCount, $"'{call.Name}' expects {signature.Parameters.Count} argument(s), not {call.Arguments.Count}.", call.Span);
            for (var i = 0; i < call.Arguments.Count; i++) { var actual = TypeOf(call.Arguments[i], globalInitializer); if (i < signature.Parameters.Count && !actual.Equals(signature.Parameters[i]) && actual != OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidArgumentType, $"Argument {i + 1} of '{call.Name}' must be {signature.Parameters[i]}, not {actual}.", call.Arguments[i].Span); }
            if (call.Name == "range" && call.Arguments.Count == 2)
            {
                if (!TryConstantNumber(call.Arguments[0], out var start) || !TryConstantNumber(call.Arguments[1], out var end) || double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(end) || double.IsInfinity(end) || start != Math.Truncate(start) || end != Math.Truncate(end)) Report(DiagnosticCodes.InvalidRange, "range bounds must be finite integral compile-time numbers.", call.Span);
                else if (start > end) Report(DiagnosticCodes.InvalidRange, "range start must be less than or equal to end.", call.Span);
            }
            return signature.ReturnType;
        }
        private static bool TryConstantNumber(OasisScriptExpressionSyntax expression, out double value) { if (expression is OasisScriptLiteralExpressionSyntax literal && literal.Type == OasisScriptType.Number) { value = (double)literal.Value; return true; } if (expression is OasisScriptUnaryExpressionSyntax unary && unary.Operator == "-" && TryConstantNumber(unary.Operand, out value)) { value = -value; return true; } value = 0; return false; }
        private void Require(OasisScriptType actual, OasisScriptType expected, OasisScriptTextSpan span, string context) { if (actual != OasisScriptType.Invalid && !actual.Equals(expected)) Report(DiagnosticCodes.TypeMismatch, $"{context} must be {expected}, not {actual}.", span); }
        private Symbol? Lookup(string name) { foreach (var scope in _scopes) if (scope.TryGetValue(name, out var local)) return local; return _globals.TryGetValue(name, out var global) ? global : null; }
        private void DeclareLocal(string name, OasisScriptType type, OasisScriptTextSpan span) { if (Lookup(name) != null) Report(DiagnosticCodes.DuplicateDeclaration, $"'{name}' shadows an active declaration; shadowing is not allowed.", span); else _scopes.Peek().Add(name, new Symbol(name, type, SymbolKind.Local)); }
        private void PushScope() => _scopes.Push(new Dictionary<string, Symbol>(StringComparer.Ordinal)); private void PopScope() => _scopes.Pop();
    }
}
