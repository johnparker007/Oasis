#nullable enable

using System;
using System.Collections.Generic;

namespace Oasis.Scripting
{
    public sealed class OasisScriptParser
    {
        private readonly IReadOnlyList<OasisScriptToken> _tokens; private readonly string _sourceName; private readonly List<OasisScriptDiagnostic> _diagnostics = new List<OasisScriptDiagnostic>(); private int _position;
        public OasisScriptParser(IReadOnlyList<OasisScriptToken> tokens, string sourceName) { _tokens = tokens; _sourceName = sourceName; }
        public IReadOnlyList<OasisScriptDiagnostic> Diagnostics => _diagnostics;
        private OasisScriptToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
        private OasisScriptToken Peek(int n) => _tokens[Math.Min(_position + n, _tokens.Count - 1)];
        private OasisScriptToken Take() { var t = Current; if (t.Kind != OasisScriptTokenKind.End) _position++; return t; }
        private OasisScriptToken Expect(OasisScriptTokenKind kind) { if (Current.Kind == kind) return Take(); Error(Current, $"Expected {kind}, found {Current.Kind}."); return new OasisScriptToken(kind, string.Empty, null, Current.Span); }
        private void Error(OasisScriptToken token, string message) => _diagnostics.Add(new OasisScriptDiagnostic(DiagnosticCodes.UnexpectedToken, OasisScriptDiagnosticSeverity.Error, message, _sourceName, token.Span));
        private static OasisScriptTextSpan From(OasisScriptTextSpan start, OasisScriptTextSpan end) => new OasisScriptTextSpan(start.Start, Math.Max(0, end.Start + end.Length - start.Start), start.Line, start.Column);

        public OasisScriptProgramSyntax ParseProgram()
        {
            var declarations = new List<OasisScriptDeclarationSyntax>(); var start = Current.Span;
            while (Current.Kind != OasisScriptTokenKind.End)
            {
                var before = _position;
                if (Current.Kind == OasisScriptTokenKind.Const || Current.Kind == OasisScriptTokenKind.State) declarations.Add(ParseGlobal());
                else if (Current.Kind == OasisScriptTokenKind.On) declarations.Add(ParseHandler());
                else { Error(Current, "Expected a top-level const, state, or event handler declaration."); Take(); }
                if (_position == before) Take();
            }
            return new OasisScriptProgramSyntax(declarations, From(start, Current.Span));
        }

        private OasisScriptVariableDeclarationSyntax ParseGlobal()
        {
            var start = Take(); var name = Expect(OasisScriptTokenKind.Identifier); Expect(OasisScriptTokenKind.Equal); var value = ParseExpression(); var end = Expect(OasisScriptTokenKind.Semicolon);
            var span = From(start.Span, end.Span);
            return start.Kind == OasisScriptTokenKind.State ? (OasisScriptVariableDeclarationSyntax)new OasisScriptStateDeclarationSyntax(name.Text, value, span) : new OasisScriptConstDeclarationSyntax(name.Text, value, span);
        }

        private OasisScriptEventHandlerSyntax ParseHandler()
        {
            var start = Expect(OasisScriptTokenKind.On); var first = Expect(OasisScriptTokenKind.Identifier); Expect(OasisScriptTokenKind.Dot); var second = Expect(OasisScriptTokenKind.Identifier); var parameters = new List<OasisScriptEventParameterSyntax>();
            if (Current.Kind == OasisScriptTokenKind.LeftParen)
            {
                Take();
                if (Current.Kind != OasisScriptTokenKind.RightParen) do { var token = Current; if (token.Kind == OasisScriptTokenKind.Identifier) { Take(); parameters.Add(new OasisScriptEventBindingSyntax(token.Text, token.Span)); } else { var expression = ParseExpression(); parameters.Add(new OasisScriptEventFilterSyntax(expression, expression.Span)); } if (Current.Kind != OasisScriptTokenKind.Comma) break; Take(); } while (true);
                Expect(OasisScriptTokenKind.RightParen);
            }
            var body = ParseBlock(); return new OasisScriptEventHandlerSyntax(first.Text + "." + second.Text, parameters, body, From(start.Span, body.Span));
        }

