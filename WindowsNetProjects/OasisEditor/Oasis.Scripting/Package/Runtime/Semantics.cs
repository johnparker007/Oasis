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
            new OasisScriptBuiltinSignature("oasis.instantiate", OasisScriptType.OasisObjectRef, OasisScriptType.PrefabRef, OasisScriptType.Vec3, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("oasis.destroy", OasisScriptType.Void, OasisScriptType.OasisObjectRef),
            new OasisScriptBuiltinSignature("oasis.is_alive", OasisScriptType.Bool, OasisScriptType.OasisObjectRef),
            new OasisScriptBuiltinSignature("oasis.set_active", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.Bool),
            new OasisScriptBuiltinSignature("oasis.teleport", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.AnchorRef),
            new OasisScriptBuiltinSignature("oasis.teleport_pose", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.Vec3, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("oasis.set_velocity", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("oasis.set_angular_velocity", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("oasis.apply_impulse", OasisScriptType.Void, OasisScriptType.OasisObjectRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("oasis.reset", OasisScriptType.Void, OasisScriptType.OasisObjectRef),
            new OasisScriptBuiltinSignature("frame.position", OasisScriptType.Vec3, OasisScriptType.AnchorRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("frame.rotation", OasisScriptType.Vec3, OasisScriptType.AnchorRef, OasisScriptType.Vec3),
            new OasisScriptBuiltinSignature("collider.sphere_radius", OasisScriptType.Number, OasisScriptType.PrefabRef),
            new OasisScriptBuiltinSignature("surface.contains_sphere", OasisScriptType.Bool, OasisScriptType.SurfaceRef, OasisScriptType.Vec3, OasisScriptType.Number),
            new OasisScriptBuiltinSignature("math.sqrt", OasisScriptType.Number, OasisScriptType.Number),
            new OasisScriptBuiltinSignature("require", OasisScriptType.Void, OasisScriptType.Bool, OasisScriptType.String),
            new OasisScriptBuiltinSignature("list.append", OasisScriptType.Invalid, OasisScriptType.Invalid, OasisScriptType.Invalid),
            new OasisScriptBuiltinSignature("list.count", OasisScriptType.Number, OasisScriptType.Invalid),
            new OasisScriptBuiltinSignature("list.contains", OasisScriptType.Bool, OasisScriptType.Invalid, OasisScriptType.Invalid),
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
        internal OasisScriptProgram(OasisScriptProgramSyntax syntax, IReadOnlyList<OasisScriptVariable> constants, IReadOnlyList<OasisScriptVariable> states, IReadOnlyList<OasisScriptEventHandler> handlers, IReadOnlyList<OasisScriptFunctionSyntax> functions, string sourceName) { SourceName = sourceName; Syntax = syntax; Constants = Array.AsReadOnly(constants.ToArray()); States = Array.AsReadOnly(states.ToArray()); EventHandlers = Array.AsReadOnly(handlers.ToArray()); Functions = Array.AsReadOnly(functions.ToArray()); }
        public string SourceName { get; }
        public OasisScriptProgramSyntax Syntax { get; } public IReadOnlyList<OasisScriptVariable> Constants { get; } public IReadOnlyList<OasisScriptVariable> States { get; } public IReadOnlyList<OasisScriptEventHandler> EventHandlers { get; } public IReadOnlyList<OasisScriptFunctionSyntax> Functions { get; }
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
            ["trigger.entered"] = new[] { OasisScriptType.TriggerRef, OasisScriptType.OasisObjectRef }, ["trigger.exited"] = new[] { OasisScriptType.TriggerRef, OasisScriptType.OasisObjectRef },
            ["collision.entered"] = new[] { OasisScriptType.OasisObjectRef, OasisScriptType.OasisObjectRef }, ["collision.exited"] = new[] { OasisScriptType.OasisObjectRef, OasisScriptType.OasisObjectRef }, ["timer.elapsed"] = new[] { OasisScriptType.String }
        };
        private readonly string _sourceName; private readonly OasisScriptBuiltinRegistry _builtins; private readonly List<OasisScriptDiagnostic> _diagnostics = new List<OasisScriptDiagnostic>(); private readonly Dictionary<string, Symbol> _globals = new Dictionary<string, Symbol>(StringComparer.Ordinal); private readonly Dictionary<string, OasisScriptFunctionSyntax> _functions = new Dictionary<string, OasisScriptFunctionSyntax>(StringComparer.Ordinal); private readonly Stack<Dictionary<string, Symbol>> _scopes = new Stack<Dictionary<string, Symbol>>(); private OasisScriptType? _currentReturnType;
        public OasisScriptSemanticAnalyzer(string sourceName, OasisScriptBuiltinRegistry builtins) { _sourceName = sourceName; _builtins = builtins; }
        public IReadOnlyList<OasisScriptDiagnostic> Diagnostics => _diagnostics;
        private void Report(string code, string message, OasisScriptTextSpan span) => _diagnostics.Add(new OasisScriptDiagnostic(code, OasisScriptDiagnosticSeverity.Error, message, _sourceName, span));

        public OasisScriptProgram Analyze(OasisScriptProgramSyntax syntax)
        {
            var constants = new List<OasisScriptVariable>(); var states = new List<OasisScriptVariable>(); var handlers = new List<OasisScriptEventHandler>();
            foreach (var function in syntax.Functions)
            {
                if (_functions.ContainsKey(function.Name) || _builtins.TryGet(function.Name, out _)) Report(DiagnosticCodes.DuplicateDeclaration, $"'{function.Name}' is already declared.", function.Span);
                else _functions.Add(function.Name, function);
            }
            foreach (var declaration in syntax.Declarations.OfType<OasisScriptVariableDeclarationSyntax>())
            {
                var variable = declaration;
                var inferred = TypeOf(variable.Initializer, true, variable.DeclaredType);
                var type = variable.DeclaredType ?? inferred;
                if (type == OasisScriptType.Void) Report(DiagnosticCodes.TypeMismatch, "A Void built-in cannot be used as a value.", variable.Initializer.Span);
                if (variable.DeclaredType != null && inferred != OasisScriptType.Invalid && !inferred.Equals(type)) Report(DiagnosticCodes.TypeMismatch, $"Initializer must be {type}, not {inferred}.", variable.Initializer.Span);
                if (_globals.ContainsKey(variable.Name) || _functions.ContainsKey(variable.Name)) Report(DiagnosticCodes.DuplicateDeclaration, $"'{variable.Name}' is already declared.", variable.Span);
                else { _globals.Add(variable.Name, new Symbol(variable.Name, type, variable.IsState ? SymbolKind.State : SymbolKind.Const)); var model = new OasisScriptVariable(variable.Name, type, variable.Initializer); if (variable.IsState) states.Add(model); else constants.Add(model); }
            }
            foreach (var declaration in syntax.Declarations)
            {
                if (declaration is OasisScriptEventHandlerSyntax handler) handlers.Add(AnalyzeHandler(handler, handlers.Count));
                else if (declaration is OasisScriptFunctionSyntax function) AnalyzeFunction(function);
            }
            ValidateFunctionCallGraph(syntax.Functions);
            return new OasisScriptProgram(syntax, constants, states, handlers, syntax.Functions, _sourceName);
        }

        private void AnalyzeFunction(OasisScriptFunctionSyntax function)
        {
            PushScope(); _currentReturnType = function.ReturnType;
            foreach (var parameter in function.Parameters) DeclareLocal(parameter.Name, parameter.Type, parameter.Span);
            AnalyzeBlock(function.Body, false);
            if (!AlwaysReturns(function.Body) && function.ReturnType != OasisScriptType.Void) Report(DiagnosticCodes.TypeMismatch, $"Function '{function.Name}' must return {function.ReturnType} on every path.", function.Body.Span);
            _currentReturnType = null; PopScope();
        }

        private void ValidateFunctionCallGraph(IReadOnlyList<OasisScriptFunctionSyntax> functions)
        {
            var graph = functions.ToDictionary(f => f.Name, f => CallsIn(f.Body).Where(_functions.ContainsKey).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string name, int depth)
            {
                if (depth > 16) { var item = _functions[name]; Report(DiagnosticCodes.TypeMismatch, "Function call depth is limited to 16.", item.Span); return false; }
                if (visiting.Contains(name)) { Report(DiagnosticCodes.TypeMismatch, $"Recursive function call involving '{name}' is not allowed.", _functions[name].Span); return false; }
                if (visited.Contains(name)) return true;
                visiting.Add(name); var valid = true; foreach (var target in graph[name]) valid &= Visit(target, depth + 1); visiting.Remove(name); visited.Add(name); return valid;
            }
            foreach (var function in functions) Visit(function.Name, 1);
        }

        private static IEnumerable<string> CallsIn(OasisScriptStatementSyntax statement)
        {
            if (statement is OasisScriptBlockSyntax block) { foreach (var child in block.Statements) foreach (var call in CallsIn(child)) yield return call; }
            else if (statement is OasisScriptLetStatementSyntax let) { foreach (var call in CallsIn(let.Initializer)) yield return call; }
            else if (statement is OasisScriptAssignmentStatementSyntax assignment) { foreach (var call in CallsIn(assignment.Value)) yield return call; }
            else if (statement is OasisScriptExpressionStatementSyntax expression) { foreach (var call in CallsIn(expression.Expression)) yield return call; }
            else if (statement is OasisScriptReturnStatementSyntax ret && ret.Expression != null) { foreach (var call in CallsIn(ret.Expression)) yield return call; }
            else if (statement is OasisScriptIfStatementSyntax conditional) { foreach (var call in CallsIn(conditional.Condition)) yield return call; foreach (var call in CallsIn(conditional.ThenBlock)) yield return call; if (conditional.ElseBlock != null) foreach (var call in CallsIn(conditional.ElseBlock)) yield return call; }
            else if (statement is OasisScriptForStatementSyntax loop) { foreach (var call in CallsIn(loop.Iterable)) yield return call; foreach (var call in CallsIn(loop.Body)) yield return call; }
        }
        private static IEnumerable<string> CallsIn(OasisScriptExpressionSyntax expression)
        {
            if (expression is OasisScriptBuiltinCallExpressionSyntax builtinCall) { yield return builtinCall.Name; foreach (var arg in builtinCall.Arguments) foreach (var child in CallsIn(arg)) yield return child; }
            else if (expression is OasisScriptBinaryExpressionSyntax binary) { foreach (var nestedCall in CallsIn(binary.Left)) yield return nestedCall; foreach (var nestedCall in CallsIn(binary.Right)) yield return nestedCall; }
            else if (expression is OasisScriptUnaryExpressionSyntax unary) { foreach (var nestedCall in CallsIn(unary.Operand)) yield return nestedCall; }
            else if (expression is OasisScriptListExpressionSyntax list) { foreach (var item in list.Elements) foreach (var nestedCall in CallsIn(item)) yield return nestedCall; }
            else if (expression is OasisScriptIndexExpressionSyntax index) { foreach (var nestedCall in CallsIn(index.Target)) yield return nestedCall; foreach (var nestedCall in CallsIn(index.Index)) yield return nestedCall; }
        }
        private static bool AlwaysReturns(OasisScriptBlockSyntax block) => block.Statements.Count > 0 && (block.Statements[block.Statements.Count - 1] is OasisScriptReturnStatementSyntax || block.Statements[block.Statements.Count - 1] is OasisScriptIfStatementSyntax conditional && conditional.ElseBlock != null && AlwaysReturns(conditional.ThenBlock) && AlwaysReturns(conditional.ElseBlock));

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
                else if (statement is OasisScriptAssignmentStatementSyntax assignment) { var target = Lookup(assignment.Name); var value = TypeOf(assignment.Value, false, target?.Type); if (target == null) Report(DiagnosticCodes.UnknownIdentifier, $"Unknown identifier '{assignment.Name}'.", assignment.Span); else if (!target.IsState) Report(DiagnosticCodes.InvalidAssignmentTarget, $"Only state variables can be assigned; '{assignment.Name}' is immutable.", assignment.Span); else if (!target.Type.Equals(value) && value != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, $"Cannot assign {value} to state {target.Type}.", assignment.Value.Span); }
                else if (statement is OasisScriptIfStatementSyntax conditional) { Require(TypeOf(conditional.Condition, false), OasisScriptType.Bool, conditional.Condition.Span, "if condition"); AnalyzeBlock(conditional.ThenBlock); if (conditional.ElseBlock != null) AnalyzeBlock(conditional.ElseBlock); }
                else if (statement is OasisScriptForStatementSyntax loop) { var iterable = TypeOf(loop.Iterable, false); var element = iterable.Kind == OasisScriptTypeKind.List ? iterable.ElementType! : iterable.Kind == OasisScriptTypeKind.Range ? OasisScriptType.Number : OasisScriptType.Invalid; if (element == OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidIterable, "for requires a List<T> or range value.", loop.Iterable.Span); PushScope(); DeclareLocal(loop.Variable, element, loop.Span); AnalyzeBlock(loop.Body, false); PopScope(); }
                else if (statement is OasisScriptExpressionStatementSyntax expressionStatement) { var type = TypeOf(expressionStatement.Expression, false); if (type != OasisScriptType.Void && type != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, "Only Void-returning built-ins may be used as expression statements.", expressionStatement.Span); }
                else if (statement is OasisScriptReturnStatementSyntax ret)
                {
                    if (_currentReturnType == null) Report(DiagnosticCodes.TypeMismatch, "return is only valid inside a function.", ret.Span);
                    else if (ret.Expression == null) { if (_currentReturnType != OasisScriptType.Void) Report(DiagnosticCodes.TypeMismatch, $"Return value must be {_currentReturnType}.", ret.Span); }
                    else { var actual = TypeOf(ret.Expression, false, _currentReturnType); if (!_currentReturnType.Equals(actual) && actual != OasisScriptType.Invalid) Report(DiagnosticCodes.TypeMismatch, $"Return value must be {_currentReturnType}, not {actual}.", ret.Expression.Span); }
                }
                else if (statement is OasisScriptBlockSyntax nested) AnalyzeBlock(nested);
            }
            if (makeScope) PopScope();
        }

        private OasisScriptType TypeOf(OasisScriptExpressionSyntax expression, bool globalInitializer, OasisScriptType? expectedType = null)
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
            if (expression is OasisScriptListExpressionSyntax list) { if (list.Elements.Count == 0) { if (expectedType?.Kind == OasisScriptTypeKind.List) return expectedType; Report(DiagnosticCodes.TypeMismatch, "An empty list needs an expected List<T> type.", list.Span); return OasisScriptType.Invalid; } var first = TypeOf(list.Elements[0], globalInitializer, expectedType?.Kind == OasisScriptTypeKind.List ? expectedType.ElementType : null); for (var i = 1; i < list.Elements.Count; i++) Require(TypeOf(list.Elements[i], globalInitializer, first), first, list.Elements[i].Span, "list element"); return OasisScriptType.ListOf(first); }
            if (expression is OasisScriptIndexExpressionSyntax index) { var target = TypeOf(index.Target, globalInitializer); Require(TypeOf(index.Index, globalInitializer), OasisScriptType.Number, index.Index.Span, "list index"); if (TryConstantNumber(index.Index, out var indexValue) && indexValue != Math.Truncate(indexValue)) Report(DiagnosticCodes.TypeMismatch, "A list index must be integral.", index.Index.Span); if (target.Kind != OasisScriptTypeKind.List) { Report(DiagnosticCodes.TypeMismatch, "Only lists can be indexed.", index.Target.Span); return OasisScriptType.Invalid; } return target.ElementType!; }
            if (expression is OasisScriptUnaryExpressionSyntax unary) { var expected = unary.Operator == "not" ? OasisScriptType.Bool : OasisScriptType.Number; Require(TypeOf(unary.Operand, globalInitializer), expected, unary.Operand.Span, $"'{unary.Operator}' operand"); return expected; }
            if (expression is OasisScriptBinaryExpressionSyntax binary) return TypeBinary(binary, globalInitializer);
            if (expression is OasisScriptBuiltinCallExpressionSyntax call) return TypeCall(call, globalInitializer, expectedType);
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

        private OasisScriptType TypeCall(OasisScriptBuiltinCallExpressionSyntax call, bool globalInitializer, OasisScriptType? expectedType)
        {
            if (_functions.TryGetValue(call.Name, out var function))
            {
                if (globalInitializer) Report(DiagnosticCodes.InvalidGlobalInitializerReference, "Top-level initializers cannot call user functions; use literals and pure built-ins only.", call.Span);
                if (call.Arguments.Count != function.Parameters.Count) Report(DiagnosticCodes.InvalidArgumentCount, $"'{call.Name}' expects {function.Parameters.Count} argument(s), not {call.Arguments.Count}.", call.Span);
                for (var i = 0; i < call.Arguments.Count; i++) { var expected = i < function.Parameters.Count ? function.Parameters[i].Type : null; var actual = TypeOf(call.Arguments[i], globalInitializer, expected); if (expected != null && !actual.Equals(expected) && actual != OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidArgumentType, $"Argument {i + 1} of '{call.Name}' must be {expected}, not {actual}.", call.Arguments[i].Span); }
                return function.ReturnType;
            }
            if (!_builtins.TryGet(call.Name, out var signature)) { Report(DiagnosticCodes.UnknownBuiltin, $"Unknown built-in '{call.Name}'.", call.Span); foreach (var arg in call.Arguments) TypeOf(arg, globalInitializer); return OasisScriptType.Invalid; }
            if (globalInitializer && (call.Name.StartsWith("oasis.", StringComparison.Ordinal) || call.Name.StartsWith("frame.", StringComparison.Ordinal) || call.Name.StartsWith("collider.", StringComparison.Ordinal) || call.Name.StartsWith("surface.", StringComparison.Ordinal) || signature.ReturnType == OasisScriptType.Void && call.Name != "range")) Report(DiagnosticCodes.TypeMismatch, "Host commands and assertions are not allowed in top-level initializers.", call.Span);
            if (signature.Parameters.Count != call.Arguments.Count) Report(DiagnosticCodes.InvalidArgumentCount, $"'{call.Name}' expects {signature.Parameters.Count} argument(s), not {call.Arguments.Count}.", call.Span);
            var customList = call.Name.StartsWith("list.", StringComparison.Ordinal);
            for (var i = 0; !customList && i < call.Arguments.Count; i++)
            {
                OasisScriptType? expected = i < signature.Parameters.Count ? signature.Parameters[i] : null;
                var actual = TypeOf(call.Arguments[i], globalInitializer, expected);
                if (expected != null && !actual.Equals(expected) && actual != OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidArgumentType, $"Argument {i + 1} of '{call.Name}' must be {expected}, not {actual}.", call.Arguments[i].Span);
            }
            if (call.Name == "list.append" && call.Arguments.Count == 2)
            {
                var listType = TypeOf(call.Arguments[0], globalInitializer, expectedType);
                var itemType = TypeOf(call.Arguments[1], globalInitializer, listType.Kind == OasisScriptTypeKind.List ? listType.ElementType : null);
                if (listType.Kind != OasisScriptTypeKind.List) Report(DiagnosticCodes.InvalidArgumentType, "Argument 1 of 'list.append' must be a List<T>.", call.Arguments[0].Span);
                else if (!itemType.Equals(listType.ElementType) && itemType != OasisScriptType.Invalid) Report(DiagnosticCodes.InvalidArgumentType, $"list.append item must be {listType.ElementType}, not {itemType}.", call.Arguments[1].Span);
                return listType;
            }
            if ((call.Name == "list.count" || call.Name == "list.contains") && call.Arguments.Count > 0)
            {
                var listType = TypeOf(call.Arguments[0], globalInitializer, expectedType?.Kind == OasisScriptTypeKind.List ? expectedType : null);
                if (listType.Kind != OasisScriptTypeKind.List) Report(DiagnosticCodes.InvalidArgumentType, $"Argument 1 of '{call.Name}' must be a List<T>.", call.Arguments[0].Span);
                if (call.Name == "list.contains" && call.Arguments.Count > 1) Require(TypeOf(call.Arguments[1], globalInitializer, listType.Kind == OasisScriptTypeKind.List ? listType.ElementType : null), listType.Kind == OasisScriptTypeKind.List ? listType.ElementType! : OasisScriptType.Invalid, call.Arguments[1].Span, "list.contains item");
                return signature.ReturnType;
            }
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
