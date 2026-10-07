using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oasis.Scripting
{
    public enum OasisScriptTokenKind
    {
        End, Identifier, Number, String, Reference,
        Const, State, Let, On, If, Else, For, In, True, False, And, Or, Not,
        LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
        Comma, Semicolon, Dot, Plus, Minus, Star, Slash, Percent,
        Equal, EqualEqual, BangEqual, Less, LessEqual, Greater, GreaterEqual
    }

    public sealed class OasisScriptToken
    {
        internal OasisScriptToken(OasisScriptTokenKind kind, string text, object? value, OasisScriptTextSpan span)
        { Kind = kind; Text = text; Value = value; Span = span; }
        public OasisScriptTokenKind Kind { get; }
        public string Text { get; }
        public object? Value { get; }
        public OasisScriptTextSpan Span { get; }
    }

    public sealed class OasisScriptLexer
    {
        private static readonly Dictionary<string, OasisScriptTokenKind> Keywords = new Dictionary<string, OasisScriptTokenKind>(StringComparer.Ordinal)
        { ["const"] = OasisScriptTokenKind.Const, ["state"] = OasisScriptTokenKind.State, ["let"] = OasisScriptTokenKind.Let, ["on"] = OasisScriptTokenKind.On, ["if"] = OasisScriptTokenKind.If, ["else"] = OasisScriptTokenKind.Else, ["for"] = OasisScriptTokenKind.For, ["in"] = OasisScriptTokenKind.In, ["true"] = OasisScriptTokenKind.True, ["false"] = OasisScriptTokenKind.False, ["and"] = OasisScriptTokenKind.And, ["or"] = OasisScriptTokenKind.Or, ["not"] = OasisScriptTokenKind.Not };
        private static readonly HashSet<string> ReferencePrefixes = new HashSet<string>(StringComparer.Ordinal) { "object", "anchor", "trigger", "input", "lamp", "reel", "alpha", "sevenSegment" };
        private readonly string _text;
        private readonly string _sourceName;
        private readonly List<OasisScriptDiagnostic> _diagnostics = new List<OasisScriptDiagnostic>();
        private int _position, _line = 1, _column = 1;

        public OasisScriptLexer(string sourceText, string sourceName) { _text = sourceText ?? string.Empty; _sourceName = string.IsNullOrWhiteSpace(sourceName) ? "<source>" : sourceName; }
        public IReadOnlyList<OasisScriptDiagnostic> Diagnostics => _diagnostics;
        private char Current => _position < _text.Length ? _text[_position] : '\0';
        private char Peek(int offset) => _position + offset < _text.Length ? _text[_position + offset] : '\0';
        private void Advance() { if (Current == '\n') { _line++; _column = 1; } else _column++; _position++; }
        private OasisScriptTextSpan Span(int start, int line, int column) => new OasisScriptTextSpan(start, _position - start, line, column);
        private OasisScriptToken Token(OasisScriptTokenKind kind, int start, int line, int column, object? value = null) => new OasisScriptToken(kind, _text.Substring(start, _position - start), value, Span(start, line, column));

        public IReadOnlyList<OasisScriptToken> Lex()
        {
            var tokens = new List<OasisScriptToken>();
            while (true)
            {
                SkipTrivia();
                var start = _position; var line = _line; var column = _column;
                if (Current == '\0') { tokens.Add(new OasisScriptToken(OasisScriptTokenKind.End, string.Empty, null, new OasisScriptTextSpan(start, 0, line, column))); break; }
                if (char.IsLetter(Current) || Current == '_') { tokens.Add(LexWord(start, line, column)); continue; }
                if (char.IsDigit(Current)) { tokens.Add(LexNumber(start, line, column)); continue; }
                if (Current == '"') { tokens.Add(LexString(start, line, column)); continue; }
                var kind = Current switch { '(' => OasisScriptTokenKind.LeftParen, ')' => OasisScriptTokenKind.RightParen, '{' => OasisScriptTokenKind.LeftBrace, '}' => OasisScriptTokenKind.RightBrace, '[' => OasisScriptTokenKind.LeftBracket, ']' => OasisScriptTokenKind.RightBracket, ',' => OasisScriptTokenKind.Comma, ';' => OasisScriptTokenKind.Semicolon, '.' => OasisScriptTokenKind.Dot, '+' => OasisScriptTokenKind.Plus, '-' => OasisScriptTokenKind.Minus, '*' => OasisScriptTokenKind.Star, '/' => OasisScriptTokenKind.Slash, '%' => OasisScriptTokenKind.Percent, _ => OasisScriptTokenKind.End };
                if (Current == '=' && Peek(1) == '=') { Advance(); Advance(); tokens.Add(Token(OasisScriptTokenKind.EqualEqual, start, line, column)); }
                else if (Current == '!' && Peek(1) == '=') { Advance(); Advance(); tokens.Add(Token(OasisScriptTokenKind.BangEqual, start, line, column)); }
                else if (Current == '<') { Advance(); if (Current == '=') { Advance(); kind = OasisScriptTokenKind.LessEqual; } else kind = OasisScriptTokenKind.Less; tokens.Add(Token(kind, start, line, column)); }
                else if (Current == '>') { Advance(); if (Current == '=') { Advance(); kind = OasisScriptTokenKind.GreaterEqual; } else kind = OasisScriptTokenKind.Greater; tokens.Add(Token(kind, start, line, column)); }
                else if (Current == '=') { Advance(); tokens.Add(Token(OasisScriptTokenKind.Equal, start, line, column)); }
                else if (kind != OasisScriptTokenKind.End) { Advance(); tokens.Add(Token(kind, start, line, column)); }
                else { Advance(); _diagnostics.Add(new OasisScriptDiagnostic(DiagnosticCodes.UnexpectedCharacter, OasisScriptDiagnosticSeverity.Error, $"Unexpected character '{_text[start]}'.", _sourceName, Span(start, line, column))); }
            }
            return tokens;
        }

        private OasisScriptToken LexWord(int start, int line, int column)
        {
            while (char.IsLetterOrDigit(Current) || Current == '_') Advance();
            var word = _text.Substring(start, _position - start);
            if (Current == ':' && ReferencePrefixes.Contains(word))
            {
                Advance(); var idStart = _position;
                while (IsAsciiLetterOrDigit(Current) || Current == '_' || Current == '-') Advance();
                var id = _text.Substring(idStart, _position - idStart);
                var numeric = word == "lamp" || word == "reel" || word == "alpha" || word == "sevenSegment";
                var valid = id.Length > 0 && (!numeric || (int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0));
                if (!valid || Current == ':')
                {
                    if (Current == ':') { while (!char.IsWhiteSpace(Current) && ";,)]}".IndexOf(Current) < 0 && Current != '\0') Advance(); }
                    _diagnostics.Add(new OasisScriptDiagnostic(DiagnosticCodes.InvalidReferenceLiteral, OasisScriptDiagnosticSeverity.Error, $"Malformed {word} reference literal.", _sourceName, Span(start, line, column)));
                }
                return Token(OasisScriptTokenKind.Reference, start, line, column, new string[] { word, id });
            }
            return Token(Keywords.TryGetValue(word, out var keyword) ? keyword : OasisScriptTokenKind.Identifier, start, line, column, word);
        }

        private OasisScriptToken LexNumber(int start, int line, int column)
        {
            while (char.IsDigit(Current)) Advance();
            if (Current == '.' && char.IsDigit(Peek(1))) { Advance(); while (char.IsDigit(Current)) Advance(); }
            var text = _text.Substring(start, _position - start);
            if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value))
                _diagnostics.Add(new OasisScriptDiagnostic(DiagnosticCodes.InvalidLiteral, OasisScriptDiagnosticSeverity.Error, "Invalid number literal.", _sourceName, Span(start, line, column)));
            return Token(OasisScriptTokenKind.Number, start, line, column, value);
        }

        private OasisScriptToken LexString(int start, int line, int column)
        {
            Advance(); var value = new System.Text.StringBuilder(); var closed = false;
            while (Current != '\0' && Current != '\n')
            {
                if (Current == '"') { Advance(); closed = true; break; }
                if (Current == '\\' && (Peek(1) == '"' || Peek(1) == '\\' || Peek(1) == 'n')) { Advance(); value.Append(Current == 'n' ? '\n' : Current); Advance(); }
                else { value.Append(Current); Advance(); }
            }
            if (!closed) _diagnostics.Add(new OasisScriptDiagnostic(DiagnosticCodes.InvalidLiteral, OasisScriptDiagnosticSeverity.Error, "Unterminated string literal.", _sourceName, Span(start, line, column)));
            return Token(OasisScriptTokenKind.String, start, line, column, value.ToString());
        }

        private void SkipTrivia()
        {
            while (true)
            {
                while (char.IsWhiteSpace(Current)) Advance();
                if (Current == '/' && Peek(1) == '/') { while (Current != '\0' && Current != '\n') Advance(); continue; }
                break;
            }
        }
        private static bool IsAsciiLetterOrDigit(char value) => (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9');
    }
}
