using System.Globalization;
using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.LayoutImport;

namespace OasisEditor.Features.FmlImport;

internal sealed class FmlToOasisMapper
{
    private const int UndefinedSublampNumber = -2;
    private const string ImportSourceFormat = "FML";
    private const string LampOffImageColourKey = "OffImageColour";
    private const string LampOffImageColorKey = "OffImageColor";
    private const string OffColourKey = "OffColour";
    private const string OffColorKey = "OffColor";

    public FmlToOasisMapResult Map(Layout layout, IReadOnlyDictionary<FmlDecodedImageKey, string> exportedImages)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(exportedImages);

        var elements = new List<PanelElementModel>();
        var warnings = new List<LayoutImportWarning>();
        var inputs = new List<InputDefinitionModel>();
        var unsupported = new List<string>();

        for (var index = 0; index < layout.Components.Count; index++)
        {
            var component = layout.Components[index];
            switch (component)
            {
                case Background or Bitmap or Frame:
                    elements.Add(MapBackground(component, index, exportedImages));
                    break;
                case Lamp or PrismLamp or Button or Checkbox:
                    MapLampLike(component, index, exportedImages, elements, inputs);
                    break;
                case Reel or BandReel or DiscReel or FlipReel:
                    elements.Add(MapReel(component, index, exportedImages, warnings));
                    break;
                case SevenSeg or SevenSegBlock:
                    elements.Add(MapSegment(component, index, exportedImages));
                    break;
                case Alpha or AlphaNew or MatrixAlpha or DotAlpha or BFMAlpha:
                    elements.Add(MapAlpha(component, index, exportedImages));
                    break;
                case Label:
                    elements.Add(MapLabel(component, index));
                    break;
                default:
                    unsupported.Add(component.GetType().Name);
                    warnings.Add(new LayoutImportWarning("fml.import.component.unsupported", $"Unsupported FML component '{component.GetType().Name}' was skipped.", component.GetType().Name));
                    break;
            }
        }

