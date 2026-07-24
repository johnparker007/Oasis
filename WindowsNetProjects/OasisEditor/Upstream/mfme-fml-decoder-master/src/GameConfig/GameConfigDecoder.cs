using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MfmeFmlDecoder.GameConfig.Structures;

namespace MfmeFmlDecoder.GameConfig
{
    internal sealed class GameConfigDecodeResult
    {
        public string MachineJson { get; init; }
        public string GameJson { get; init; }
        public IReadOnlyList<string> Diagnostics { get; init; }
    }

    internal static class GameConfigDecoder
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static bool TryDecode(
            string fmlPath,
            string gamPath,
            byte[] decryptedLayoutBytes,
            string mapsDirectory,
            out GameConfigDecodeResult result,
            out string skipReason)
        {
            result = null;
            skipReason = null;

            GamFile gam;
            try
            {
                gam = GamFile.Load(gamPath);
            }
            catch (Exception ex)
            {
                skipReason = "Failed to read GAM: " + ex.Message;
                return false;
            }

            if (!GameConfigMapLoader.TryLoad(gam.System, mapsDirectory, out GameConfigMap map, out string mapError))
            {
                skipReason = mapError;
                return false;
            }

            Dictionary<uint, byte[]> tags;
            try
            {
                tags = FileLevelTagBag.Parse(decryptedLayoutBytes);
            }
            catch (Exception ex)
            {
                skipReason = "Failed to parse FML tags: " + ex.Message;
                return false;
            }

            var diagnostics = new List<string>();
            var machineControls = new JsonObject();
            var gameControls = new JsonObject();
            var switches = new JsonObject();

            foreach (KeyValuePair<string, JsonElement> entry in map.Controls)
            {
                string name = entry.Key;
                JsonElement control = entry.Value;
                string storage = GetString(control, "storage") ?? "fml";
                string kind = GetString(control, "kind") ?? "unknown";

                if (string.Equals(storage, "volatile", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(storage, "unknown", StringComparison.OrdinalIgnoreCase))
                {
                    string fixedValue = GetString(control, "fixedValue");
                    if (!string.IsNullOrWhiteSpace(fixedValue))
                    {
                        // Persisted effective value (e.g. J2 always IRQ3).
                        machineControls[name] = fixedValue;
                    }
                    else
                    {
                        machineControls[name] = null;
                    }
                    continue;
                }

                try
                {
                    if (string.Equals(storage, "gam", StringComparison.OrdinalIgnoreCase))
                    {
                        JsonObject decoded = DecodeGamControl(name, kind, control, gam, diagnostics);
                        if (decoded != null)
                            gameControls[name] = decoded;
                    }
                    else
                    {
                        // Individual RJ1/RJ2 J1–J8 checkboxes are already summarized
                        // under "Reel Jumpers 1" / "Reel Jumpers 2".
                        if (IsReelJumperCheckboxControl(name))
                            continue;

                        JsonObject decoded = DecodeFmlControl(name, kind, control, tags, diagnostics);
                        if (decoded == null)
                            continue;

                        JsonNode flat = FlattenSimpleFmlValue(decoded);
                        if (string.Equals(kind, "switch-number", StringComparison.OrdinalIgnoreCase))
                            switches[name] = flat;
                        else
                            machineControls[name] = flat;
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"Failed decoding '{name}': {ex.Message}");
                }
            }

            if (switches.Count > 0)
                machineControls["switches"] = switches;

            var machineDoc = new JsonObject
            {
                ["System"] = map.System,
                ["Settings"] = machineControls
            };

            var gameDoc = new JsonObject
            {
                ["System"] = map.System,
                ["Settings"] = gameControls
            };

            result = new GameConfigDecodeResult
            {
                MachineJson = machineDoc.ToJsonString(WriteOptions),
                GameJson = gameDoc.ToJsonString(WriteOptions),
                Diagnostics = diagnostics
            };
            return true;
        }

        private static JsonObject DecodeGamControl(
            string name,
            string kind,
            JsonElement control,
            GamFile gam,
            List<string> diagnostics)
        {
            if (string.Equals(kind, "dip-banks", StringComparison.OrdinalIgnoreCase))
                return DecodeDipBanks(control, gam, diagnostics);

            // Prefer gamLine; else first usable entry in gamLines[]
            string gamLine = GetString(control, "gamLine");
            var extraGamLines = new List<string>();
            if (control.TryGetProperty("gamLines", out JsonElement gamLinesEl) &&
                gamLinesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement el in gamLinesEl.EnumerateArray())
                {
                    string s = el.GetString();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (gamLine is null) gamLine = s;
                    else extraGamLines.Add(s);
                }
            }

            if (gamLine != null && gamLine.Contains("<bitmask>", StringComparison.OrdinalIgnoreCase))
            {
                string key = GamLineKey(gamLine);
                int mask = GetInt(control, "mask") ?? 0;
                if (!gam.TryGetUInt(key, out uint raw))
                {
                    bool def = GetBool(control, "defaultChecked") ?? false;
                    return new JsonObject
                    {
                        ["kind"] = kind,
                        ["value"] = def,
                        ["raw"] = null,
                        ["defaultUsed"] = true
                    };
                }

                bool on = mask == 0 ? raw != 0 : (raw & (uint)mask) != 0;
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = on,
                    ["raw"] = raw,
                    ["mask"] = mask
                };
            }

            if (gamLine != null &&
                gamLine.Contains("1 when checked", StringComparison.OrdinalIgnoreCase))
            {
                string key = GamLineKey(gamLine);
                bool defaultChecked = GetBool(control, "defaultChecked") ?? false;
                if (!gam.Fields.TryGetValue(key, out string text))
                {
                    return new JsonObject
                    {
                        ["kind"] = kind,
                        ["value"] = defaultChecked,
                        ["raw"] = null,
                        ["defaultUsed"] = true
                    };
                }

                bool on = text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
                return new JsonObject { ["kind"] = kind, ["value"] = on, ["raw"] = text };
            }