        private OasisScriptBlockSyntax ParseBlock()
        {
            var start = Expect(OasisScriptTokenKind.LeftBrace); var statements = new List<OasisScriptStatementSyntax>();
            while (Current.Kind != OasisScriptTokenKind.RightBrace && Current.Kind != OasisScriptTokenKind.End) { var before = _position; statements.Add(ParseStatement()); if (before == _position) Take(); }
            var end = Expect(OasisScriptTokenKind.RightBrace); return new OasisScriptBlockSyntax(statements, From(start.Span, end.Span));
        }

        private OasisScriptStatementSyntax ParseStatement()
        {
            if (Current.Kind == OasisScriptTokenKind.Let) { var start = Take(); var name = Expect(OasisScriptTokenKind.Identifier); Expect(OasisScriptTokenKind.Equal); var init = ParseExpression(); var end = Expect(OasisScriptTokenKind.Semicolon); return new OasisScriptLetStatementSyntax(name.Text, init, From(start.Span, end.Span)); }
            if (Current.Kind == OasisScriptTokenKind.If) { var start = Take(); var condition = ParseExpression(); var thenBlock = ParseBlock(); OasisScriptBlockSyntax? elseBlock = null; if (Current.Kind == OasisScriptTokenKind.Else) { Take(); elseBlock = ParseBlock(); } return new OasisScriptIfStatementSyntax(condition, thenBlock, elseBlock, From(start.Span, (elseBlock ?? thenBlock).Span)); }
            if (Current.Kind == OasisScriptTokenKind.For) { var start = Take(); var name = Expect(OasisScriptTokenKind.Identifier); Expect(OasisScriptTokenKind.In); var iterable = ParseExpression(); var body = ParseBlock(); return new OasisScriptForStatementSyntax(name.Text, iterable, body, From(start.Span, body.Span)); }
            if (Current.Kind == OasisScriptTokenKind.LeftBrace) return ParseBlock();
            if (Current.Kind == OasisScriptTokenKind.Identifier && Peek(1).Kind == OasisScriptTokenKind.Equal) { var name = Take(); Take(); var value = ParseExpression(); var end = Expect(OasisScriptTokenKind.Semicolon); return new OasisScriptAssignmentStatementSyntax(name.Text, value, From(name.Span, end.Span)); }
            var expression = ParseExpression(); var finish = Expect(OasisScriptTokenKind.Semicolon); return new OasisScriptExpressionStatementSyntax(expression, From(expression.Span, finish.Span));
        }

        private OasisScriptExpressionSyntax ParseExpression(int parentPrecedence = 0)
        {
            OasisScriptExpressionSyntax left;
            var unary = UnaryPrecedence(Current.Kind);
            if (unary > 0) { var op = Take(); var operand = ParseExpression(unary); left = new OasisScriptUnaryExpressionSyntax(op.Text, operand, From(op.Span, operand.Span)); }
            else left = ParsePrimary();
            while (true)
            {
                if (Current.Kind == OasisScriptTokenKind.LeftBracket) { var start = left.Span; Take(); var index = ParseExpression(); var end = Expect(OasisScriptTokenKind.RightBracket); left = new OasisScriptIndexExpressionSyntax(left, index, From(start, end.Span)); continue; }
                var precedence = BinaryPrecedence(Current.Kind); if (precedence <= parentPrecedence) break; var op = Take(); var right = ParseExpression(precedence); left = new OasisScriptBinaryExpressionSyntax(left, op.Text, right, From(left.Span, right.Span));
            }
            return left;
        }

