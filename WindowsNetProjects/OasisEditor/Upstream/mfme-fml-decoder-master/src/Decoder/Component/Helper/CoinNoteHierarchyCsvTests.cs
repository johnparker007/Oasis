using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MfmeFmlDecoder.src.Decoder.Component.Helper;
using Xunit;

namespace MfmeFmlDecoder.Tests.Decoder.Component.Helper
{
    public class CoinNoteHierarchyCsvTests
    {
        private static readonly string CsvPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "TestData",
            "CoinNoteOptions.csv"
        );

        public static IEnumerable<object[]> CoinNoteRows()
        {
            foreach (var row in EnumerateCsvRows())
            {
                yield return new object[] { row.CoinNote, row.CoinNoteId, row.EffectId };
            }
        }

        public static IEnumerable<object[]> EffectRows()
        {
            foreach (var row in EnumerateCsvRows())
            {
                yield return new object[] { row.Effect, row.CoinNoteId, row.EffectId };
            }
        }

        private static IEnumerable<CsvRow> EnumerateCsvRows()
        {
            var lines = File.ReadAllLines(CsvPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = ParseCsvLine(line);
                if (parts.Length < 5) continue;

                // Skip header row
                if (parts[0] == "filename") continue;

                var coinNoteIdStr = parts[3].Trim();
                var effectIdStr   = parts[4].Trim();

                // Skip rows with unknown or unresolvable IDs
                if (string.IsNullOrEmpty(coinNoteIdStr) || string.IsNullOrEmpty(effectIdStr)) continue;

                yield return new CsvRow(
                    parts[1],
                    parts[2],
                    ParseHex(coinNoteIdStr),
                    (uint)ParseHex(effectIdStr));
            }
        }

        [Theory]
        [MemberData(nameof(CoinNoteRows))]
        public void ResolveCoinNote_MatchesCsvExpectation(string expectedCoinNote, int coinNoteId, uint effectId)
        {
            Assert.Equal(expectedCoinNote, CoinNoteHierarchy.ResolveCoinNote(coinNoteId, effectId));
        }

        [Theory]
        [MemberData(nameof(EffectRows))]
        public void ResolveEffect_MatchesCsvExpectation(string expectedEffect, int coinNoteId, uint effectId)
        {
            Assert.Equal(expectedEffect, CoinNoteHierarchy.ResolveEffect(coinNoteId, effectId));
        }

        private readonly struct CsvRow
        {
            public CsvRow(string coinNote, string effect, int coinNoteId, uint effectId)
            {
                CoinNote = coinNote;
                Effect = effect;
                CoinNoteId = coinNoteId;
                EffectId = effectId;
            }

            public string CoinNote { get; }
            public string Effect { get; }
            public int CoinNoteId { get; }
            public uint EffectId { get; }
        }

        private static int ParseHex(string s)
        {
            var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s;
            return (int)uint.Parse(hex, NumberStyles.HexNumber);
        }

        private static string[] ParseCsvLine(string line)
        {
            var result   = new List<string>();
            var inQuotes = false;
            var current  = new StringBuilder();

            foreach (var c in line)
            {
                if (c == '"')
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}