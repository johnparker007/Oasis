using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using MfmeFmlDecoder.Decoder;
using MfmeFmlDecoder.Decryption;
using MfmeFmlDecoder.src.Decoder.Component.Core;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.Utilities;

namespace MfmeFmlDecoder.Diagnostics
{
    /// <summary>
    /// Batch tool: decode a legacy zip entry and its 20.1 resave, then compare the
    /// serializable object graph and the ordered JSON separately.
    /// </summary>
    internal static class LegacyPairVerifier
    {
        private const string ManifestOption = "--verify-legacy-pairs=";
        private const string VersionOption = "--verify-version=";

        public static bool TryRun(string[] args, out int exitCode)
        {
            exitCode = 0;
            string manifestPath = null;
            string versionFilter = null;

            foreach (string arg in args)
            {
                if (arg.StartsWith(ManifestOption, StringComparison.Ordinal))
                {
                    manifestPath = arg.Substring(ManifestOption.Length);
                }
                else if (arg.StartsWith(VersionOption, StringComparison.Ordinal))
                {
                    versionFilter = arg.Substring(VersionOption.Length);
                }
            }

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return false;
            }

            exitCode = Run(manifestPath, versionFilter);
            return true;
        }

        private static int Run(string manifestPath, string versionFilter)
        {
            bool wasQuiet = RunLog.Quiet;
            RunLog.Quiet = true;
            try
            {
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"Manifest not found: {manifestPath}");
                return 2;
            }

            var skipDetailLog = new List<string>();

            MfmeVersion? filter = null;
            if (!string.IsNullOrWhiteSpace(versionFilter)
                && MfmeVersion.TryParse(versionFilter, out MfmeVersion parsedFilter))
            {
                filter = parsedFilter;
            }

            var stats = new Dictionary<string, int>(StringComparer.Ordinal);
            var mismatches = new List<string>();
            int total = 0;
            int deepEqual = 0;
            int orderedEqual = 0;
            int oldFailures = 0;
            int newFailures = 0;
            int skipped = 0;

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            foreach (JsonElement pair in manifest.RootElement.EnumerateArray())
            {
                string versionText = GetString(pair, "version");
                if (filter.HasValue)
                {
                    MfmeVersion pairVersion = MfmeVersion.Parse(versionText);
                    if (pairVersion != filter.Value)
                    {
                        continue;
                    }
                }

                int pairId = GetInt(pair, "id");
                total++;

                string id = pairId.ToString(CultureInfo.InvariantCulture);
                string machine = GetString(pair, "machine");
                string zipPath = GetString(pair, "zip_path");
                string oldEntry = GetString(pair, "old_entry");
                string newPath = GetString(pair, "new_path");
                string key = $"{id} {versionText} {machine}";

                if (!File.Exists(zipPath))
                {
                    skipped++;
                    Increment(stats, "missing legacy zip");
                    skipDetailLog.Add($"{id} {machine}: missing legacy zip");
                    continue;
                }

                byte[] oldBytes;
                try
                {
                    oldBytes = ReadZipEntry(zipPath, oldEntry);
                }
                catch (Exception)
                {
                    skipped++;
                    Increment(stats, "missing legacy zip entry");
                    skipDetailLog.Add($"{id} {machine}: missing legacy zip entry ({oldEntry})");
                    continue;
                }

                if (!File.Exists(newPath))
                {
                    skipped++;
                    Increment(stats, "missing new path");
                    skipDetailLog.Add($"{id} {machine}: missing new path");
                    continue;
                }

                byte[] newBytes = File.ReadAllBytes(newPath);
                newBytes = DecryptIfFml(newPath, newBytes);
                string newVersion = MfmeVersionReader.Read(newBytes, 0);
                if (!string.Equals(newVersion.Trim(), "20.1", StringComparison.Ordinal))
                {
                    skipped++;
                    Increment(stats, "new side not 20.1");
                    skipDetailLog.Add($"{id} {machine}: new side version {newVersion}");
                    continue;
                }

                Layout oldLayout;
                try
                {
                    oldLayout = Decode(DecryptIfFml(oldEntry, oldBytes));
                }
                catch (Exception ex)
                {
                    oldFailures++;
                    Increment(stats, "old failure");
                    mismatches.Add($"{key}: OLD_FAIL {FirstLine(ex.Message)}");
                    continue;
                }

                Layout newLayout;
                try
                {
                    newLayout = Decode(newBytes);
                }
                catch (Exception ex)
                {
                    newFailures++;
                    Increment(stats, "new failure");
                    mismatches.Add($"{key}: NEW_FAIL {FirstLine(ex.Message)}");
                    continue;
                }

                string oldJson = oldLayout.ToJson(indented: false);
                string newJson = newLayout.ToJson(indented: false);
                JsonNode oldNode = JsonNode.Parse(oldJson);
                JsonNode newNode = JsonNode.Parse(newJson);
                bool isDeepEqual = JsonNode.DeepEquals(oldNode, newNode);
                bool isOrderedEqual = string.Equals(oldJson, newJson, StringComparison.Ordinal);

                if (isDeepEqual)
                {
                    deepEqual++;
                }
                else
                {
                    Increment(stats, "json differs");
                    mismatches.Add($"{key}: DIFF {FirstDifference(oldNode, newNode)}");
                }

                if (isOrderedEqual)
                {
                    orderedEqual++;
                }
                else
                {
                    Increment(stats, "ordered json differs");
                }
            }

