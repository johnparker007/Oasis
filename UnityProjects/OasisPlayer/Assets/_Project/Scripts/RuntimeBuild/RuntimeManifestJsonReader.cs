using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>
    /// Strict reader for generated runtime manifests. JsonUtility creates non-null instances for some
    /// explicitly-null nested fields and silently discards unknown fields, which is unsafe for the
    /// versioned Machine/Object capability contracts.
    /// </summary>
    public static class RuntimeManifestJsonReader
    {
        private const int MaximumJsonCharacters = 16 * 1024 * 1024;
        private const int MaximumJsonDepth = 256;

        public static MachineRuntimeManifest ReadMachine(string json)
        {
            var machine = Deserialize<MachineRuntimeManifest>(json);
            if (machine.faces == null) machine.faces = Array.Empty<MachineRuntimeFaceReference>();
            if (machine.objectInstances == null) machine.objectInstances = Array.Empty<MachineRuntimeObjectInstance>();
            if (machine.inputs == null) machine.inputs = Array.Empty<MachineInputDefinition>();
            return machine;
        }

        public static Object3DRuntimeManifest ReadObject(string json)
        {
            return Deserialize<Object3DRuntimeManifest>(json);
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("JSON content is empty.");
            if (json.Length > MaximumJsonCharacters) throw new FormatException("JSON content exceeds the supported size limit.");
            var parser = new Parser(json);
            var value = parser.Parse();
            return ConvertValue<T>(value, "$", 0);
        }

        private static T ConvertValue<T>(object value, string context, int depth) where T : class
        {
            return (T)ConvertValue(value, typeof(T), context, depth);
        }

        private static object ConvertValue(object value, Type targetType, string context, int depth)
        {
            if (depth > MaximumJsonDepth) throw new FormatException($"{context}: manifest object nesting exceeds {MaximumJsonDepth}.");
            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null) return value == null ? null : ConvertValue(value, nullableType, context, depth);
            if (value == null)
            {
                if (targetType.IsValueType) throw new FormatException($"{context}: null is not valid for {targetType.Name}.");
                return null;
            }

            if (targetType == typeof(string))
            {
                if (value is string text) return text;
                throw new FormatException($"{context}: expected a string.");
            }
            if (targetType == typeof(bool))
            {
                if (value is bool boolean) return boolean;
                throw new FormatException($"{context}: expected a Boolean.");
            }
            if (targetType == typeof(int))
            {
                if (value is double number && number >= int.MinValue && number <= int.MaxValue && Math.Floor(number) == number) return (int)number;
                throw new FormatException($"{context}: expected a 32-bit integer.");
            }
            if (targetType == typeof(float))
            {
                if (value is double number && !double.IsInfinity(number) && !double.IsNaN(number) && number <= float.MaxValue && number >= -float.MaxValue) return (float)number;
                throw new FormatException($"{context}: expected a finite number.");
            }

            if (targetType.IsArray)
            {
                if (!(value is List<object> items)) throw new FormatException($"{context}: expected an array.");
                var elementType = targetType.GetElementType();
                var result = Array.CreateInstance(elementType, items.Count);
                for (var index = 0; index < items.Count; index++)
                    result.SetValue(ConvertValue(items[index], elementType, $"{context}[{index}]", depth + 1), index);
                return result;
            }

            if (!(value is Dictionary<string, object> properties)) throw new FormatException($"{context}: expected an object.");
            if (targetType == typeof(RuntimeVector3Definition)
                && (!properties.ContainsKey("x") || !properties.ContainsKey("y") || !properties.ContainsKey("z")))
                throw new FormatException($"{context}: vector must define x, y, and z.");
            RequireFields(targetType, properties, context);
            if (targetType.IsAbstract || targetType.IsInterface || targetType.GetConstructor(Type.EmptyTypes) == null)
                throw new FormatException($"{context}: unsupported manifest type {targetType.Name}.");
            var instance = Activator.CreateInstance(targetType);
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var knownFields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                knownFields.Add(field.Name, field);
                field.SetValue(instance, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
            foreach (var property in properties)
            {
                if (!knownFields.TryGetValue(property.Key, out var field))
                    throw new FormatException($"{context}: unknown field '{property.Key}'.");
                field.SetValue(instance, ConvertValue(property.Value, field.FieldType, $"{context}.{property.Key}", depth + 1));
            }
            if (targetType == typeof(MachineInputDefinition))
            {
                var input = (MachineInputDefinition)instance;
                if (!properties.ContainsKey("name") || input.name == null) input.name = string.Empty;
                if (!properties.ContainsKey("keyboardShortcut") || input.keyboardShortcut == null) input.keyboardShortcut = string.Empty;
            }
            return instance;
        }

        private static void RequireFields(Type targetType, Dictionary<string, object> properties, string context)
        {
            string[] required = null;
            if (targetType == typeof(MachineRuntimeManifest)) required = new[] { "schema", "schemaVersion", "machineId", "displayName", "cabinetManifest", "anchors", "runtime" };
            else if (targetType == typeof(MachineRuntimeManifestDefinition)) required = new[] { "kind" };
            else if (targetType == typeof(MachineRuntimeObjectInstance)) required = new[] { "id", "displayName", "definitionId", "definitionManifest", "transform", "active" };
            else if (targetType == typeof(MachineRuntimeObjectTransform)) required = new[] { "position", "rotationEulerDegrees", "scale" };
            else if (targetType == typeof(MachineRuntimeAnchor)) required = new[] { "id", "displayName", "position", "rotationEulerDegrees" };
            else if (targetType == typeof(MachineInputDefinition)) required = new[] { "id" };
            else if (targetType == typeof(Object3DRuntimeManifest)) required = new[] { "schema", "schemaVersion", "definitionId", "displayName", "root" };
            else if (targetType == typeof(Object3DRuntimeNode)) required = new[] { "id", "name", "components", "children" };
            else if (targetType == typeof(Object3DRuntimeComponent)) required = new[] { "componentId", "kind", "contractVersion" };
            else if (targetType == typeof(Object3DRuntimeTransform)) required = new[] { "position", "rotationEulerDegrees", "scale" };
            else if (targetType == typeof(Object3DRuntimeModel)) required = new[] { "path", "scale", "upAxis" };
            else if (targetType == typeof(Object3DRuntimeCollider)) required = new[] { "kind", "sourceFrame", "isTrigger" };
            else if (targetType == typeof(Object3DRuntimeRigidbody)) required = new[] { "mode", "useGravity" };

            if (required == null) return;
            foreach (var field in required)
                if (!properties.ContainsKey(field)) throw new FormatException($"{context}: required field '{field}' is missing.");
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _position;

            public Parser(string json) { _json = json; }

            public object Parse()
            {
                SkipWhitespace();
                var value = ParseValue(0);
                SkipWhitespace();
                if (_position != _json.Length) Fail("Unexpected content after the root value");
                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaximumJsonDepth) Fail($"JSON nesting exceeds {MaximumJsonDepth}");
                SkipWhitespace();
                if (_position >= _json.Length) Fail("Unexpected end of JSON");
                switch (_json[_position])
                {
                    case '{': return ParseObject(depth + 1);
                    case '[': return ParseArray(depth + 1);
                    case '"': return ParseString();
                    case 't': ParseLiteral("true"); return true;
                    case 'f': ParseLiteral("false"); return false;
                    case 'n': ParseLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                _position++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}')) return result;
                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _json.Length || _json[_position] != '"') Fail("Object field name must be a string");
                    var name = ParseString();
                    SkipWhitespace();
                    Require(':');
                    var value = ParseValue(depth);
                    if (result.ContainsKey(name)) Fail($"Duplicate field '{name}'");
                    result.Add(name, value);
                    SkipWhitespace();
                    if (Consume('}')) return result;
                    Require(',');
                }
            }

            private List<object> ParseArray(int depth)
            {
                _position++;
                var result = new List<object>();
                SkipWhitespace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (Consume(']')) return result;
                    Require(',');
                }
            }

            private string ParseString()
            {
                Require('"');
                var result = new StringBuilder();
                while (_position < _json.Length)
                {
                    var character = _json[_position++];
                    if (character == '"') return result.ToString();
                    if (character < 0x20) Fail("Unescaped control character in string");
                    if (character != '\\') { result.Append(character); continue; }
                    if (_position >= _json.Length) Fail("Incomplete string escape");
                    var escaped = _json[_position++];
                    switch (escaped)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: Fail("Invalid string escape"); break;
                    }
                }
                Fail("Unterminated string");
                return string.Empty;
            }

            private char ParseUnicodeEscape()
            {
                if (_position + 4 > _json.Length) Fail("Incomplete Unicode escape");
                var digits = _json.Substring(_position, 4);
                if (!ushort.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) Fail("Invalid Unicode escape");
                _position += 4;
                return (char)value;
            }

            private double ParseNumber()
            {
                var start = _position;
                if (Consume('-') && _position >= _json.Length) Fail("Incomplete number");
                if (Consume('0'))
                {
                    if (_position < _json.Length && char.IsDigit(_json[_position])) Fail("Leading zero in number");
                }
                else
                {
                    if (_position >= _json.Length || _json[_position] < '1' || _json[_position] > '9') Fail("Invalid number");
                    while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                }
                if (Consume('.'))
                {
                    if (_position >= _json.Length || !char.IsDigit(_json[_position])) Fail("Invalid fraction");
                    while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                }
                if (Consume('e') || Consume('E'))
                {
                    if (!Consume('+')) Consume('-');
                    if (_position >= _json.Length || !char.IsDigit(_json[_position])) Fail("Invalid exponent");
                    while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                }
                if (!double.TryParse(_json.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || double.IsInfinity(value) || double.IsNaN(value)) Fail("Number is outside the supported finite range");
                return value;
            }

            private void ParseLiteral(string literal)
            {
                if (_position + literal.Length > _json.Length || string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
                    Fail($"Invalid literal; expected '{literal}'");
                _position += literal.Length;
            }

            private bool Consume(char expected)
            {
                if (_position >= _json.Length || _json[_position] != expected) return false;
                _position++;
                return true;
            }

            private void Require(char expected)
            {
                SkipWhitespace();
                if (!Consume(expected)) Fail($"Expected '{expected}'");
            }

            private void SkipWhitespace()
            {
                while (_position < _json.Length)
                {
                    var character = _json[_position];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n') break;
                    _position++;
                }
            }

            private void Fail(string message)
            {
                throw new FormatException($"{message} at character {_position}.");
            }
        }
    }
}