            if (gamLine != null)
            {
                string key = GamLineKey(gamLine);
                if (!gam.Fields.TryGetValue(key, out string text))
                    return DecodeMissingGamLine(name, kind, control, gamLine, key, diagnostics);

                JsonObject primary = DecodeGamLineValue(kind, control, gamLine, text);

                // Companion lines (e.g. SetPercent alongside Percentage index)
                if (extraGamLines.Count > 0)
                {
                    var extras = new JsonObject();
                    foreach (string extra in extraGamLines)
                    {
                        string extraKey = GamLineKey(extra);
                        if (!gam.Fields.TryGetValue(extraKey, out string extraText))
                            continue;
                        if (extra.Contains("<numeric-percent>", StringComparison.OrdinalIgnoreCase) ||
                            extra.Contains("<index>", StringComparison.OrdinalIgnoreCase))
                        {
                            if (long.TryParse(extraText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
                                extras[ToCamelKey(extraKey)] = n;
                            else
                                extras[ToCamelKey(extraKey)] = extraText;
                        }
                        else
                        {
                            extras[ToCamelKey(extraKey)] = extraText;
                        }
                    }

                    if (extras.Count > 0)
                        primary["also"] = extras;
                }

                return primary;
            }

            diagnostics.Add($"Unsupported GAM control '{name}' ({kind}).");
            return null;
        }

        private static JsonObject DecodeGamLineValue(
            string kind,
            JsonElement control,
            string gamLine,
            string text)
        {
            if (gamLine.Contains("<0|1>", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "checkbox", StringComparison.OrdinalIgnoreCase))
            {
                bool on = text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
                return new JsonObject { ["kind"] = kind, ["value"] = on, ["raw"] = text };
            }

            if (gamLine.Contains("<index>", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "dropdown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "radio", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                {
                    string label = LookupValueLabel(control, idx);
                    return new JsonObject
                    {
                        ["kind"] = kind,
                        ["value"] = label ?? (JsonNode)idx,
                        ["raw"] = idx
                    };
                }
            }

            return new JsonObject { ["kind"] = kind, ["value"] = text };
        }

        private static string GamLineKey(string gamLine)
        {
            int space = gamLine.IndexOf(' ');
            return space < 0 ? gamLine.Trim() : gamLine.Substring(0, space).Trim();
        }

        /// <summary>
        /// MFME often omits GAM lines at their default UI value. Resolve those to the
        /// same default instead of emitting null.
        /// </summary>
        private static JsonObject DecodeMissingGamLine(
            string name,
            string kind,
            JsonElement control,
            string gamLine,
            string key,
            List<string> diagnostics)
        {
            // Text / rating / tags: blank in UI.
            if (string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("<text>", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("<0-5>", StringComparison.OrdinalIgnoreCase))
            {
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = "",
                    ["defaultUsed"] = true
                };
            }

            // Checkboxes (LoTech/WIP <0|1>, "1 when checked").
            if (string.Equals(kind, "checkbox", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("<0|1>", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("1 when checked", StringComparison.OrdinalIgnoreCase))
            {
                bool def = GetBool(control, "defaultChecked") ?? false;
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = def,
                    ["raw"] = null,
                    ["defaultUsed"] = true
                };
            }

            // Index dropdowns/radios (Protocol/DataPak, Stake, Prize, %).
            if (gamLine.Contains("<index>", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "dropdown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "radio", StringComparison.OrdinalIgnoreCase))
            {
                int defaultIndex = GetInt(control, "defaultIndex") ?? 0;
                string label = LookupValueLabel(control, defaultIndex);
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = label ?? (JsonNode)defaultIndex,
                    ["raw"] = defaultIndex,
                    ["defaultUsed"] = true
                };
            }

            // Numeric GAM values (e.g. DX).
            if (string.Equals(kind, "number", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("<value>", StringComparison.OrdinalIgnoreCase) ||
                gamLine.Contains("<numeric", StringComparison.OrdinalIgnoreCase))
            {
                int defaultValue = GetInt(control, "defaultValue") ?? GetInt(control, "defaultIndex") ?? 0;
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = defaultValue,
                    ["raw"] = defaultValue,
                    ["defaultUsed"] = true
                };
            }

            diagnostics.Add($"Missing GAM line '{key}' for '{name}'.");
            return new JsonObject { ["kind"] = kind, ["value"] = null, ["defaultUsed"] = true };
        }

        private static JsonObject BuildDipSwitches(string pattern, int bits)
        {
            var switches = new JsonObject();
            for (int i = 0; i < bits && i < pattern.Length; i++)
            {
                // UI switch 1 = LSB = index 0 in lsb-first pattern string
                int ui = i + 1;
                bool on = pattern[i] == '1';
                switches[ui.ToString(CultureInfo.InvariantCulture)] = on;
            }

            return switches;
        }

        private static string ToCamelKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return char.ToLowerInvariant(key[0]) + key.Substring(1);
        }

        private static JsonObject DecodeDipBanks(JsonElement control, GamFile gam, List<string> diagnostics)
        {
            var banksOut = new JsonObject();
            if (!control.TryGetProperty("banks", out JsonElement banks) || banks.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add("DIP control missing banks definition.");
                return new JsonObject { ["kind"] = "dip-banks", ["banks"] = banksOut };
            }

            string order = "lsb-first";
            if (control.TryGetProperty("stringLayout", out JsonElement layout) &&
                layout.TryGetProperty("order", out JsonElement orderEl))
            {
                order = orderEl.GetString() ?? order;
            }

            foreach (JsonProperty bank in banks.EnumerateObject())
            {
                string gamLine = bank.Value.TryGetProperty("gamLine", out JsonElement gl)
                    ? gl.GetString()
                    : bank.Name;
                int bits = bank.Value.TryGetProperty("bits", out JsonElement bitsEl)
                    ? bitsEl.GetInt32()
                    : 8;

                if (!gam.Fields.TryGetValue(gamLine ?? bank.Name, out string pattern))
                {
                    // MFME omits DIP lines when all switches are off.
                    pattern = new string('0', bits);
                    banksOut[bank.Name] = new JsonObject
                    {
                        ["pattern"] = pattern,
                        ["order"] = order,
                        ["switches"] = BuildDipSwitches(pattern, bits),
                        ["defaultUsed"] = true
                    };
                    continue;
                }

                banksOut[bank.Name] = new JsonObject
                {
                    ["pattern"] = pattern,
                    ["order"] = order,
                    ["switches"] = BuildDipSwitches(pattern, bits)
                };
            }

            return new JsonObject { ["kind"] = "dip-banks", ["banks"] = banksOut };
        }

        /// <summary>
        /// Simple FML scalars become a bare JSON value. Compound controls keep a
        /// compact object (no kind/tag/raw metadata).
        /// </summary>
        private static JsonNode FlattenSimpleFmlValue(JsonObject obj)
        {
            if (obj is null) return null;

            bool compound =
                obj.ContainsKey("slots") ||
                obj.ContainsKey("checkboxes") ||
                obj.ContainsKey("rows") ||
                obj.ContainsKey("in") ||
                obj.ContainsKey("out") ||
                obj.ContainsKey("records") ||
                obj.ContainsKey("entries") ||
                obj.ContainsKey("banks") ||
                obj.ContainsKey("Button") ||
                obj.ContainsKey("Mask");

            if (compound)
            {
                obj.Remove("kind");
                obj.Remove("tag");
                obj.Remove("raw");
                obj.Remove("byteIndex");
                obj.Remove("byteOffset");
                obj.Remove("mask");
                obj.Remove("bankModeRaw");
                return obj;
            }

            if (!obj.TryGetPropertyValue("value", out JsonNode value) || value is null)
                return null;

            // net6 JsonNode has no DeepClone; round-trip keeps ownership clean.
            return JsonNode.Parse(value.ToJsonString());
        }

        private static bool IsReelJumperCheckboxControl(string name)
        {
            // "Reel Jumpers 1 J3", "Reel Jumpers 2 J8", …
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Reel Jumpers ", StringComparison.Ordinal))
                return false;
            int j = name.LastIndexOf(" J", StringComparison.Ordinal);
            if (j < 0 || j + 3 > name.Length)
                return false;
            return name[j + 2] >= '1' && name[j + 2] <= '8' && name.Length == j + 3;
        }

        private static JsonObject DecodeFmlControl(
            string name,
            string kind,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            string structure = GetString(control, "structure");
            bool isReelJumpersRadio =
                string.Equals(structure, "reel-jumpers-v1b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Reel Jumpers 1", StringComparison.Ordinal) ||
                string.Equals(name, "Reel Jumpers 2", StringComparison.Ordinal);
            if (isReelJumpersRadio)
                return DecodeReelJumpersRadio(name, control, tags, diagnostics);

            return kind switch
            {
                "checkbox" => DecodeFmlCheckbox(name, control, tags, diagnostics),
                "radio" or "dropdown" or "number" or "slider" or "hex-number" or "switch-number"
                    => DecodeFmlScalar(name, kind, control, tags, diagnostics),
                "text" => DecodeFmlText(name, control, tags, diagnostics),
                "effect-grid" => DecodeEffectGrid(name, control, tags, diagnostics),
                "meter-grid" => DecodeMeterGrid(name, control, tags, diagnostics),
                "payout-sense" => DecodePayoutSense(name, control, tags, diagnostics),
                "record-list" => DecodeRecordList(name, control, tags, diagnostics),
                "indexed-enum-table" => DecodeIndexedEnumTable(name, control, tags, diagnostics),
                _ => DecodeFmlScalar(name, kind, control, tags, diagnostics)
            };
        }

        private static JsonObject DecodeReelJumpersRadio(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            int bank = name.IndexOf('2') >= 0 ? 2 : 1;
            tags.TryGetValue(ReelJumpersV1b.ModeTag, out byte[] modeBytes);
            uint mode = modeBytes != null && modeBytes.Length >= 4
                ? BitConverter.ToUInt32(modeBytes, 0)
                : 0u;
            string label = ReelJumpersV1b.DecodeModeLabel(mode, bank);

            var obj = new JsonObject
            {
                ["kind"] = "radio",
                ["value"] = label,
                ["raw"] = mode,
                ["bankModeRaw"] = bank == 1 ? (mode & 5u) : (mode & 10u)
            };

            if (ReelJumpersV1b.IsV1b(mode, bank))
            {
                uint tagA = bank == 1 ? ReelJumpersV1b.Rj1SideA : ReelJumpersV1b.Rj2SideA;
                uint tagB = bank == 1 ? ReelJumpersV1b.Rj1SideB : ReelJumpersV1b.Rj2SideB;
                tags.TryGetValue(tagA, out byte[] a);
                tags.TryGetValue(tagB, out byte[] b);
                string[] slots = ReelJumpersV1b.DecodeSlots(a, b);
                var arr = new JsonArray();
                foreach (string s in slots) arr.Add(s);
                obj["slots"] = arr;
            }
            else
            {
                uint checkboxTag = bank == 1 ? ReelJumpersV1b.Rj1SideB : 0x4E;
                tags.TryGetValue(checkboxTag, out byte[] box);
                Dictionary<string, bool> checks = ReelJumpersV1b.DecodeOldCheckboxes(box);
                var checksObj = new JsonObject();
                foreach (KeyValuePair<string, bool> kv in checks)
                    checksObj[kv.Key] = kv.Value;
                obj["checkboxes"] = checksObj;
            }

            return obj;
        }

        private static JsonObject DecodeFmlCheckbox(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(control);
            if (tag is null)
            {
                diagnostics.Add($"Missing FML tag for checkbox '{name}'.");
                return new JsonObject { ["kind"] = "checkbox", ["value"] = null };
            }

            if (!tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag 0x{tag.Value:X2} for '{name}'.");
                return new JsonObject { ["kind"] = "checkbox", ["value"] = false, ["raw"] = null };
            }

            int byteIndex = GetInt(control, "byteIndex") ?? 0;
            int bit = GetInt(control, "bit") ?? 0;
            int mask = GetInt(control, "mask") ?? (1 << bit);
            byte b = byteIndex < payload.Length ? payload[byteIndex] : (byte)0;
            bool bitSet = (b & mask) != 0;
            // inverted: bit set means unchecked (IMPACT Hopper 1/2 probes).
            bool inverted = GetBool(control, "inverted") ?? false;
            bool on = inverted ? !bitSet : bitSet;
            return new JsonObject
            {
                ["kind"] = "checkbox",
                ["value"] = on,
                ["tag"] = $"0x{tag.Value:X2}",
                ["byteIndex"] = byteIndex,
                ["mask"] = mask
            };
        }

        private static JsonObject DecodeFmlScalar(
            string name,
            string kind,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(control);
            if (tag is null)
            {
                diagnostics.Add($"Missing FML tag for '{name}'.");
                return new JsonObject { ["kind"] = kind, ["value"] = null };
            }

            if (!tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag 0x{tag.Value:X2} for '{name}'.");
                return new JsonObject { ["kind"] = kind, ["value"] = null, ["tag"] = $"0x{tag.Value:X2}" };
            }

            string encoding = GetString(control, "encoding") ?? "u32-le";
            int byteOffset = GetInt(control, "byteOffset") ?? 0;

            if (string.Equals(encoding, "width-height-2xu32-le", StringComparison.OrdinalIgnoreCase))
                return DecodeWidthHeight(name, kind, control, tag.Value, payload, byteOffset, diagnostics);

            // Bit-field radio (e.g. J2 on 0x46)
            if (GetInt(control, "bit") is int bit && control.TryGetProperty("values", out _))
            {
                int mask = GetInt(control, "mask") ?? (1 << bit);
                uint word = payload.Length >= 4 ? BitConverter.ToUInt32(payload, 0) : payload[0];
                long bitVal = (word & (uint)mask) != 0 ? 1 : 0;
                string label = LookupValueLabel(control, bitVal) ?? bitVal.ToString(CultureInfo.InvariantCulture);
                return new JsonObject
                {
                    ["kind"] = kind,
                    ["value"] = label,
                    ["raw"] = bitVal,
                    ["tag"] = $"0x{tag.Value:X2}"
                };
            }

            long? raw = ReadScalar(payload, encoding, control, byteOffset);
            int? emptyUi = GetInt(control, "emptyUi");
            // MFME blank switch-number is almost always 255; maps often omit emptyUi.
            if (emptyUi is null &&
                string.Equals(kind, "switch-number", StringComparison.OrdinalIgnoreCase))
            {
                emptyUi = 255;
            }

            JsonNode valueNode;
            if (raw is null)
            {
                valueNode = null;
            }
            else if (emptyUi is int empty && raw.Value == empty)
            {
                valueNode = null;
            }
            else if (string.Equals(kind, "switch-number", StringComparison.OrdinalIgnoreCase))
            {
                // Stored as u32-le but MFME displays / means signed int32
                // (e.g. 0xFFFFFFE1 → -31, not 4294967265).
                valueNode = unchecked((int)(uint)raw.Value);
            }
            else if (control.TryGetProperty("values", out _))
            {
                string label = LookupValueLabel(control, raw.Value);
                if (label is null)
                {
                    diagnostics.Add($"Unknown raw {raw} for '{name}'.");
                    valueNode = raw.Value;
                }
                else
                {
                    valueNode = label;
                }
            }
            else if (string.Equals(kind, "hex-number", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(encoding, "hex-u32-le", StringComparison.OrdinalIgnoreCase))
            {
                int width = GetInt(control, "displayWidth") ?? 8;
                valueNode = raw.Value.ToString("X" + width, CultureInfo.InvariantCulture);
            }
            else
            {
                valueNode = raw.Value;
            }

            return new JsonObject
            {
                ["kind"] = kind,
                ["value"] = valueNode,
                ["raw"] = raw,
                ["tag"] = $"0x{tag.Value:X2}"
            };
        }

        private static JsonObject DecodeWidthHeight(
            string name,
            string kind,
            JsonElement control,
            uint tag,
            byte[] payload,
            int byteOffset,
            List<string> diagnostics)
        {
            if (byteOffset + 8 > payload.Length)
            {
                diagnostics.Add($"Tag 0x{tag:X2} too short for width-height at offset {byteOffset} ('{name}').");
                return new JsonObject { ["kind"] = kind, ["value"] = null, ["tag"] = $"0x{tag:X2}" };
            }

            uint w = BitConverter.ToUInt32(payload, byteOffset);
            uint h = BitConverter.ToUInt32(payload, byteOffset + 4);
            string label = LookupWidthHeightLabel(control, w, h);
            if (label is null)
                diagnostics.Add($"Unknown size {w}x{h} for '{name}'.");

            return new JsonObject
            {
                ["kind"] = kind,
                ["value"] = label ?? (JsonNode)($"{w} x {h}"),
                ["raw"] = new JsonArray { w, h },
                ["tag"] = $"0x{tag:X2}",
                ["byteOffset"] = byteOffset
            };
        }

        private static JsonObject DecodeFmlText(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            string encoding = GetString(control, "encoding") ?? "ascii-null-padded";
            if (string.Equals(encoding, "split-ascii", StringComparison.OrdinalIgnoreCase))
                return DecodeSplitAscii(name, control, tags, diagnostics);

            uint? tag = GetPrimaryTag(control);
            if (tag is null)
            {
                diagnostics.Add($"Missing FML tag for text '{name}'.");
                return new JsonObject { ["kind"] = "text", ["value"] = null };
            }

            if (!tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag 0x{tag.Value:X2} for '{name}'.");
                return new JsonObject { ["kind"] = "text", ["value"] = null, ["tag"] = $"0x{tag.Value:X2}" };
            }

            string text = encoding switch
            {
                "ascii-4" => AsciiPrefix(payload, 4),
                "ascii-8" => AsciiPrefix(payload, 8),
                "ascii" => FileLevelTagBag.AsAsciiNullPadded(payload),
                _ => FileLevelTagBag.AsAsciiNullPadded(payload)
            };

            return new JsonObject
            {
                ["kind"] = "text",
                ["value"] = text,
                ["tag"] = $"0x{tag.Value:X2}"
            };
        }

        private static JsonObject DecodeSplitAscii(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            if (!control.TryGetProperty("parts", out JsonElement parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add($"split-ascii text '{name}' has no parts.");
                return new JsonObject { ["kind"] = "text", ["value"] = null };
            }

            var sb = new System.Text.StringBuilder();
            var usedTags = new JsonArray();
            foreach (JsonElement part in parts.EnumerateArray())
            {
                string tagText = part.TryGetProperty("tag", out JsonElement t) ? t.GetString() : null;
                if (!TryParseTag(tagText, out uint tagId))
                    continue;
                usedTags.Add($"0x{tagId:X2}");
                if (!tags.TryGetValue(tagId, out byte[] payload))
                    continue;

                int offset = part.TryGetProperty("byteOffset", out JsonElement bo) ? bo.GetInt32() : 0;
                int size = part.TryGetProperty("sizeBytes", out JsonElement sz)
                    ? sz.GetInt32()
                    : Math.Max(0, payload.Length - offset);
                if (offset >= payload.Length) continue;
                size = Math.Min(size, payload.Length - offset);
                var slice = new byte[size];
                Buffer.BlockCopy(payload, offset, slice, 0, size);
                sb.Append(FileLevelTagBag.AsAsciiNullPadded(slice));
            }

            return new JsonObject
            {
                ["kind"] = "text",
                ["value"] = sb.ToString(),
                ["tags"] = usedTags
            };
        }

        private static JsonObject DecodeEffectGrid(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(control);
            if (tag is null || !tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag for effect-grid '{name}'.");
                return new JsonObject { ["kind"] = "effect-grid", ["rows"] = new JsonArray() };
            }

            int rows = GetInt(control, "rows") ?? 16;
            int stride = GetInt(control, "rowStride") ?? 8;
            int onOff = 0, offOff = 4;
            if (control.TryGetProperty("fields", out JsonElement fields))
            {
                if (fields.TryGetProperty("on", out JsonElement on) &&
                    on.TryGetProperty("byteOffset", out JsonElement onB))
                    onOff = onB.GetInt32();
                if (fields.TryGetProperty("off", out JsonElement off) &&
                    off.TryGetProperty("byteOffset", out JsonElement offB))
                    offOff = offB.GetInt32();
            }

            // UI column header: "Triac #" / "Meter #"
            string numberKey = name.IndexOf("Triac", StringComparison.OrdinalIgnoreCase) >= 0
                ? "triac"
                : name.IndexOf("Meter", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "meter"
                    : "number";

            Dictionary<long, string> optionLabels = BuildReverseValuesFromOptions(control);
            var rowArr = new JsonArray();
            for (int i = 0; i < rows; i++)
            {
                int baseOff = i * stride;
                if (baseOff + Math.Max(onOff, offOff) + 4 > payload.Length)
                    break;
                uint onVal = BitConverter.ToUInt32(payload, baseOff + onOff);
                uint offVal = BitConverter.ToUInt32(payload, baseOff + offOff);
                string onLabel = EffectLabel(optionLabels, onVal);
                string offLabel = EffectLabel(optionLabels, offVal);
                if (string.IsNullOrEmpty(onLabel) && string.IsNullOrEmpty(offLabel))
                    continue;

                rowArr.Add(new JsonObject
                {
                    [numberKey] = i + 1,
                    ["on"] = onLabel,
                    ["off"] = offLabel
                });
            }

            return new JsonObject
            {
                ["kind"] = "effect-grid",
                ["tag"] = $"0x{tag.Value:X2}",
                ["rows"] = rowArr
            };
        }

        private static string EffectLabel(Dictionary<long, string> labels, long raw)
        {
            if (labels.TryGetValue(raw, out string label))
                return label ?? string.Empty;
            if (raw == 0)
                return string.Empty;
            return raw.ToString(CultureInfo.InvariantCulture);
        }

        private static JsonObject DecodePayoutSense(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            var button = new JsonObject();
            var mask = new JsonObject();

            if (control.TryGetProperty("button", out JsonElement buttonEl) &&
                buttonEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty cell in buttonEl.EnumerateObject())
                    button[cell.Name] = DecodePayoutSenseCell(name, "Button", cell.Name, cell.Value, tags, diagnostics);
            }

            if (control.TryGetProperty("mask", out JsonElement maskEl) &&
                maskEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty cell in maskEl.EnumerateObject())
                    mask[cell.Name] = DecodePayoutSenseCell(name, "Mask", cell.Name, cell.Value, tags, diagnostics);
            }

            return new JsonObject
            {
                ["kind"] = "payout-sense",
                ["Button"] = button,
                ["Mask"] = mask
            };
        }

        private static JsonNode DecodePayoutSenseCell(
            string controlName,
            string row,
            string column,
            JsonElement cell,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(cell);
            if (tag is null || !tags.TryGetValue(tag.Value, out byte[] payload) || payload is null)
            {
                diagnostics.Add($"Missing FML tag for '{controlName}' {row}/{column}.");
                return null;
            }

            string encoding = GetString(cell, "encoding") ?? "u32-le";
            int byteOffset = GetInt(cell, "byteOffset") ?? 0;
            long? raw = ReadScalar(payload, encoding, cell, byteOffset);
            int? emptyUi = GetInt(cell, "emptyUi");
            if (raw is null || (emptyUi is int empty && raw.Value == empty))
                return null;

            if (string.Equals(encoding, "hex-u32-le", StringComparison.OrdinalIgnoreCase))
            {
                int width = GetInt(cell, "displayWidth") ?? 8;
                return raw.Value.ToString("X" + width, CultureInfo.InvariantCulture);
            }

            return raw.Value;
        }

        private static JsonObject DecodeMeterGrid(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(control);
            if (tag is null || !tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag for meter-grid '{name}'.");
                return new JsonObject { ["kind"] = "meter-grid", ["in"] = new JsonArray(), ["out"] = new JsonArray() };
            }

            uint unset = (uint)(GetInt(control, "unsetValue") ?? 0);
            Dictionary<long, string> inMult = BuildReverseValues(control, "inMultiplierValues");
            Dictionary<long, string> outMult = BuildReverseValues(control, "outMultiplierValues");

            int meterTriacBase = 0;
            int meterTriacRows = 16;
            int meterTriacStride = 16;
            int meterInOff = 0, meterOutOff = 4, triacInOff = 8, triacOutOff = 12;
            if (control.TryGetProperty("meterTriacBlock", out JsonElement mt))
            {
                meterTriacBase = mt.TryGetProperty("byteOffset", out JsonElement bo) ? bo.GetInt32() : 0;
                meterTriacRows = mt.TryGetProperty("rowCount", out JsonElement rc) ? rc.GetInt32() : 16;
                meterTriacStride = mt.TryGetProperty("rowStride", out JsonElement rs) ? rs.GetInt32() : 16;
                if (mt.TryGetProperty("rowLayout", out JsonElement layout))
                {
                    meterInOff = layout.TryGetProperty("meterIn", out JsonElement a) ? a.GetInt32() : 0;
                    meterOutOff = layout.TryGetProperty("meterOut", out JsonElement b) ? b.GetInt32() : 4;
                    triacInOff = layout.TryGetProperty("triacIn", out JsonElement c) ? c.GetInt32() : 8;
                    triacOutOff = layout.TryGetProperty("triacOut", out JsonElement d) ? d.GetInt32() : 12;
                }
            }

            int secBase = 256;
            int secCount = 32;
            int secStride = 8;
            int secInOff = 0, secOutOff = 4;
            if (control.TryGetProperty("secBlock", out JsonElement sec))
            {
                secBase = sec.TryGetProperty("byteOffset", out JsonElement bo) ? bo.GetInt32() : 256;
                secCount = sec.TryGetProperty("entryCount", out JsonElement ec) ? ec.GetInt32() : 32;
                secStride = sec.TryGetProperty("entryStride", out JsonElement es) ? es.GetInt32() : 8;
                if (sec.TryGetProperty("entryLayout", out JsonElement layout))
                {
                    secInOff = layout.TryGetProperty("in", out JsonElement a) ? a.GetInt32() : 0;
                    secOutOff = layout.TryGetProperty("out", out JsonElement b) ? b.GetInt32() : 4;
                }
            }

            var inRows = new JsonArray();
            var outRows = new JsonArray();

            void AddIfSet(JsonArray dest, string source, uint raw, Dictionary<long, string> multLabels)
            {
                if (raw == unset) return;
                string mult = ResolveMultiplierLabel(raw, multLabels);
                dest.Add(new JsonObject
                {
                    ["source"] = source,
                    ["multiplier"] = mult
                });
            }

            for (int i = 0; i < meterTriacRows; i++)
            {
                int row = meterTriacBase + i * meterTriacStride;
                if (row + meterTriacStride > payload.Length) break;
                int n = i + 1;
                AddIfSet(inRows, "Meter " + n, BitConverter.ToUInt32(payload, row + meterInOff), inMult);
                AddIfSet(outRows, "Meter " + n, BitConverter.ToUInt32(payload, row + meterOutOff), outMult);
                AddIfSet(inRows, "Triac " + n, BitConverter.ToUInt32(payload, row + triacInOff), inMult);
                AddIfSet(outRows, "Triac " + n, BitConverter.ToUInt32(payload, row + triacOutOff), outMult);
            }

            for (int i = 0; i < secCount; i++)
            {
                int row = secBase + i * secStride;
                if (row + secStride > payload.Length) break;
                int s = i + 1;
                AddIfSet(inRows, "Sec " + s, BitConverter.ToUInt32(payload, row + secInOff), inMult);
                AddIfSet(outRows, "Sec " + s, BitConverter.ToUInt32(payload, row + secOutOff), outMult);
            }

            return new JsonObject
            {
                ["kind"] = "meter-grid",
                ["tag"] = $"0x{tag.Value:X2}",
                ["in"] = inRows,
                ["out"] = outRows
            };
        }

        /// <summary>
        /// Multipliers are normally combo indices (map tables). Some layouts store
        /// the signed literal instead (e.g. 0xFFFFFFFF for -1).
        /// </summary>
        private static string ResolveMultiplierLabel(uint raw, Dictionary<long, string> multLabels)
        {
            if (multLabels.TryGetValue(raw, out string label))
                return label;

            int signed = unchecked((int)raw);
            string signedText = signed.ToString(CultureInfo.InvariantCulture);
            foreach (string known in multLabels.Values)
            {
                if (string.Equals(known, signedText, StringComparison.Ordinal))
                    return known;
            }

            return signed < 0
                ? signedText
                : raw.ToString(CultureInfo.InvariantCulture);
        }

        private static JsonObject DecodeRecordList(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            var records = new JsonArray();
            if (!control.TryGetProperty("recordTags", out JsonElement recordTags))
            {
                diagnostics.Add($"record-list '{name}' missing recordTags.");
                return new JsonObject { ["kind"] = "record-list", ["records"] = records };
            }

            // Pair by index across tags that share the same record count.
            byte[] labelPayload = null;
            byte[] shortcutPayload = null;
            byte[] bodyPayload = null;
            int bodyStride = 16;
            int labelSize = 21; // 20-char buffer + trailing NUL (maps historically said 20)
            Dictionary<string, int> bodyFields = null;
            Dictionary<long, string> outputTypeValues = BuildReverseValues(control, "outputTypeValues");

            foreach (JsonProperty rt in recordTags.EnumerateObject())
            {
                if (!TryParseTag(rt.Name, out uint tagId))
                    continue;
                tags.TryGetValue(tagId, out byte[] payload);
                string encoding = rt.Value.TryGetProperty("encoding", out JsonElement enc)
                    ? enc.GetString()
                    : null;
                int size = rt.Value.TryGetProperty("sizeBytes", out JsonElement sz)
                    ? sz.GetInt32()
                    : (payload?.Length ?? 0);

                if (string.Equals(encoding, "ascii-null-padded", StringComparison.OrdinalIgnoreCase))
                {
                    labelPayload = payload;
                    if (rt.Value.TryGetProperty("sizeBytes", out JsonElement labelSz) &&
                        labelSz.TryGetInt32(out int ls) &&
                        ls > 0)
                        labelSize = ls;
                }
                else if (rt.Value.TryGetProperty("field", out JsonElement field) &&
                         field.GetString() == "shortcut")
                    // encodings: historical "u32-le", current "vk-u8"
                    shortcutPayload = payload;
                else if (string.Equals(encoding, "4xu32-le", StringComparison.OrdinalIgnoreCase))
                {
                    bodyPayload = payload;
                    bodyStride = size > 0 ? size : 16;
                    bodyFields = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (rt.Value.TryGetProperty("fields", out JsonElement fields))
                    {
                        foreach (JsonProperty f in fields.EnumerateObject())
                            bodyFields[f.Name] = f.Value.GetInt32();
                    }
                }
            }

            int count = 0;
            if (bodyPayload != null && bodyStride > 0)
                count = Math.Max(count, bodyPayload.Length / bodyStride);
            if (labelPayload != null)
                count = Math.Max(count, labelPayload.Length / Math.Max(labelSize, 1));
            if (shortcutPayload != null)
                count = Math.Max(count, shortcutPayload.Length / 4);

            for (int i = 0; i < count; i++)
            {
                var rec = new JsonObject { ["index"] = i };
                if (bodyPayload != null && bodyFields != null)
                {
                    int baseOff = i * bodyStride;
                    if (baseOff + bodyStride <= bodyPayload.Length)
                    {
                        foreach (KeyValuePair<string, int> f in bodyFields)
                        {
                            if (baseOff + f.Value + 4 <= bodyPayload.Length)
                            {
                                uint v = BitConverter.ToUInt32(bodyPayload, baseOff + f.Value);
                                if (f.Key == "outputType" && outputTypeValues.TryGetValue(v, out string otLabel))
                                    rec[f.Key] = otLabel;
                                else if (f.Key is "value1" or "value2")
                                    // MFME Value/Lamp fields display FML-1; 0 means blank.
                                    rec[f.Key] = v == 0 ? null : (JsonNode)(v - 1);
                                else
                                    rec[f.Key] = v;
                            }
                        }
                    }
                }

                if (labelPayload != null)
                {
                    int baseOff = i * labelSize;
                    if (baseOff < labelPayload.Length)
                    {
                        int len = Math.Min(labelSize, labelPayload.Length - baseOff);
                        var slice = new byte[len];
                        Buffer.BlockCopy(labelPayload, baseOff, slice, 0, len);
                        rec["label"] = FileLevelTagBag.AsAsciiNullPadded(slice);
                    }
                }

                if (shortcutPayload != null && i * 4 + 4 <= shortcutPayload.Length)
                {
                    // 0x95 is a 4-byte slot:
                    //   [0]=VK (0=none), [1]=modifier bits (usually 0),
                    //   [2..3]=auto label prefix junk (not shown in UI Shortcut column).
                    // Probed: typed Q → UI "Q", FML 51 00 41 6C; F2 → UI "F2", FML 71 00 42 65.
                    byte vk = shortcutPayload[i * 4];
                    byte mods = shortcutPayload[i * 4 + 1];
                    if (vk != 0)
                    {
                        rec["shortcut"] = vk;
                        if (mods != 0)
                            rec["shortcutMods"] = mods;
                    }
                }

                records.Add(rec);
            }

            return new JsonObject { ["kind"] = "record-list", ["records"] = records };
        }

        private static JsonObject DecodeIndexedEnumTable(
            string name,
            JsonElement control,
            Dictionary<uint, byte[]> tags,
            List<string> diagnostics)
        {
            uint? tag = GetPrimaryTag(control);
            if (tag is null || !tags.TryGetValue(tag.Value, out byte[] payload))
            {
                diagnostics.Add($"Missing FML tag for indexed-enum-table '{name}'.");
                return new JsonObject { ["kind"] = "indexed-enum-table", ["entries"] = new JsonArray() };
            }

            string entryEncoding = GetString(control, "entryEncoding") ?? "u16-le";
            int entrySize = entryEncoding.Contains("u16", StringComparison.Ordinal) ? 2 : 4;
            Dictionary<long, string> labels = BuildReverseValues(control, "values");
            bool functionLowParamHigh = IsFunctionLowParamHighLayout(control);
            string valueDisplayKind = null;
            if (control.TryGetProperty("valueDisplay", out JsonElement vd) &&
                vd.TryGetProperty("kind", out JsonElement vk))
                valueDisplayKind = vk.GetString();

            var entries = new JsonArray();
            for (int i = 0; i + entrySize <= payload.Length; i += entrySize)
            {
                long raw = entrySize == 2
                    ? BitConverter.ToUInt16(payload, i)
                    : BitConverter.ToUInt32(payload, i);

                if (functionLowParamHigh)
                {
                    long function = raw & 0xFF;
                    long param = (raw >> 8) & 0xFF;
                    var entry = new JsonObject
                    {
                        ["index"] = i / entrySize,
                        ["value"] = labels.TryGetValue(function, out string fn)
                            ? fn
                            : (JsonNode)function,
                        ["param"] = param,
                        ["raw"] = raw
                    };
                    entries.Add(entry);
                    continue;
                }

                // Legacy: valueDisplay u8-high-byte treats high byte as the enum code.
                long lookup = raw;
                if (string.Equals(valueDisplayKind, "u8-high-byte", StringComparison.OrdinalIgnoreCase))
                    lookup = (raw >> 8) & 0xFF;

                entries.Add(new JsonObject
                {
                    ["index"] = i / entrySize,
                    ["value"] = labels.TryGetValue(lookup, out string lab) ? lab : (JsonNode)lookup,
                    ["raw"] = raw
                });
            }

            return new JsonObject
            {
                ["kind"] = "indexed-enum-table",
                ["tag"] = $"0x{tag.Value:X2}",
                ["entries"] = entries
            };
        }

        /// <summary>
        /// SRU/SYSTEM80 Output Mappings: u16 = (param &lt;&lt; 8) | functionType.
        /// Function type is the radio (low byte); form Value is the high byte.
        /// </summary>
        private static bool IsFunctionLowParamHighLayout(JsonElement control)
        {
            if (control.TryGetProperty("valueDisplay", out JsonElement vd) &&
                vd.TryGetProperty("kind", out JsonElement vk))
            {
                string kind = vk.GetString();
                if (string.Equals(kind, "u8-low-function-u8-param", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kind, "function-low-param-high", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (control.TryGetProperty("entryLayout", out JsonElement layout) &&
                layout.ValueKind == JsonValueKind.Object)
            {
                string low = layout.TryGetProperty("lowByte", out JsonElement lb) ? lb.GetString() : null;
                string high = layout.TryGetProperty("highByte", out JsonElement hb) ? hb.GetString() : null;
                if (string.Equals(low, "function-type", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(high, "value", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(high, "param", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static long? ReadScalar(byte[] payload, string encoding, JsonElement control, int byteOffset = 0)
        {
            if (payload is null || payload.Length == 0) return null;
            if (byteOffset < 0 || byteOffset >= payload.Length) return null;
            int avail = payload.Length - byteOffset;
            switch (encoding)
            {
                case "u32-le":
                case "fml-uint32":
                case "hex-u32-le":
                case "integer":
                case "scalar-le":
                case "mirrored-u32-le":
                    if (avail >= 4) return BitConverter.ToUInt32(payload, byteOffset);
                    if (avail >= 2) return BitConverter.ToUInt16(payload, byteOffset);
                    return payload[byteOffset];
                case "bytes":
                case "u8":
                case "byte":
                    return payload[byteOffset];
                case "width-height-2xu32-le":
                    if (avail >= 4) return BitConverter.ToUInt32(payload, byteOffset);
                    return null;
                default:
                    if (avail >= 4) return BitConverter.ToUInt32(payload, byteOffset);
                    if (avail >= 2) return BitConverter.ToUInt16(payload, byteOffset);
                    return payload[byteOffset];
            }
        }

        private static string LookupValueLabel(JsonElement control, long raw)
        {
            if (!control.TryGetProperty("values", out JsonElement values) ||
                values.ValueKind != JsonValueKind.Object)
                return null;

            foreach (JsonProperty p in values.EnumerateObject())
            {
                if (p.Name is "unchecked" or "checked") continue;
                if (p.Value.ValueKind == JsonValueKind.Array) continue;
                if (JsonValueToLong(p.Value) == raw)
                    return p.Name;
            }

            return null;
        }

        private static string LookupWidthHeightLabel(JsonElement control, uint w, uint h)
        {
            if (!control.TryGetProperty("values", out JsonElement values) ||
                values.ValueKind != JsonValueKind.Object)
                return null;

            foreach (JsonProperty p in values.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Array) continue;
                var arr = p.Value.EnumerateArray().ToList();
                if (arr.Count < 2) continue;
                long? aw = JsonValueToLong(arr[0]);
                long? ah = JsonValueToLong(arr[1]);
                if (aw == w && ah == h)
                    return p.Name;
            }

            return null;
        }

        private static Dictionary<long, string> BuildReverseValues(JsonElement control, string propName)
        {
            var map = new Dictionary<long, string>();
            if (!control.TryGetProperty(propName, out JsonElement values) ||
                values.ValueKind != JsonValueKind.Object)
                return map;
            foreach (JsonProperty p in values.EnumerateObject())
            {
                long? v = JsonValueToLong(p.Value);
                if (v is long n && !map.ContainsKey(n))
                    map[n] = p.Name;
            }

            return map;
        }

        private static Dictionary<long, string> BuildReverseValuesFromOptions(JsonElement control)
        {
            var map = new Dictionary<long, string>();
            if (control.TryGetProperty("confirmedEffectCodes", out JsonElement codes) &&
                codes.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in codes.EnumerateObject())
                {
                    long? v = JsonValueToLong(p.Value);
                    if (v is long n) map[n] = p.Name;
                }
            }

            if (control.TryGetProperty("options", out JsonElement options) &&
                options.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (JsonElement opt in options.EnumerateArray())
                {
                    string label = opt.GetString() ?? string.Empty;
                    if (!map.ContainsKey(i))
                        map[i] = label;
                    i++;
                }
            }

            return map;
        }

        private static JsonNode LabelOrRaw(Dictionary<long, string> labels, long raw) =>
            labels.TryGetValue(raw, out string label) ? label : raw;

        private static uint? GetPrimaryTag(JsonElement control)
        {
            if (control.TryGetProperty("tag", out JsonElement tagEl))
            {
                if (TryParseTag(tagEl.GetString(), out uint t)) return t;
            }

            if (control.TryGetProperty("tags", out JsonElement tags) &&
                tags.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement el in tags.EnumerateArray())
                {
                    if (TryParseTag(el.GetString(), out uint t)) return t;
                }
            }

            return null;
        }

        private static bool TryParseTag(string text, out uint tag)
        {
            tag = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out tag);
            }

            return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out tag);
        }

        private static string GetString(JsonElement el, string name) =>
            el.TryGetProperty(name, out JsonElement p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;

        private static int? GetInt(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out JsonElement p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int i)) return i;
            if (p.ValueKind == JsonValueKind.String &&
                int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
                return i;
            return null;
        }

        private static bool? GetBool(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out JsonElement p)) return null;
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
            return null;
        }

        private static long? JsonValueToLong(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out long l)) return l;
            if (el.ValueKind == JsonValueKind.String &&
                long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                return l;
            return null;
        }

        private static string AsciiPrefix(byte[] payload, int maxLen)
        {
            int n = Math.Min(maxLen, payload.Length);
            var slice = new byte[n];
            Buffer.BlockCopy(payload, 0, slice, 0, n);
            return FileLevelTagBag.AsAsciiNullPadded(slice);
        }
    }
}
