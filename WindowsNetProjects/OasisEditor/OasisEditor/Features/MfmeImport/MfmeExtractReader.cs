using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OasisEditor.Features.MfmeImport;

internal sealed class MfmeExtractReader : IMfmeExtractReader
{
    public MfmeImportResult Read(MfmeImportContext context)
    {
        var warnings = new List<MfmeImportWarning>();
        var errors = new List<string>();
        var skipped = new List<MfmeExtractComponentData>();

        if (!Directory.Exists(context.SourceExtractPath))
        {
            errors.Add($"MFME extract directory does not exist: '{context.SourceExtractPath}'.");
            return new MfmeImportResult { Errors = errors };
        }

        if (!Directory.Exists(context.ProjectRootPath))
        {
            errors.Add($"Project root does not exist: '{context.ProjectRootPath}'.");
        }

        if (!Directory.Exists(context.AssetsRootPath))
        {
            errors.Add($"Assets root does not exist: '{context.AssetsRootPath}'.");
        }

        if (errors.Count > 0)
        {
            return new MfmeImportResult { Errors = errors };
        }

        var manifestPath = TryFindManifestPath(context.SourceExtractPath);
        if (manifestPath is null)
        {
            errors.Add("Could not find MFME layout manifest (.json) in the extract directory.");
            return new MfmeImportResult { Errors = errors };
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Failed to read manifest '{manifestPath}': {ex.Message}");
            return new MfmeImportResult { Errors = errors };
        }

        using (document)
        {
            var root = document.RootElement;
            var layoutName = ReadString(root, "ASName")
                ?? context.LayoutDisplayName
                ?? new DirectoryInfo(context.SourceExtractPath).Name;

            if (ReadString(root, "ASName") is null)
            {
                warnings.Add(new MfmeImportWarning(
                    Code: "missing-layout-name",
                    Message: "Manifest did not contain ASName; using fallback layout name.",
                    ContextPath: manifestPath));
            }

            var imported = new List<MfmeExtractComponentData>();
            if (!root.TryGetProperty("Components", out var componentsElement) || componentsElement.ValueKind != JsonValueKind.Array)
            {
                warnings.Add(new MfmeImportWarning(
                    Code: "missing-components",
                    Message: "Manifest did not contain a Components array; import will continue with no components.",
                    ContextPath: manifestPath));
            }
            else
            {
                foreach (var component in componentsElement.EnumerateArray())
                {
                    if (!MfmeExtractComponentParser.TryParse(component, manifestPath, warnings.Add, out var parsed) || parsed is null)
                    {
                        warnings.Add(new MfmeImportWarning(
                            Code: "unsupported-component",
                            Message: "Skipped unsupported MFME component type.",
                            ContextPath: manifestPath));

                        skipped.Add(new MfmeUnknownComponentData
                        {
                            Kind = MfmeComponentKind.Unknown,
                            SourceType = ReadString(component, "$type") ?? ReadString(component, "Type") ?? "Unknown",
                            DisplayName = ReadString(component, "TextBoxText"),
                            RawJson = component.GetRawText()
                        });

                        continue;
                    }

                    imported.Add(parsed);
                }
            }

            return new MfmeImportResult
            {
                ExtractDocument = new MfmeExtractDocument
                {
                    SourceExtractPath = context.SourceExtractPath,
                    ManifestPath = manifestPath,
                    LayoutName = layoutName,
                    Components = imported
                },
                ImportedElements = imported,
                CopiedAssets = [],
                SkippedComponents = skipped,
                Warnings = warnings,
                Errors = errors
            };
        }
    }

    private static string? TryFindManifestPath(string extractPath)
    {
        var jsonFiles = Directory
            .EnumerateFiles(extractPath, "*.json", SearchOption.TopDirectoryOnly)
            .ToArray();

        if (jsonFiles.Length == 0)
        {
            return null;
        }

        var directoryName = new DirectoryInfo(extractPath).Name;
        return jsonFiles.FirstOrDefault(path =>
                   string.Equals(Path.GetFileNameWithoutExtension(path), directoryName, StringComparison.OrdinalIgnoreCase))
               ?? jsonFiles[0];
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
