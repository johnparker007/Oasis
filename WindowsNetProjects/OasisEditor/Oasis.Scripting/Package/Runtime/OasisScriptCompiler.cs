#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Oasis.Scripting
{
    public sealed class OasisScriptCompilationResult
    {
        internal OasisScriptCompilationResult(OasisScriptProgram? program, IReadOnlyList<OasisScriptDiagnostic> diagnostics) { Program = program; Diagnostics = diagnostics; }
        public OasisScriptProgram? Program { get; }
        public IReadOnlyList<OasisScriptDiagnostic> Diagnostics { get; }
        public bool Success => Program != null && Diagnostics.All(x => x.Severity != OasisScriptDiagnosticSeverity.Error);
    }

    public static class OasisScriptCompiler
    {
        public static OasisScriptCompilationResult Compile(string sourceText, string sourceName = "<source>")
        {
            var lexer = new OasisScriptLexer(sourceText, sourceName); var tokens = lexer.Lex();
            var parser = new OasisScriptParser(tokens, sourceName); var syntax = parser.ParseProgram();
            var diagnostics = new List<OasisScriptDiagnostic>(); diagnostics.AddRange(lexer.Diagnostics); diagnostics.AddRange(parser.Diagnostics);
            if (diagnostics.Any(x => x.Severity == OasisScriptDiagnosticSeverity.Error)) return new OasisScriptCompilationResult(null, diagnostics);
            var analyzer = new OasisScriptSemanticAnalyzer(sourceName, OasisScriptBuiltinRegistry.CreateDefault()); var program = analyzer.Analyze(syntax); diagnostics.AddRange(analyzer.Diagnostics);
            return new OasisScriptCompilationResult(diagnostics.Any(x => x.Severity == OasisScriptDiagnosticSeverity.Error) ? null : program, diagnostics);
        }
    }
}
