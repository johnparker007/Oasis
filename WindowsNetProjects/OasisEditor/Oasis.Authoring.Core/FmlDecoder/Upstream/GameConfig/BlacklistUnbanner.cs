using System;
using System.Collections.Generic;
using MfmeFmlDecoder.Decoder;

namespace MfmeFmlDecoder.GameConfig
{
    /// <summary>
    /// A single byte-level change that clears a blacklist marker from the plaintext.
    /// </summary>
    internal sealed class UnbanEdit
    {
        public int Offset;
        public string Field;
        public string Kind;
        public int RecordIndex;
        public byte[] Original;
        public byte[] Replacement;

        public override string ToString() =>
            $"{Kind} in {Field} of record {RecordIndex} at 0x{Offset:X}: " +
            $"{Convert.ToHexString(Original)} -> {Convert.ToHexString(Replacement)}";
    }

    /// <summary>
    /// Outcome of <see cref="BlacklistUnbanner.Unban"/>.
    /// </summary>
    internal sealed class UnbanResult
    {
        public byte[] Plaintext;
        public int PayloadsScanned;
        public int RecordsScanned;
        public List<UnbanEdit> Edits = new List<UnbanEdit>();
        public List<string> Warnings = new List<string>();

        public bool Changed => Edits.Count > 0;
    }

    /// <summary>
    /// Clears the layout-blacklist markers MFME 20.1 looks for when loading an FML.
    ///
    /// The loader scans the payload of file-level tag 0x97 (dispatch case 0x98 ->
    /// FUN_00511950, RVA 0x111950). That payload is a run of records:
    ///
    ///     u32 len + bytes   field 1
    ///     u32 len + bytes   field 2  <- searched for the substring needles
    ///     u32 len + bytes   field 3
    ///     u32 len + bytes   field 4
    ///     8 raw bytes       timestamp
    ///     u32 len + bytes   field 5  (its first byte weights a hit via bits 3 / 2)
    ///     u32 len + bytes   field 6  <- compared for equality with the exact needle
    ///                       (only read when payload bytes remain)
    ///
    /// Each record's fields are read as NUL-terminated C strings, so only the bytes
    /// before the first 0x00 are ever visible to the loader. A record that matches
    /// the exact needle scores 21 (20 + 1) and each substring hit scores at least 1
    /// more; a total above 10 makes the loader refuse the layout with the
    /// "Blacklisted Layout" message box and layout load fails.
    ///
    /// Needles verified against MFME 20.1 at 0xA89645 (exact) and the five
    /// 7-byte entries at 0xA89650/58/60/68/70. Only the exact compare reads
    /// field 6; the five substring compares all read field 2.
    /// </summary>
    internal static class BlacklistUnbanner
    {
        /// <summary>File-level tag whose payload the loader scans.</summary>
        internal const uint LayoutPayloadTag = 0x97;

        /// <summary>Compared for equality against the C string in field 6.</summary>
        internal static readonly byte[] ExactNeedle = Hex("F8 8D F3 DF 9B B1 F9 CC C9 D8");

        /// <summary>Searched for as substrings of the C string in field 2.</summary>
        internal static readonly byte[][] SubstringNeedles =
        {
            Hex("AE EE D3 DF 9E D5 A5"),
            Hex("A5 E0 D6 D2 FD AE AB"),
            Hex("D1 E4 D4 AC F9 B2 CD"),
            Hex("CD E3 A8 C8 9D B0 D1"),
            Hex("A8 E2 AB C8 FD AB CE"),
        };

        /// <summary>
        /// Returns a copy of <paramref name="plain"/> with every blacklist marker
        /// cleared. When nothing matches, <see cref="UnbanResult.Plaintext"/> is the
        /// original array and <see cref="UnbanResult.Changed"/> is false.
        /// </summary>
        public static UnbanResult Unban(byte[] plain)
        {
            if (plain is null) throw new ArgumentNullException(nameof(plain));

            var result = new UnbanResult { Plaintext = plain };
            List<PayloadSpan> payloads = FindLayoutPayloads(plain, result.Warnings);

            if (payloads.Count == 0)
            {
                result.Warnings.Add(
                    $"No file-level 0x{LayoutPayloadTag:X2} tag found; nothing to unban.");
                return result;
            }

            foreach (PayloadSpan payload in payloads)
            {
                result.PayloadsScanned++;
                ScanPayload(plain, payload, result.RecordsScanned, result.Edits, result.Warnings,
                    out int records);
                result.RecordsScanned = records;
            }

            if (result.Edits.Count == 0)
                return result;

            byte[] edited = (byte[])plain.Clone();
            foreach (UnbanEdit edit in result.Edits)
                Buffer.BlockCopy(edit.Replacement, 0, edited, edit.Offset, edit.Replacement.Length);

            // The rewritten plaintext must scan clean; never report success otherwise.
            var recheck = new List<UnbanEdit>();
            var sink = new UnbanResult { Plaintext = edited };
            foreach (PayloadSpan payload in FindLayoutPayloads(edited, sink.Warnings))
                ScanPayload(edited, payload, 0, recheck, sink.Warnings, out _);

            if (recheck.Count != 0)
            {
                throw new InvalidOperationException(
                    $"{recheck.Count} blacklist marker(s) survived unbanning " +
                    $"(first at 0x{recheck[0].Offset:X}); refusing to report success.");
            }

            result.Plaintext = edited;
            return result;
        }

