using System;
using System.Collections.Generic;
using System.Linq;

namespace Oasis.Scripting
{
    public enum OasisScriptTypeKind { Invalid, Void, Number, Bool, String, Vec3, ObjectRef, AnchorRef, TriggerRef, InputRef, LampRef, ReelRef, AlphaDisplayRef, SevenSegmentRef, List, Range }

    public sealed class OasisScriptType : IEquatable<OasisScriptType>
    {
        private OasisScriptType(OasisScriptTypeKind kind, OasisScriptType? elementType = null) { Kind = kind; ElementType = elementType; }
        public OasisScriptTypeKind Kind { get; }
        public OasisScriptType? ElementType { get; }
        public static readonly OasisScriptType Invalid = new OasisScriptType(OasisScriptTypeKind.Invalid);
        public static readonly OasisScriptType Void = new OasisScriptType(OasisScriptTypeKind.Void);
        public static readonly OasisScriptType Number = new OasisScriptType(OasisScriptTypeKind.Number);
        public static readonly OasisScriptType Bool = new OasisScriptType(OasisScriptTypeKind.Bool);
        public static readonly OasisScriptType String = new OasisScriptType(OasisScriptTypeKind.String);
        public static readonly OasisScriptType Vec3 = new OasisScriptType(OasisScriptTypeKind.Vec3);
        public static readonly OasisScriptType ObjectRef = new OasisScriptType(OasisScriptTypeKind.ObjectRef);
        public static readonly OasisScriptType AnchorRef = new OasisScriptType(OasisScriptTypeKind.AnchorRef);
        public static readonly OasisScriptType TriggerRef = new OasisScriptType(OasisScriptTypeKind.TriggerRef);
        public static readonly OasisScriptType InputRef = new OasisScriptType(OasisScriptTypeKind.InputRef);
        public static readonly OasisScriptType LampRef = new OasisScriptType(OasisScriptTypeKind.LampRef);
        public static readonly OasisScriptType ReelRef = new OasisScriptType(OasisScriptTypeKind.ReelRef);
        public static readonly OasisScriptType AlphaDisplayRef = new OasisScriptType(OasisScriptTypeKind.AlphaDisplayRef);
        public static readonly OasisScriptType SevenSegmentRef = new OasisScriptType(OasisScriptTypeKind.SevenSegmentRef);
        public static readonly OasisScriptType Range = new OasisScriptType(OasisScriptTypeKind.Range, Number);
        public static OasisScriptType ListOf(OasisScriptType element) => new OasisScriptType(OasisScriptTypeKind.List, element);
        public bool Equals(OasisScriptType? other) => other != null && Kind == other.Kind && Equals(ElementType, other.ElementType);
        public override bool Equals(object? obj) => Equals(obj as OasisScriptType);
        public override int GetHashCode() => ((int)Kind * 397) ^ (ElementType?.GetHashCode() ?? 0);
        public override string ToString() => Kind == OasisScriptTypeKind.List ? $"List<{ElementType}>" : Kind.ToString();
    }