            Console.WriteLine($"pairs: {total}");
            Console.WriteLine($"deep equal: {deepEqual}");
            Console.WriteLine($"ordered equal: {orderedEqual}");
            Console.WriteLine($"old failures: {oldFailures}");
            Console.WriteLine($"new failures: {newFailures}");
            Console.WriteLine($"skipped: {skipped}");
            if (skipDetailLog.Count > 0)
            {
                Console.WriteLine($"skipped (details): {skipDetailLog.Count}");
                foreach (string skip in skipDetailLog)
                {
                    Console.WriteLine($"  {skip}");
                }
            }

            foreach (var kvp in stats)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            foreach (string mismatch in mismatches)
            {
                Console.WriteLine(mismatch);
            }

            return deepEqual + skipped == total && orderedEqual + skipped == total ? 0 : 1;
            }
            finally
            {
                RunLog.Quiet = wasQuiet;
            }
        }

        private static Layout Decode(byte[] plaintext)
        {
            var parser = new ComponentParser();
            var walker = new FileWalker(new ComponentWalker(parser));
            walker.WalkTlv(plaintext, 0);
            return parser.ToLayout(walker.Header);
        }

        private static byte[] DecryptIfFml(string path, byte[] bytes)
        {
            return string.Equals(Path.GetExtension(path), ".fml", StringComparison.OrdinalIgnoreCase)
                ? FmlDecryptor.Decrypt(bytes)
                : bytes;
        }

        private static byte[] ReadZipEntry(string zipPath, string entryName)
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            if (entry is null)
            {
                foreach (ZipArchiveEntry candidate in archive.Entries)
                {
                    if (string.Equals(
                            candidate.FullName.Replace('\\', '/'),
                            entryName.Replace('\\', '/'),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        break;
                    }
                }
            }

            if (entry is null)
            {
                throw new FileNotFoundException($"Zip entry '{entryName}' not found in '{zipPath}'.");
            }

            using Stream stream = entry.Open();
            using MemoryStream output = new();
            stream.CopyTo(output);
            return output.ToArray();
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out JsonElement value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int GetInt(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out JsonElement value)
                ? value.GetInt32()
                : 0;
        }

        private static void Increment(Dictionary<string, int> stats, string key)
        {
            stats.TryGetValue(key, out int count);
            stats[key] = count + 1;
        }

        private static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int newline = value.IndexOfAny(new[] { '\r', '\n' });
            return newline >= 0 ? value.Substring(0, newline) : value;
        }

        private static string FirstDifference(JsonNode left, JsonNode right, string path = "")
        {
            if (JsonNode.DeepEquals(left, right))
            {
                return "none";
            }

            if (left is null || right is null)
            {
                return path + ": " + (left?.ToJsonString() ?? "null") + " != " + (right?.ToJsonString() ?? "null");
            }

            if (left.GetValueKind() != right.GetValueKind())
            {
                return path + ": kind " + left.GetValueKind() + " != " + right.GetValueKind();
            }

            if (left is JsonObject typedObject
                && typedObject.TryGetPropertyValue("Type", out JsonNode typeNode)
                && !path.EndsWith("(" + typeNode + ")", StringComparison.Ordinal))
            {
                path += "(" + typeNode + ")";
            }

            if (left is JsonObject leftObject && right is JsonObject rightObject)
            {
                foreach (var kvp in leftObject)
                {
                    if (!rightObject.TryGetPropertyValue(kvp.Key, out JsonNode rightValue))
                    {
                        return path + "/" + kvp.Key + ": only old";
                    }

                    JsonNode leftValue = kvp.Value;
                    if (!JsonNode.DeepEquals(leftValue, rightValue))
                    {
                        return FirstDifference(leftValue, rightValue, path + "/" + kvp.Key);
                    }
                }

                foreach (var kvp in rightObject)
                {
                    if (!leftObject.ContainsKey(kvp.Key))
                    {
                        return path + "/" + kvp.Key + ": only new";
                    }
                }

                return path + ": object differs";
            }

            if (left is JsonArray leftArray && right is JsonArray rightArray)
            {
                int count = Math.Min(leftArray.Count, rightArray.Count);
                for (int i = 0; i < count; i++)
                {
                    if (!JsonNode.DeepEquals(leftArray[i], rightArray[i]))
                    {
                        return FirstDifference(leftArray[i], rightArray[i], path + "[" + i + "]");
                    }
                }

                if (leftArray.Count != rightArray.Count)
                {
                    return path + ": length " + leftArray.Count + " != " + rightArray.Count;
                }
            }

            return path + ": " + left.ToJsonString() + " != " + right.ToJsonString();
        }

    }
}