        private readonly struct PayloadSpan
        {
            public PayloadSpan(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public int Offset { get; }
            public int Length { get; }
        }

        private readonly struct Field
        {
            public Field(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public int Offset { get; }
            public int Length { get; }
        }

        private static List<PayloadSpan> FindLayoutPayloads(byte[] plain, List<string> warnings)
        {
            var result = new List<PayloadSpan>();
            int pos = 0;

            while (pos + 8 <= plain.Length)
            {
                uint tag = BitConverter.ToUInt32(plain, pos);
                uint length = BitConverter.ToUInt32(plain, pos + 4);
                if (tag == FileLevelTagBag.TerminationTag)
                    break;

                pos += 8;
                if (length > (uint)(plain.Length - pos))
                {
                    warnings.Add(
                        $"TLV tag 0x{tag:X} length 0x{length:X} overruns the buffer at 0x{pos:X}; stopped scanning.");
                    break;
                }

                if (tag == LayoutPayloadTag)
                    result.Add(new PayloadSpan(pos, (int)length));

                pos += (int)length;

                if (LengthPrefixedStringTableScanner.IsFileLevelTag(tag))
                {
                    pos += LengthPrefixedStringTableScanner.MeasureSpan(
                        plain, pos, hostTagKeyByte: (byte)tag);
                }
            }

            return result;
        }

        private static void ScanPayload(
            byte[] plain,
            PayloadSpan payload,
            int recordsSoFar,
            List<UnbanEdit> edits,
            List<string> warnings,
            out int recordCount)
        {
            int pos = 0;
            int record = recordsSoFar;

            // Mirrors the loader: keep starting records while payload bytes remain.
            while (pos < payload.Length)
            {
                var fields = new Field[6];
                bool truncated = false;

                for (int i = 0; i < 4 && !truncated; i++)
                    truncated = !TryReadBlob(plain, payload, ref pos, out fields[i]);

                if (!truncated)
                {
                    if (payload.Length - pos < 8)
                        truncated = true;
                    else
                        pos += 8;
                }

                if (!truncated)
                    truncated = !TryReadBlob(plain, payload, ref pos, out fields[4]);

                if (!truncated)
                {
                    if (pos < payload.Length)
                        truncated = !TryReadBlob(plain, payload, ref pos, out fields[5]);
                    else
                        fields[5] = new Field(payload.Offset + pos, 0);
                }

                if (truncated)
                {
                    warnings.Add(
                        $"Record {record} in the 0x{LayoutPayloadTag:X2} payload at 0x{payload.Offset:X} is " +
                        $"truncated after {pos} of {payload.Length} byte(s); later records were not scanned.");
                    break;
                }

                MatchField2(plain, fields[1], record, edits);
                MatchField6(plain, fields[5], record, edits);
                record++;
            }

            recordCount = record;
        }

        /// <summary>Substring needles are searched for in field 2.</summary>
        private static void MatchField2(byte[] plain, Field field, int record, List<UnbanEdit> edits)
        {
            int visible = CStringLength(plain, field.Offset, field.Length);
            if (visible <= 0)
                return;

            var hits = new SortedSet<int>();
            foreach (byte[] needle in SubstringNeedles)
            {
                int at = IndexOf(plain, field.Offset, visible, needle);
                while (at >= 0)
                {
                    hits.Add(at);
                    at = IndexOf(plain, at + 1, field.Offset + visible - (at + 1), needle);
                }
            }

            foreach (int at in hits)
            {
                byte[] needle = NeedleAt(plain, at);
                if (needle is null)
                    continue;

                if (!TryBreakField2(plain, field, visible, at, needle, out int editOffset, out byte replacement))
                {
                    throw new InvalidOperationException(
                        $"Cannot clear the substring needle at 0x{at:X} in field 2 of record {record}.");
                }

                var edit = new UnbanEdit
                {
                    Offset = editOffset,
                    Field = "field 2",
                    Kind = "substring needle " + Convert.ToHexString(needle),
                    RecordIndex = record,
                    Original = new[] { plain[editOffset] },
                    Replacement = new[] { replacement },
                };
                edits.Add(edit);
            }
        }

        /// <summary>The exact needle is compared against field 6.</summary>
        private static void MatchField6(byte[] plain, Field field, int record, List<UnbanEdit> edits)
        {
            int visible = CStringLength(plain, field.Offset, field.Length);
            if (visible != ExactNeedle.Length)
                return;
            if (!RegionEquals(plain, field.Offset, ExactNeedle))
                return;

            if (!TryBreakExact(plain, field, out int editOffset, out byte replacement))
            {
                throw new InvalidOperationException(
                    $"Cannot clear the exact needle in field 6 of record {record}.");
            }

            edits.Add(new UnbanEdit
            {
                Offset = editOffset,
                Field = "field 6",
                Kind = "exact needle",
                RecordIndex = record,
                Original = new[] { plain[editOffset] },
                Replacement = new[] { replacement },
            });
        }

        /// <summary>
        /// Picks the smallest change to the last byte of a field-2 needle hit that
        /// leaves no substring needle visible in the field's C string.
        /// </summary>
        private static bool TryBreakField2(
            byte[] plain,
            Field field,
            int visible,
            int matchOffset,
            byte[] needle,
            out int editOffset,
            out byte replacement)
        {
            byte[] window = new byte[visible];
            Buffer.BlockCopy(plain, field.Offset, window, 0, visible);

            int relative = matchOffset - field.Offset + needle.Length - 1;
            return TryCandidates(window, relative, out editOffset, out replacement,
                rel => field.Offset + rel);
        }

        /// <summary>
        /// Picks the smallest change to the last byte of field 6 that makes it differ
        /// from the exact needle and hides every substring needle.
        /// </summary>
        private static bool TryBreakExact(
            byte[] plain,
            Field field,
            out int editOffset,
            out byte replacement)
        {
            byte[] window = new byte[ExactNeedle.Length];
            Buffer.BlockCopy(plain, field.Offset, window, 0, ExactNeedle.Length);

            return TryCandidates(window, ExactNeedle.Length - 1, out editOffset, out replacement,
                rel => field.Offset + rel, requireNotEqualToExactNeedle: true);
        }

        /// <summary>
        /// Mutates <paramref name="relative"/> in <paramref name="window"/> with the
        /// first candidate that leaves no needle crossing that byte. Checking only
        /// the mutated byte keeps other, separately scheduled hits in the same field
        /// intact. Candidates are ordered smallest-change-first.
        /// </summary>
        private static bool TryCandidates(
            byte[] window,
            int relative,
            out int editOffset,
            out byte replacement,
            Func<int, int> toAbsolute,
            bool requireNotEqualToExactNeedle = false)
        {
            editOffset = -1;
            replacement = 0;

            byte original = window[relative];
            byte[] candidates = { (byte)(original ^ 0x01), 0xFF, 0xAA, 0x55, 0x00 };

            foreach (byte candidate in candidates)
            {
                if (candidate == original)
                    continue;

                window[relative] = candidate;
                int visible = Array.IndexOf(window, (byte)0);
                if (visible < 0)
                    visible = window.Length;

                bool ok = !requireNotEqualToExactNeedle || !RegionEquals(window, 0, ExactNeedle);
                if (ok && !AnyNeedleCrosses(window, visible, relative))
                {
                    editOffset = toAbsolute(relative);
                    replacement = candidate;
                    window[relative] = original;
                    return true;
                }

                window[relative] = original;
            }

            return false;
        }

        /// <summary>
        /// True when any needle occurrence in the visible part of
        /// <paramref name="window"/> spans <paramref name="index"/>.
        /// </summary>
        private static bool AnyNeedleCrosses(byte[] window, int visible, int index)
        {
            foreach (byte[] needle in SubstringNeedles)
            {
                int at = IndexOf(window, 0, visible, needle);
                while (at >= 0)
                {
                    if (at <= index && index < at + needle.Length)
                        return true;

                    at = IndexOf(window, at + 1, visible - (at + 1), needle);
                }
            }

            return false;
        }

        private static bool TryReadBlob(
            byte[] plain,
            PayloadSpan payload,
            ref int pos,
            out Field field)
        {
            field = default;
            if (payload.Length - pos < 4)
                return false;

            int length = BitConverter.ToInt32(plain, payload.Offset + pos);
            if (length < 0 || length > payload.Length - pos - 4)
                return false;

            field = new Field(payload.Offset + pos + 4, length);
            pos += 4 + length;
            return true;
        }

        private static int CStringLength(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (data[offset + i] == 0)
                    return i;
            }

            return length;
        }

        private static byte[] NeedleAt(byte[] data, int offset)
        {
            foreach (byte[] needle in SubstringNeedles)
            {
                if (RegionEquals(data, offset, needle))
                    return needle;
            }

            return null;
        }

        private static bool RegionEquals(byte[] data, int offset, byte[] pattern)
        {
            if (offset < 0 || offset + pattern.Length > data.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                    return false;
            }

            return true;
        }

        private static int IndexOf(byte[] data, int offset, int length, byte[] pattern)
        {
            if (pattern.Length == 0 || length < pattern.Length)
                return -1;

            int last = offset + length - pattern.Length;
            for (int i = offset; i <= last; i++)
            {
                int j = 0;
                while (j < pattern.Length && data[i + j] == pattern[j])
                    j++;

                if (j == pattern.Length)
                    return i;
            }

            return -1;
        }

        private static byte[] Hex(string spaced)
        {
            return Convert.FromHexString(spaced.Replace(" ", string.Empty));
        }
    }
}