        return new FmlToOasisMapResult { Elements = elements, Warnings = warnings, InputDefinitions = inputs, UnsupportedComponentTypes = unsupported };
    }

    private static PanelElementModel MapBackground(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images) => new()
    {
        ObjectId = Guid.NewGuid().ToString("N"), Name = "Background", Kind = PanelElementKind.Background,
        X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height),
        AssetPath = BackgroundAssetPath(c, images, index), IsTransformLocked = true, OnColorHex = Color(c, "Colour") ?? Color(c, "Color") ?? Color(c, "BackgroundColour") ?? Color(c, "BackgroundColor"), SourceComponentIndex = index, ImportSource = Source(c, index)
    };

    private static void MapLampLike(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images, List<PanelElementModel> elements, List<InputDefinitionModel> inputs)
    {
        var entries = GetSublamps(c).Where(e => e.SublampNumber != UndefinedSublampNumber && e.SublampNumber >= 0).OrderBy(e => e.SublampIndex).ToArray();
        if (entries.Length == 0)
        {
            var fallback = Number(c);
            entries = fallback.HasValue ? [new LampSublampTableEntry(0, fallback.Value)] : [new LampSublampTableEntry(0, -1)];
        }

        var sharedSetId = $"fml-component-{index.ToString(CultureInfo.InvariantCulture)}";
        var noOutline = Bool(c, "NoOutline");
        var hasBorder = noOutline.HasValue ? !noOutline.Value : c is Button;
        foreach (var entry in entries)
        {
            var main = FindLampImage(images, index, entry.SublampIndex, isMask: false) ?? FindLampImage(images, index, entry.SublampIndex, isMask: null) ?? FirstLampImage(images, index, isMask: false);
            var mask = FindLampImage(images, index, entry.SublampIndex, isMask: true) ?? FirstLampImage(images, index, isMask: true);
            var displayNumber = entry.SublampNumber >= 0 ? entry.SublampNumber : (int?)null;
            var font = Font(c);
            elements.Add(new PanelElementModel
            {
                ObjectId = Guid.NewGuid().ToString("N"), Name = displayNumber.HasValue ? $"Lamp {displayNumber.Value}" : "Lamp", Kind = PanelElementKind.Lamp,
                X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height), DisplayNumber = displayNumber,
                AssetPath = main, SecondaryAssetPath = main is null ? null : mask,
                OnColorHex = SublampColor(c, entry.SublampIndex) ?? Color(c, "OnColour") ?? Color(c, "OnColor") ?? Color(c, "Colour") ?? Color(c, "Color"),
                OffColorHex = LampOffColor(c), TextColorHex = TextColor(c),
                DisplayText = LampText(c), HasBorder = hasBorder, TextBoxFontName = font?.FontName ?? "Tahoma", TextBoxFontStyle = FontStyle(font), TextBoxFontSize = font?.FontSize.ToString(CultureInfo.InvariantCulture) ?? "8",
                SourceComponentIndex = index, SourceElementIndex = entry.SublampIndex, SharedSourceSetId = sharedSetId, SharedSourceSetCount = entries.Length, ImportSource = Source(c, index, displayNumber)
            });
        }

        var input = BuildInput(c, elements.LastOrDefault(e => e.SourceComponentIndex == index)?.ObjectId ?? string.Empty);
        if (input is not null) inputs.Add(input);
    }

    private static PanelElementModel MapReel(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images, ICollection<LayoutImportWarning> warnings)
    {
        var stops = UInt(c, "Stops") ?? (c as DiscReel)?.Stops ?? (c as FlipReel)?.Stops ?? 0;
        double? scale = stops > 0 ? ((Double(c, "Height") ?? c.Height) / 50d) / stops : null;
        if (stops == 0) warnings.Add(new LayoutImportWarning("fml.import.reel.stops.invalid", "Reel stops must be greater than zero to calculate visible scale.", index.ToString(CultureInfo.InvariantCulture)));
        return new PanelElementModel { ObjectId = Guid.NewGuid().ToString("N"), Name = $"Reel {Number(c).GetValueOrDefault(c.Number) + 1}", Kind = PanelElementKind.Reel, X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height), DisplayNumber = Number(c).GetValueOrDefault(c.Number) + 1, AssetPath = FirstRoleImage(images, index, IsReelBand) ?? FirstImage(images, index), SecondaryAssetPath = FirstRoleImage(images, index, IsOverlay), Stops = (int)stops, IsReversed = Bool(c, "Reversed") ?? Bool(c, "Reverse") ?? false, VisibleScale = scale, SourceComponentIndex = index, ImportSource = Source(c, index) };
    }

    private static PanelElementModel MapSegment(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images) => new() { ObjectId = Guid.NewGuid().ToString("N"), Name = $"7 Segment {Number(c).GetValueOrDefault(c.Number)}", Kind = PanelElementKind.SevenSegment, X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height), DisplayNumber = Number(c).GetValueOrDefault(c.Number), SecondaryAssetPath = FirstRoleImage(images, index, IsOverlay), OnColorHex = Color(c, "OnColour") ?? Color(c, "OnColor"), SourceComponentIndex = index, ImportSource = Source(c, index) };
    private static PanelElementModel MapAlpha(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images) => new() { ObjectId = Guid.NewGuid().ToString("N"), Name = "Alpha", Kind = PanelElementKind.Alpha, X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height), IsReversed = Bool(c, "Reversed") ?? false, SecondaryAssetPath = FirstRoleImage(images, index, IsOverlay), OnColorHex = Color(c, "OnColour") ?? Color(c, "OnColor"), SourceComponentIndex = index, ImportSource = Source(c, index) };
    private static PanelElementModel MapLabel(BaseComponent c, int index)
    {
        var font = Font(c);
        return new PanelElementModel
        {
            ObjectId = Guid.NewGuid().ToString("N"), Name = "Label", Kind = PanelElementKind.Label,
            X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height),
            DisplayText = LabelText(c), LampNumber = LabelLampNumber(c),
            TextBoxFontName = font?.FontName, TextBoxFontStyle = FontStyle(font), TextBoxFontSize = font?.FontSize.ToString(CultureInfo.InvariantCulture),
            TextColorHex = TextColor(c), SourceComponentIndex = index, ImportSource = Source(c, index)
        };
    }

    private static IReadOnlyList<LampSublampTableEntry> GetSublamps(BaseComponent c) => c switch { Lamp l => l.SublampTable, Button b => b.SublampTable, Reel r => r.SublampTable, DiscReel d => d.SublampTable, PrismLamp p => Build(p.SubLamp1Number, p.SubLamp2Number), FlipReel f => Build(f.SubLamp1Number, f.SubLamp2Number, f.SubLamp3Number), _ => [] };
    private static LampSublampTableEntry[] Build(params uint[] values) => values.Select((v, i) => new LampSublampTableEntry(i + 1, unchecked((int)v))).ToArray();
    private static string? LampText(BaseComponent c) => Str(c, "OffText") ?? Str(c, "On1Text") ?? Str(c, "On2Text") ?? Str(c, "On3Text") ?? ButtonLabelText(c) ?? Text(c);
    private static string? ButtonLabelText(BaseComponent c) => Str(c, "Label") ?? Str(c, "Label (UTF-16)");
    private static string? LabelText(BaseComponent c) => Str(c, "Label") ?? Str(c, "Label (UTF-16)") ?? Text(c);
    private static int? LabelLampNumber(BaseComponent c) => UInt(c, "Lamp") is uint lamp ? unchecked((int)lamp) : null;
    private static string? Text(BaseComponent c) => Str(c, "Text") ?? Str(c, "Caption") ?? Str(c, "TextBoxText");
    private static FontTagEntry? Font(BaseComponent c) => c.Fonts.Values.FirstOrDefault(f => f.Role.Contains("off", StringComparison.OrdinalIgnoreCase)) ?? c.Fonts.Values.FirstOrDefault();
    private static string? FontStyle(FontTagEntry? font) => font is null ? null : font.FontStyle == 1 ? "Bold" : "Regular";
    private static string? TextColor(BaseComponent c) => ConvertDecoderRgbaToOasisArgb(Font(c)?.TextColour) ?? Color(c, "TextColour") ?? Color(c, "TextColor") ?? Color(c, "Colour") ?? Color(c, "Color");
    private static string? LampOffColor(BaseComponent c) => FirstPresentColor(c, LampOffImageColourKey, LampOffImageColorKey, OffColourKey, OffColorKey);
    private static string? SublampColor(BaseComponent c, int i)
    {
        var oneBasedIndex = Math.Max(1, i);
        return Color(c, $"Sublamp{oneBasedIndex}Colour")
            ?? Color(c, $"Sublamp{oneBasedIndex}Color")
            ?? Color(c, $"On{oneBasedIndex}Colour")
            ?? Color(c, $"On{oneBasedIndex}Color")
            ?? (i == 0 ? Color(c, "Sublamp0Colour") ?? Color(c, "Sublamp0Color") : null);
    }
    private static string? Color(BaseComponent c, string key) => c.Colours.TryGetValue(key, out var value) ? ConvertDecoderRgbaToOasisArgb(value) : null;
    private static string? FirstPresentColor(BaseComponent c, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (c.Colours.TryGetValue(key, out var value))
            {
                return ConvertDecoderRgbaToOasisArgb(value);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts FML decoder color strings (#RRGGBB or #RRGGBBAA) to Oasis/WPF-compatible #AARRGGBB.
    /// </summary>
    internal static string? ConvertDecoderRgbaToOasisArgb(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hex = value.Trim();
        if (!hex.StartsWith('#')) return null;
        hex = hex[1..];
        if (hex.Length != 6 && hex.Length != 8) return null;
        if (!hex.All(static c => Uri.IsHexDigit(c))) return null;
        hex = hex.ToUpperInvariant();
        return hex.Length == 6 ? $"#FF{hex}" : $"#{hex[6..8]}{hex[..6]}";
    }
    private static string? Str(BaseComponent c, string key) => c.Strings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    private static uint? UInt(BaseComponent c, string key) => c.UInt32s.TryGetValue(key, out var v) ? v : null;
    private static double? Double(BaseComponent c, string key) => c.Floats.TryGetValue(key, out var f) ? f : c.UInt32s.TryGetValue(key, out var u) ? u : null;
    private static bool? Bool(BaseComponent c, string key) => c.Booleans.TryGetValue(key, out var v) ? v : null;
    private static int? Number(BaseComponent c) => c.Int32s.TryGetValue("Number", out var n) ? n : c.UInt32s.TryGetValue("Number", out var u) ? (int)u : c.Number != 0 ? c.Number : null;
    private static string? BackgroundAssetPath(BaseComponent c, IReadOnlyDictionary<FmlDecodedImageKey, string> images, int index) => c is Background ? FirstRoleImage(images, index, IsMainBackgroundImage) : FirstImage(images, index);
    private static string? FirstImage(IReadOnlyDictionary<FmlDecodedImageKey, string> images, int index) => images.Where(k => k.Key.ComponentIndex == index).OrderBy(k => k.Key.ImageName, StringComparer.Ordinal).Select(k => k.Value).FirstOrDefault();
    private static string? FirstRoleImage(IReadOnlyDictionary<FmlDecodedImageKey, string> images, int index, Func<string, bool> role) => images.Where(k => k.Key.ComponentIndex == index && role(k.Key.ImageName)).OrderBy(k => k.Key.ImageName, StringComparer.Ordinal).Select(k => k.Value).FirstOrDefault();
    private static string? FindLampImage(IReadOnlyDictionary<FmlDecodedImageKey, string> images, int index, int sub, bool? isMask) => images.Where(k => k.Key.ComponentIndex == index && IsSublamp(k.Key.ImageName, sub) && MatchesMask(k.Key.ImageName, isMask)).OrderBy(k => k.Key.ImageName, StringComparer.Ordinal).Select(k => k.Value).FirstOrDefault();
    private static string? FirstLampImage(IReadOnlyDictionary<FmlDecodedImageKey, string> images, int index, bool? isMask) => images.Where(k => k.Key.ComponentIndex == index && MatchesMask(k.Key.ImageName, isMask)).OrderBy(k => k.Key.ImageName, StringComparer.Ordinal).Select(k => k.Value).FirstOrDefault();
    private static bool IsSublamp(string key, int sub) { var n = Norm(key); return n.StartsWith($"sublamp_{sub}_", StringComparison.Ordinal) || key.StartsWith($"Sublamp {sub} ", StringComparison.OrdinalIgnoreCase); }
    private static bool MatchesMask(string key, bool? mask) => mask is null || Norm(key).Contains("mask", StringComparison.Ordinal) == mask.Value;
    private static bool IsReelBand(string k) { var n = Norm(k); return n.Contains("band") || n.Contains("gradient") || n.Contains("strip") || n.Contains("reel_image") || n.EndsWith("reel"); }
    private static bool IsOverlay(string k) { var n = Norm(k); return n.Contains("overlay") || n.Contains("over_lay") || n.Contains("window") || n.Contains("cutout") || n.Contains("cut_out") || n.Contains("mask_overlay"); }
    private static bool IsMainBackgroundImage(string k) { var n = Norm(k); return n.Contains("background") && !n.Contains("mask") && !IsOverlay(k); }
    private static string Norm(string s) { var n = new string(s.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray()); while (n.Contains("__", StringComparison.Ordinal)) n = n.Replace("__", "_", StringComparison.Ordinal); return n.Trim('_'); }
    private static PanelElementImportSourceModel Source(BaseComponent c, int index, int? n = null) => new() { Format = ImportSourceFormat, Reference = n.HasValue ? $"{c.GetType().Name}:{index}:{n.Value}" : $"{c.GetType().Name}:{index}" };
    private static InputDefinitionModel? BuildInput(BaseComponent c, string linkedObjectId) { var button = Str(c, "ButtonNumber") ?? Str(c, "Button"); var has = button is not null || Bool(c, "HasButtonInput") == true || Bool(c, "HasCoinInput") == true; if (!has) return null; var raw = Str(c, "Shortcut1") ?? string.Empty; MfmeShortcutKeyMapper.TryMap(raw, out var mapped); return new InputDefinitionModel { Id = Guid.NewGuid().ToString("N"), Name = Text(c) ?? "Imported Input", Kind = Bool(c, "HasCoinInput") == true ? InputDefinitionKind.Coin : InputDefinitionKind.Button, ButtonNumber = button ?? string.Empty, CoinInput = Bool(c, "HasCoinInput") == true, Inverted = Bool(c, "Inverted") ?? false, RawMfmeShortcut = raw, KeyboardShortcut = mapped.ToString(), LinkedVisualElementId = Guid.TryParse(linkedObjectId, out var id) ? id : null, Notes = string.Empty }; }
}

internal sealed class FmlToOasisMapResult
{
    public required IReadOnlyList<PanelElementModel> Elements { get; init; }
    public required IReadOnlyList<LayoutImportWarning> Warnings { get; init; }
    public required IReadOnlyList<InputDefinitionModel> InputDefinitions { get; init; }
    public required IReadOnlyList<string> UnsupportedComponentTypes { get; init; }
}