    public abstract class OasisScriptSyntaxNode { protected OasisScriptSyntaxNode(OasisScriptTextSpan span) { Span = span; } public OasisScriptTextSpan Span { get; } }
    public sealed class OasisScriptProgramSyntax : OasisScriptSyntaxNode { public OasisScriptProgramSyntax(IReadOnlyList<OasisScriptDeclarationSyntax> declarations, OasisScriptTextSpan span) : base(span) { Declarations = Array.AsReadOnly(declarations.ToArray()); } public IReadOnlyList<OasisScriptDeclarationSyntax> Declarations { get; } }
    public abstract class OasisScriptDeclarationSyntax : OasisScriptSyntaxNode { protected OasisScriptDeclarationSyntax(OasisScriptTextSpan span) : base(span) { } }
    public abstract class OasisScriptVariableDeclarationSyntax : OasisScriptDeclarationSyntax { protected OasisScriptVariableDeclarationSyntax(bool isState, string name, OasisScriptExpressionSyntax initializer, OasisScriptTextSpan span) : base(span) { IsState = isState; Name = name; Initializer = initializer; } public bool IsState { get; } public string Name { get; } public OasisScriptExpressionSyntax Initializer { get; } }
    public sealed class OasisScriptConstDeclarationSyntax : OasisScriptVariableDeclarationSyntax { public OasisScriptConstDeclarationSyntax(string name, OasisScriptExpressionSyntax initializer, OasisScriptTextSpan span) : base(false, name, initializer, span) { } }
    public sealed class OasisScriptStateDeclarationSyntax : OasisScriptVariableDeclarationSyntax { public OasisScriptStateDeclarationSyntax(string name, OasisScriptExpressionSyntax initializer, OasisScriptTextSpan span) : base(true, name, initializer, span) { } }
    public sealed class OasisScriptEventHandlerSyntax : OasisScriptDeclarationSyntax { public OasisScriptEventHandlerSyntax(string eventName, IReadOnlyList<OasisScriptEventParameterSyntax> parameters, OasisScriptBlockSyntax body, OasisScriptTextSpan span) : base(span) { EventName = eventName; Parameters = Array.AsReadOnly(parameters.ToArray()); Body = body; } public string EventName { get; } public IReadOnlyList<OasisScriptEventParameterSyntax> Parameters { get; } public OasisScriptBlockSyntax Body { get; } }
    public abstract class OasisScriptEventParameterSyntax : OasisScriptSyntaxNode { protected OasisScriptEventParameterSyntax(OasisScriptTextSpan span) : base(span) { } }
    public sealed class OasisScriptEventFilterSyntax : OasisScriptEventParameterSyntax { public OasisScriptEventFilterSyntax(OasisScriptExpressionSyntax value, OasisScriptTextSpan span) : base(span) { Value = value; } public OasisScriptExpressionSyntax Value { get; } }
    public sealed class OasisScriptEventBindingSyntax : OasisScriptEventParameterSyntax { public OasisScriptEventBindingSyntax(string name, OasisScriptTextSpan span) : base(span) { Name = name; } public string Name { get; } }
    public abstract class OasisScriptStatementSyntax : OasisScriptSyntaxNode { protected OasisScriptStatementSyntax(OasisScriptTextSpan span) : base(span) { } }
    public sealed class OasisScriptBlockSyntax : OasisScriptStatementSyntax { public OasisScriptBlockSyntax(IReadOnlyList<OasisScriptStatementSyntax> statements, OasisScriptTextSpan span) : base(span) { Statements = Array.AsReadOnly(statements.ToArray()); } public IReadOnlyList<OasisScriptStatementSyntax> Statements { get; } }
    public sealed class OasisScriptLetStatementSyntax : OasisScriptStatementSyntax { public OasisScriptLetStatementSyntax(string name, OasisScriptExpressionSyntax initializer, OasisScriptTextSpan span) : base(span) { Name = name; Initializer = initializer; } public string Name { get; } public OasisScriptExpressionSyntax Initializer { get; } }
    public sealed class OasisScriptAssignmentStatementSyntax : OasisScriptStatementSyntax { public OasisScriptAssignmentStatementSyntax(string name, OasisScriptExpressionSyntax value, OasisScriptTextSpan span) : base(span) { Name = name; Value = value; } public string Name { get; } public OasisScriptExpressionSyntax Value { get; } }
    public sealed class OasisScriptIfStatementSyntax : OasisScriptStatementSyntax { public OasisScriptIfStatementSyntax(OasisScriptExpressionSyntax condition, OasisScriptBlockSyntax thenBlock, OasisScriptBlockSyntax? elseBlock, OasisScriptTextSpan span) : base(span) { Condition = condition; ThenBlock = thenBlock; ElseBlock = elseBlock; } public OasisScriptExpressionSyntax Condition { get; } public OasisScriptBlockSyntax ThenBlock { get; } public OasisScriptBlockSyntax? ElseBlock { get; } }
    public sealed class OasisScriptForStatementSyntax : OasisScriptStatementSyntax { public OasisScriptForStatementSyntax(string variable, OasisScriptExpressionSyntax iterable, OasisScriptBlockSyntax body, OasisScriptTextSpan span) : base(span) { Variable = variable; Iterable = iterable; Body = body; } public string Variable { get; } public OasisScriptExpressionSyntax Iterable { get; } public OasisScriptBlockSyntax Body { get; } }
    public sealed class OasisScriptExpressionStatementSyntax : OasisScriptStatementSyntax { public OasisScriptExpressionStatementSyntax(OasisScriptExpressionSyntax expression, OasisScriptTextSpan span) : base(span) { Expression = expression; } public OasisScriptExpressionSyntax Expression { get; } }
    public abstract class OasisScriptExpressionSyntax : OasisScriptSyntaxNode { protected OasisScriptExpressionSyntax(OasisScriptTextSpan span) : base(span) { } }
    public sealed class OasisScriptLiteralExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptLiteralExpressionSyntax(object value, OasisScriptType type, OasisScriptTextSpan span) : base(span) { Value = value; Type = type; } public object Value { get; } public OasisScriptType Type { get; } }
    public sealed class OasisScriptReferenceLiteralSyntax : OasisScriptExpressionSyntax { public OasisScriptReferenceLiteralSyntax(string prefix, string id, OasisScriptType type, OasisScriptTextSpan span) : base(span) { Prefix = prefix; Id = id; Type = type; } public string Prefix { get; } public string Id { get; } public OasisScriptType Type { get; } public override string ToString() => Prefix + ":" + Id; }
    public sealed class OasisScriptIdentifierExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptIdentifierExpressionSyntax(string name, OasisScriptTextSpan span) : base(span) { Name = name; } public string Name { get; } }
    public sealed class OasisScriptListExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptListExpressionSyntax(IReadOnlyList<OasisScriptExpressionSyntax> elements, OasisScriptTextSpan span) : base(span) { Elements = Array.AsReadOnly(elements.ToArray()); } public IReadOnlyList<OasisScriptExpressionSyntax> Elements { get; } }
    public sealed class OasisScriptIndexExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptIndexExpressionSyntax(OasisScriptExpressionSyntax target, OasisScriptExpressionSyntax index, OasisScriptTextSpan span) : base(span) { Target = target; Index = index; } public OasisScriptExpressionSyntax Target { get; } public OasisScriptExpressionSyntax Index { get; } }
    public sealed class OasisScriptUnaryExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptUnaryExpressionSyntax(string op, OasisScriptExpressionSyntax operand, OasisScriptTextSpan span) : base(span) { Operator = op; Operand = operand; } public string Operator { get; } public OasisScriptExpressionSyntax Operand { get; } }
    public sealed class OasisScriptBinaryExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptBinaryExpressionSyntax(OasisScriptExpressionSyntax left, string op, OasisScriptExpressionSyntax right, OasisScriptTextSpan span) : base(span) { Left = left; Operator = op; Right = right; } public OasisScriptExpressionSyntax Left { get; } public string Operator { get; } public OasisScriptExpressionSyntax Right { get; } }
    public sealed class OasisScriptBuiltinCallExpressionSyntax : OasisScriptExpressionSyntax { public OasisScriptBuiltinCallExpressionSyntax(string name, IReadOnlyList<OasisScriptExpressionSyntax> arguments, OasisScriptTextSpan span) : base(span) { Name = name; Arguments = Array.AsReadOnly(arguments.ToArray()); } public string Name { get; } public IReadOnlyList<OasisScriptExpressionSyntax> Arguments { get; } }
}
