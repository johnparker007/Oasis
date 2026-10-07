using System;

namespace Oasis.Scripting
{
    public readonly struct OasisScriptTextSpan
    {
        public OasisScriptTextSpan(int start, int length, int line, int column)
        { Start = start; Length = length; Line = line; Column = column; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
    }

    public enum OasisScriptDiagnosticSeverity { Error, Warning }

    public sealed class OasisScriptDiagnostic
    {
        public OasisScriptDiagnostic(string code, OasisScriptDiagnosticSeverity severity, string message, string sourceName, OasisScriptTextSpan span)
        { Code = code; Severity = severity; Message = message; SourceName = sourceName; Span = span; }
        public string Code { get; }
        public OasisScriptDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string SourceName { get; }
        public OasisScriptTextSpan Span { get; }
        public int Line => Span.Line;
        public int Column => Span.Column;
        public override string ToString() => $"{SourceName}:{Line}:{Column}: {Code} {Message}";
    }

    internal static class DiagnosticCodes
    {
        public const string UnexpectedCharacter = "OS1001";
        public const string UnexpectedToken = "OS1002";
        public const string InvalidLiteral = "OS1003";
        public const string UnknownIdentifier = "OS2001";
        public const string DuplicateDeclaration = "OS2002";
        public const string TypeMismatch = "OS2101";
        public const string InvalidAssignmentTarget = "OS2102";
        public const string InvalidIterable = "OS2103";
        public const string InvalidGlobalInitializerReference = "OS2104";
        public const string InvalidEventPattern = "OS2201";
        public const string UnknownBuiltin = "OS2301";
        public const string InvalidArgumentCount = "OS2302";
        public const string InvalidArgumentType = "OS2303";
        public const string InvalidReferenceLiteral = "OS2401";
        public const string InvalidRange = "OS2501";
    }
}