        private OasisScriptExpressionSyntax ParsePrimary()
        {
            var token = Current;
            if (token.Kind == OasisScriptTokenKind.Number) { Take(); return new OasisScriptLiteralExpressionSyntax(token.Value ?? 0d, OasisScriptType.Number, token.Span); }
            if (token.Kind == OasisScriptTokenKind.String) { Take(); return new OasisScriptLiteralExpressionSyntax(token.Value ?? string.Empty, OasisScriptType.String, token.Span); }
            if (token.Kind == OasisScriptTokenKind.True || token.Kind == OasisScriptTokenKind.False) { Take(); return new OasisScriptLiteralExpressionSyntax(token.Kind == OasisScriptTokenKind.True, OasisScriptType.Bool, token.Span); }
            if (token.Kind == OasisScriptTokenKind.Reference) { Take(); var parts = (string[])token.Value!; return new OasisScriptReferenceLiteralSyntax(parts[0], parts[1], ReferenceType(parts[0]), token.Span); }
            if (token.Kind == OasisScriptTokenKind.LeftParen) { Take(); var expression = ParseExpression(); Expect(OasisScriptTokenKind.RightParen); return expression; }
            if (token.Kind == OasisScriptTokenKind.LeftBracket) return ParseList();
            if (token.Kind == OasisScriptTokenKind.Identifier)
            {
                Take(); var name = token.Text;
                if (Current.Kind == OasisScriptTokenKind.Dot) { Take(); name += "." + Expect(OasisScriptTokenKind.Identifier).Text; }
                if (Current.Kind == OasisScriptTokenKind.LeftParen) return ParseCall(name, token.Span);
                return new OasisScriptIdentifierExpressionSyntax(name, token.Span);
            }
            Error(token, $"Expected expression, found {token.Kind}."); Take(); return new OasisScriptLiteralExpressionSyntax(0d, OasisScriptType.Invalid, token.Span);
        }

        private OasisScriptExpressionSyntax ParseList()
        {
            var start = Take(); var values = new List<OasisScriptExpressionSyntax>(); if (Current.Kind != OasisScriptTokenKind.RightBracket) do { values.Add(ParseExpression()); if (Current.Kind != OasisScriptTokenKind.Comma) break; Take(); } while (true); var end = Expect(OasisScriptTokenKind.RightBracket); return new OasisScriptListExpressionSyntax(values, From(start.Span, end.Span));
        }
        private OasisScriptExpressionSyntax ParseCall(string name, OasisScriptTextSpan start)
        {
            Take(); var args = new List<OasisScriptExpressionSyntax>(); if (Current.Kind != OasisScriptTokenKind.RightParen) do { args.Add(ParseExpression()); if (Current.Kind != OasisScriptTokenKind.Comma) break; Take(); } while (true); var end = Expect(OasisScriptTokenKind.RightParen); return new OasisScriptBuiltinCallExpressionSyntax(name, args, From(start, end.Span));
        }
        private static int UnaryPrecedence(OasisScriptTokenKind k) => k == OasisScriptTokenKind.Minus || k == OasisScriptTokenKind.Not ? 7 : 0;
        private static int BinaryPrecedence(OasisScriptTokenKind k) => k == OasisScriptTokenKind.Star || k == OasisScriptTokenKind.Slash || k == OasisScriptTokenKind.Percent ? 6 : k == OasisScriptTokenKind.Plus || k == OasisScriptTokenKind.Minus ? 5 : k == OasisScriptTokenKind.Less || k == OasisScriptTokenKind.LessEqual || k == OasisScriptTokenKind.Greater || k == OasisScriptTokenKind.GreaterEqual ? 4 : k == OasisScriptTokenKind.EqualEqual || k == OasisScriptTokenKind.BangEqual ? 3 : k == OasisScriptTokenKind.And ? 2 : k == OasisScriptTokenKind.Or ? 1 : 0;
        private static OasisScriptType ReferenceType(string prefix) => prefix switch { "object" => OasisScriptType.ObjectRef, "anchor" => OasisScriptType.AnchorRef, "trigger" => OasisScriptType.TriggerRef, "input" => OasisScriptType.InputRef, "lamp" => OasisScriptType.LampRef, "reel" => OasisScriptType.ReelRef, "alpha" => OasisScriptType.AlphaDisplayRef, "sevenSegment" => OasisScriptType.SevenSegmentRef, _ => OasisScriptType.Invalid };
    }
}
