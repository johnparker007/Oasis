using System.IO;
using System.Text;
using System.Text.Json;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Features.FmlImport;

internal sealed class FmlImportDiagnosticsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void WriteDecodedLayout(Layout layout, string stagingDirectory)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Directory.CreateDirectory(stagingDirectory);
        File.WriteAllText(Path.Combine(stagingDirectory, "decoded-layout.json"), layout.ToJson(indented: true), Encoding.UTF8);
    }

    public void WriteMappedElements(
        FmlToOasisMapResult mapResult,
        Layout layout,
        IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths,
        string stagingDirectory)
    {
        ArgumentNullException.ThrowIfNull(mapResult);
        ArgumentNullException.ThrowIfNull(layout);
        Directory.CreateDirectory(stagingDirectory);

        var document = new
        {
            Elements = mapResult.Elements
                .OrderBy(e => e.SourceComponentIndex ?? int.MaxValue)
                .ThenBy(e => e.SourceElementIndex ?? int.MaxValue)
                .ThenBy(e => e.Kind.ToString(), StringComparer.Ordinal)
                .ThenBy(e => e.DisplayNumber ?? int.MaxValue)
                .Select(e => new
                {
                    e.ObjectId,
                    Kind = e.Kind.ToString(),
                    e.Name,
                    e.DisplayNumber,
                    e.DisplayText,
                    e.AssetPath,
                    e.SecondaryAssetPath,
                    Graphic = !string.IsNullOrWhiteSpace(e.AssetPath),
                    e.OnColorHex,
                    e.OffColorHex,
                    e.TextColorHex,
                    Bounds = new { e.X, e.Y, e.Width, e.Height },
                    e.SourceComponentIndex,
                    SourceDecoderType = TryGetComponent(layout, e.SourceComponentIndex)?.GetType().Name,
                    e.SourceElementIndex,
                    e.SharedSourceSetId,
                    e.SharedSourceSetCount,
                    e.SegmentDisplayType,
                    e.ShowDecimalPoint,
                    e.ShowCommaTail,
                    e.TextBoxFontName,
                    e.TextBoxFontStyle,
                    e.TextBoxFontSize,
                    e.IsReversed,
                    e.Stops,
                    e.VisibleScale,
                    e.BandOffset,
                    e.SourceBlend,
                    e.ImportSource,
                    Images = GetImagesForComponent(imagePaths, e.SourceComponentIndex)
                }),
            InputDefinitions = mapResult.InputDefinitions
                .OrderBy(i => i.Name, StringComparer.Ordinal)
                .ThenBy(i => i.ButtonNumber, StringComparer.Ordinal)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    Kind = i.Kind.ToString(),
                    i.ButtonNumber,
                    i.CoinInput,
                    i.Inverted,
                    i.RawMfmeShortcut,
                    i.KeyboardShortcut,
                    i.LinkedVisualElementId,
                    i.MamePortTag,
                    i.MameMask,
                    i.Notes
                }),
            Warnings = mapResult.Warnings
                .OrderBy(w => w.Code, StringComparer.Ordinal)
                .ThenBy(w => w.Message, StringComparer.Ordinal)
                .ThenBy(w => w.Context, StringComparer.Ordinal)
                .ToArray(),
            UnsupportedComponentTypes = mapResult.UnsupportedComponentTypes
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToArray()
        };

        File.WriteAllText(Path.Combine(stagingDirectory, "mapped-elements.json"), JsonSerializer.Serialize(document, JsonOptions), Encoding.UTF8);
    }

    public void WriteReport(FmlImportDiagnosticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(report.StagingDirectory);
        File.WriteAllText(Path.Combine(report.StagingDirectory, "import-diagnostics.txt"), BuildReport(report), Encoding.UTF8);
    }

    private static string BuildReport(FmlImportDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "FML Import Diagnostics");
        AppendLine(sb, "FML filename", Path.GetFileName(report.FmlPath));
        AppendLine(sb, "FML path", report.FmlPath);
        AppendLine(sb, "Import timestamp (UTC)", report.ImportTimestampUtc.ToString("O"));
        AppendLine(sb, "Staging folder", report.StagingDirectory);
        AppendLine(sb, "Elapsed decode time", FormatElapsed(report.DecodeElapsed));
        AppendLine(sb, "Elapsed mapping time", FormatElapsed(report.MappingElapsed));
        AppendLine(sb, "Elapsed asset copy time", FormatElapsed(report.AssetCopyElapsed));
        AppendLine(sb, "Total import time", FormatElapsed(report.TotalElapsed));
        sb.AppendLine();

        AppendSection(sb, "Warnings and Errors");
        AppendList(sb, "Decoder errors", report.DecoderErrors);
        AppendList(sb, "Decoder warnings", report.DecoderWarnings);
        AppendList(sb, "Mapper warnings", report.MapperWarnings.Select(FormatWarning));
        AppendList(sb, "Asset copy warnings", report.AssetCopyWarnings.Select(FormatWarning));
        sb.AppendLine();

        AppendSection(sb, "Summary");
        AppendList(sb, "Unsupported component summary", CountStrings(report.UnsupportedComponentTypes).Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        AppendList(sb, "Component counts by type", report.Layout is null ? [] : CountComponents(report.Layout).Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        AppendLine(sb, "Image count", report.ImagePaths.Count.ToString());
        AppendList(sb, "Image filenames", report.ImagePaths.Values.OrderBy(v => v, StringComparer.Ordinal));
        AppendList(sb, "Panel element counts", CountStrings(report.MapResult?.Elements.Select(e => e.Kind.ToString()) ?? []).Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        AppendLine(sb, "Input definition count", (report.MapResult?.InputDefinitions.Count ?? 0).ToString());
        AppendLine(sb, "Imported asset count", report.ImportedAssetCount.ToString());
        sb.AppendLine();

        AppendSection(sb, "Mapped Elements");
        if (report.MapResult is null)
        {
            sb.AppendLine("(mapping did not complete)");
        }
        else
        {
            foreach (var element in report.MapResult.Elements.OrderBy(e => e.SourceComponentIndex ?? int.MaxValue).ThenBy(e => e.SourceElementIndex ?? int.MaxValue).ThenBy(e => e.Kind.ToString(), StringComparer.Ordinal))
            {
                var component = TryGetComponent(report.Layout, element.SourceComponentIndex);
                sb.AppendLine($"- Component {FormatNullable(element.SourceComponentIndex)} | Decoder type: {component?.GetType().Name ?? "(unknown)"} | Mapped element type: {element.Kind}");
            }
        }
        sb.AppendLine();

        AppendLampColourDiagnostics(sb, report);
        return sb.ToString();
    }

    private static void AppendLampColourDiagnostics(StringBuilder sb, FmlImportDiagnosticsReport report)
    {
        AppendSection(sb, "Lamp Colour Diagnostics");
        if (report.MapResult is null)
        {
            sb.AppendLine("(mapping did not complete)");
            return;
        }

        var lamps = report.MapResult.Elements.Where(e => e.Kind == PanelElementKind.Lamp).OrderBy(e => e.SourceComponentIndex ?? int.MaxValue).ThenBy(e => e.SourceElementIndex ?? int.MaxValue).ToArray();
        if (lamps.Length == 0)
        {
            sb.AppendLine("(none)");
            return;
        }

        foreach (var lamp in lamps)
        {
            var component = TryGetComponent(report.Layout, lamp.SourceComponentIndex);
            sb.AppendLine($"Component {FormatNullable(lamp.SourceComponentIndex)} / Element {FormatNullable(lamp.SourceElementIndex)}");
            AppendLine(sb, "  Display number", lamp.DisplayNumber?.ToString() ?? "(none)");
            AppendLine(sb, "  Display text", lamp.DisplayText ?? "(none)");
            AppendList(sb, "  Decoded Colours dictionary", component?.Colours.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{kvp.Key}: {kvp.Value}") ?? []);
            AppendLine(sb, "  Mapped OnColorHex", lamp.OnColorHex ?? "(none)");
            AppendLine(sb, "  Mapped OffColorHex", lamp.OffColorHex ?? "(none)");
            AppendLine(sb, "  Mapped TextColorHex", lamp.TextColorHex ?? "(none)");
            AppendLine(sb, "  Mapped Graphic", (!string.IsNullOrWhiteSpace(lamp.AssetPath)).ToString());
            AppendLine(sb, "  Mapped AssetPath", lamp.AssetPath ?? "(none)");
            sb.AppendLine();
        }
    }

    private static IReadOnlyList<string> GetImagesForComponent(IReadOnlyDictionary<FmlDecodedImageKey, string> imagePaths, int? componentIndex)
        => componentIndex.HasValue
            ? imagePaths.Where(kvp => kvp.Key.ComponentIndex == componentIndex.Value).OrderBy(kvp => kvp.Key.ImageName, StringComparer.Ordinal).Select(kvp => kvp.Value).ToArray()
            : [];

    private static BaseComponent? TryGetComponent(Layout? layout, int? index)
        => layout is not null && index.HasValue && index.Value >= 0 && index.Value < layout.Components.Count ? layout.Components[index.Value] : null;

    private static IReadOnlyDictionary<string, int> CountComponents(Layout layout)
        => CountStrings(layout.Components.Select(c => c.GetType().Name));

    private static IReadOnlyDictionary<string, int> CountStrings(IEnumerable<string> values)
        => values.GroupBy(v => v, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    private static string FormatWarning(LayoutImportWarning warning)
        => string.IsNullOrWhiteSpace(warning.Context) ? $"{warning.Code}: {warning.Message}" : $"{warning.Code}: {warning.Message} ({warning.Context})";

    private static string FormatElapsed(TimeSpan elapsed) => $"{elapsed.TotalMilliseconds:0.###} ms";
    private static string FormatNullable(int? value) => value?.ToString() ?? "(none)";
    private static void AppendHeader(StringBuilder sb, string value) { sb.AppendLine(value); sb.AppendLine(new string('=', value.Length)); }
    private static void AppendSection(StringBuilder sb, string value) { sb.AppendLine(value); sb.AppendLine(new string('-', value.Length)); }
    private static void AppendLine(StringBuilder sb, string key, string value) => sb.AppendLine($"{key}: {value}");
    private static void AppendList(StringBuilder sb, string key, IEnumerable<string> values)
    {
        sb.AppendLine($"{key}:");
        var ordered = values.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) sb.AppendLine("  (none)");
        foreach (var value in ordered) sb.AppendLine($"  - {value}");
    }
}

internal sealed class FmlImportDiagnosticsReport
{
    public required string FmlPath { get; init; }
    public required DateTimeOffset ImportTimestampUtc { get; init; }
    public required string StagingDirectory { get; init; }
    public Layout? Layout { get; init; }
    public FmlToOasisMapResult? MapResult { get; init; }
    public IReadOnlyDictionary<FmlDecodedImageKey, string> ImagePaths { get; init; } = new Dictionary<FmlDecodedImageKey, string>();
    public IReadOnlyList<string> DecoderErrors { get; init; } = [];
    public IReadOnlyList<string> DecoderWarnings { get; init; } = [];
    public IReadOnlyList<LayoutImportWarning> MapperWarnings { get; init; } = [];
    public IReadOnlyList<LayoutImportWarning> AssetCopyWarnings { get; init; } = [];
    public IReadOnlyList<string> UnsupportedComponentTypes { get; init; } = [];
    public int ImportedAssetCount { get; init; }
    public TimeSpan DecodeElapsed { get; init; }
    public TimeSpan MappingElapsed { get; init; }
    public TimeSpan AssetCopyElapsed { get; init; }
    public TimeSpan TotalElapsed { get; init; }
}
