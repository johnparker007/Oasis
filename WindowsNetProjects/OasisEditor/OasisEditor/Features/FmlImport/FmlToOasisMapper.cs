using System.Diagnostics;
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
        var inputDiagnostics = new List<string>();
        var unsupported = new List<string>();

        for (var index = 0; index < layout.Components.Count; index++)
        {
            var component = layout.Components[index];
            switch (component)
            {
                case Background:
                    elements.Add(MapBackground(component, index, exportedImages));
                    break;
                case Bitmap:
                    elements.Add(MapImage(component, index, exportedImages));
                    break;
                case Frame or Border:
                    AddUnsupportedComponent(component, unsupported, warnings);
                    break;
                case Lamp or PrismLamp or Button or Checkbox:
                    MapLampLike(component, index, exportedImages, elements, inputs, inputDiagnostics, warnings);
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
                    AddUnsupportedComponent(component, unsupported, warnings);
                    break;
            }
        }

        return new FmlToOasisMapResult { Elements = elements, Warnings = warnings, InputDefinitions = inputs, InputDiagnostics = inputDiagnostics, UnsupportedComponentTypes = unsupported };
    }

    private static PanelElementModel MapBackground(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images) => new()
    {
        ObjectId = Guid.NewGuid().ToString("N"), Name = "Background", Kind = PanelElementKind.Background,
        X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height),
        AssetPath = BackgroundAssetPath(c, images, index), IsTransformLocked = true, OnColorHex = Color(c, "Colour") ?? Color(c, "Color") ?? Color(c, "BackgroundColour") ?? Color(c, "BackgroundColor"), SourceComponentIndex = index, ImportSource = Source(c, index)
    };

    private static PanelElementModel MapImage(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images) => new()
    {
        ObjectId = Guid.NewGuid().ToString("N"), Name = "Image", Kind = PanelElementKind.Image,
        X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height),
        AssetPath = FirstImage(images, index), IsTransformLocked = false, SourceComponentIndex = index, ImportSource = Source(c, index)
    };

    private static void AddUnsupportedComponent(BaseComponent component, ICollection<string> unsupported, ICollection<LayoutImportWarning> warnings)
    {
        var componentType = component.GetType().Name;
        unsupported.Add(componentType);
        warnings.Add(new LayoutImportWarning("fml.import.component.unsupported", $"Unsupported FML component '{componentType}' was skipped.", componentType));
    }

    private static void MapLampLike(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images, List<PanelElementModel> elements, List<InputDefinitionModel> inputs, List<string> inputDiagnostics, ICollection<LayoutImportWarning> warnings)
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
                SourceComponentIndex = index, SourceElementIndex = entry.SublampIndex, SharedSourceSetId = sharedSetId, SharedSourceSetCount = entries.Length,
                SourceBlend = Bool(c, "Blend") ?? false, ImportSource = Source(c, index, displayNumber)
            });
        }

        var input = BuildInput(c, elements.LastOrDefault(e => e.SourceComponentIndex == index)?.ObjectId ?? string.Empty);
        if (input is not null)
        {
            inputs.Add(input);
            inputDiagnostics.Add(BuildInputDiagnostic(c, index, input));
        }
        else if (HasInputMetadata(c))
        {
            var shortcutCodes = c.UInt32s
                .Where(pair => pair.Key is "Shortcut 1" or "Shortcut 2")
                .Select(pair => $"{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}");
            warnings.Add(new LayoutImportWarning(
                "fml.import.input.unmapped",
                $"FML component {index.ToString(CultureInfo.InvariantCulture)} ({c.GetType().Name}) contains input metadata but no input could be mapped. UInt32 keys=[{string.Join(", ", c.UInt32s.Keys.OrderBy(key => key, StringComparer.Ordinal))}]; Boolean keys=[{string.Join(", ", c.Booleans.Keys.OrderBy(key => key, StringComparer.Ordinal))}]; shortcut codes=[{string.Join(", ", shortcutCodes)}].",
                index.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string BuildInputDiagnostic(BaseComponent component, int componentIndex, InputDefinitionModel input)
    {
        var sourceField = component.WasValuePresent("Button Number") ? "Button Number"
            : component.WasValuePresent("ButtonNumber") ? "ButtonNumber"
            : "none";
        return $"[MFME Input Decode] component={componentIndex} type={component.GetType().Name} " +
            $"name=\"{EscapeDiagnostic(input.Name)}\" coin={input.CoinInput.ToString().ToLowerInvariant()} " +
            $"selectedButtonNumber={input.ButtonNumber} sourceField=\"{sourceField}\" " +
            $"shortcut=\"{EscapeDiagnostic(input.KeyboardShortcut)}\" " +
            $"int32={FormatDiagnosticDictionary(component.Int32s)} " +
            $"uint32={FormatDiagnosticDictionary(component.UInt32s)} " +
            $"bool={FormatDiagnosticDictionary(component.Booleans)} " +
            $"strings={FormatDiagnosticDictionary(component.Strings, value => $"\"{EscapeDiagnostic(value)}\"")}";
    }

    private static string FormatDiagnosticDictionary<T>(
        IReadOnlyDictionary<string, T> values, Func<T, string>? formatValue = null)
    {
        formatValue ??= value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return "{" + string.Join(", ", values
            .Where(pair => !pair.Key.Contains("image", StringComparison.OrdinalIgnoreCase)
                && !pair.Key.Contains("bitmap", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={formatValue(pair.Value)}")) + "}";
    }

    private static string EscapeDiagnostic(string value)
    {
        const int maximumLength = 200;
        var bounded = value.Length <= maximumLength ? value : value[..maximumLength] + "…";
        return bounded.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static PanelElementModel MapReel(BaseComponent c, int index, IReadOnlyDictionary<FmlDecodedImageKey, string> images, ICollection<LayoutImportWarning> warnings)
    {
        var stops = UInt(c, "Stops") ?? (c as DiscReel)?.Stops ?? (c as FlipReel)?.Stops ?? 0;
        double? scale = stops > 0 ? ((Double(c, "Height") ?? c.Height) / 50d) / stops : null;
        if (stops == 0) warnings.Add(new LayoutImportWarning("fml.import.reel.stops.invalid", "Reel stops must be greater than zero to calculate visible scale.", index.ToString(CultureInfo.InvariantCulture)));
        var lampsEnabled = Bool(c, "LampsEnabled") ?? true;
        var reelLamps = MapReelLamps(c);
        var reelNumber = Number(c).GetValueOrDefault(c.Number);
        var reelName = $"Reel {reelNumber}";
        Debug.WriteLine($"Reel '{reelName}': {FormatMfmeCommonReelLampSlots(c)} -> Oasis [top={FormatLamp(reelLamps[0].LampNumber)}, middle={FormatLamp(reelLamps[1].LampNumber)}, bottom={FormatLamp(reelLamps[2].LampNumber)}], enabled={lampsEnabled.ToString().ToLowerInvariant()}");
        AddReelLampImportWarnings(c, reelName, reelLamps, lampsEnabled, index, warnings);
        return new PanelElementModel { ObjectId = Guid.NewGuid().ToString("N"), Name = reelName, Kind = PanelElementKind.Reel, X = c.X, Y = c.Y, Width = Math.Max(1, c.Width), Height = Math.Max(1, c.Height), DisplayNumber = reelNumber, AssetPath = FirstRoleImage(images, index, IsReelBand) ?? FirstImage(images, index), SecondaryAssetPath = FirstRoleImage(images, index, IsOverlay), Stops = (int)stops, IsReversed = Bool(c, "Reversed") ?? Bool(c, "Reverse") ?? false, VisibleScale = scale, ReelLampsEnabled = lampsEnabled, ReelLamps = reelLamps, IsOpaqueReel = Bool(c, "OpaqueBand") ?? Bool(c, "Opaque") ?? false, SourceComponentIndex = index, ImportSource = Source(c, index) };
    }

    private static IReadOnlyList<ReelLampSlotModel> MapReelLamps(BaseComponent c)
    {
        // Oasis currently imports only the common three-lamp subset of MFME's larger 3-column by 5-row reel-lamp tray.
        // Confirmed common mechanical reels use MFME left-column tray slots 2, 3 and 4 for top, middle and bottom.
        var bySlot = GetSublamps(c)
            .Where(entry => entry.SublampIndex is >= 2 and <= 4)
            .GroupBy(entry => entry.SublampIndex)
            .ToDictionary(group => group.Key, group => NormalizeLampNumber(group.OrderBy(entry => entry.SublampNumber).First().SublampNumber));

        return
        [
            new ReelLampSlotModel { Position = ReelLampSlotPosition.Top, LampNumber = bySlot.GetValueOrDefault(2), LocalVerticalCenter = 1d / 6d, Radius = 0d, Intensity = 1d },
            new ReelLampSlotModel { Position = ReelLampSlotPosition.Middle, LampNumber = bySlot.GetValueOrDefault(3), LocalVerticalCenter = 0.5d, Radius = 0d, Intensity = 1d },
            new ReelLampSlotModel { Position = ReelLampSlotPosition.Bottom, LampNumber = bySlot.GetValueOrDefault(4), LocalVerticalCenter = 5d / 6d, Radius = 0d, Intensity = 1d }
        ];
    }


    private static void AddReelLampImportWarnings(BaseComponent c, string reelName, IReadOnlyList<ReelLampSlotModel> reelLamps, bool lampsEnabled, int index, ICollection<LayoutImportWarning> warnings)
    {
        var sublamps = GetSublamps(c).ToArray();
        if (lampsEnabled && reelLamps.All(lamp => lamp.LampNumber is null))
        {
            warnings.Add(new LayoutImportWarning("fml.import.reel.lamps.common3.missing", $"Reel '{reelName}' has LampsEnabled=true but none of MFME slots 2, 3 or 4 contain assigned lamp numbers.", index.ToString(CultureInfo.InvariantCulture)));
        }

        var undefinedCommonSlots = sublamps.Where(entry => entry.SublampIndex is >= 2 and <= 4 && entry.SublampNumber == UndefinedSublampNumber).Select(entry => entry.SublampIndex).Distinct().OrderBy(slot => slot).ToArray();
        if (undefinedCommonSlots.Length > 0)
        {
            warnings.Add(new LayoutImportWarning("fml.import.reel.lamps.common3.undefined", $"Reel '{reelName}' has undefined MFME reel-lamp values in common slots [{string.Join(",", undefinedCommonSlots)}].", index.ToString(CultureInfo.InvariantCulture)));
        }

        var ignoredSlots = sublamps.Where(entry => entry.SublampIndex is < 2 or > 4 && NormalizeLampNumber(entry.SublampNumber) is not null).Select(entry => entry.SublampIndex).Distinct().OrderBy(slot => slot).ToArray();
        if (ignoredSlots.Length > 0)
        {
            warnings.Add(new LayoutImportWarning("fml.import.reel.lamps.extraIgnored", $"Reel '{reelName}' has MFME reel-lamp tray slots outside the supported common slots 2, 3 and 4; ignored slots [{string.Join(",", ignoredSlots)}].", index.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string FormatMfmeCommonReelLampSlots(BaseComponent c)
    {
        var bySlot = GetSublamps(c).GroupBy(entry => entry.SublampIndex).ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.SublampNumber).First().SublampNumber);
        return $"MFME slots [2={FormatLamp(ResolveCommonSlot(bySlot, 2))}, 3={FormatLamp(ResolveCommonSlot(bySlot, 3))}, 4={FormatLamp(ResolveCommonSlot(bySlot, 4))}]";
    }

    private static int? ResolveCommonSlot(IReadOnlyDictionary<int, int> bySlot, int slot) => bySlot.TryGetValue(slot, out var lampNumber) ? NormalizeLampNumber(lampNumber) : null;

    private static string FormatLamp(int? lampNumber) => lampNumber?.ToString(CultureInfo.InvariantCulture) ?? "blank";

    private static int? NormalizeLampNumber(int lampNumber) => lampNumber >= 0 && lampNumber != UndefinedSublampNumber ? lampNumber : null;

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
    private static int? LabelLampNumber(BaseComponent c) => c is Label label ? label.Lamp : c.Int32s.TryGetValue("Lamp", out var lamp) ? lamp : UInt(c, "Lamp") is uint legacyLamp ? unchecked((int)legacyLamp) : null;
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
    private static InputDefinitionModel? BuildInput(BaseComponent c, string linkedObjectId)
    {
        var buttonNumber = GetExplicitInputButtonNumber(c);
        var coinNote = GetSelectedCoinNote(c);
        var isCoinInput =
            (c.WasValuePresent("Coin / Note Selected") && Bool(c, "Coin / Note Selected") == true)
            || (c.WasValuePresent("SelectedCoinNoteId") && coinNote is not null);
        var shortcut = GetPrimaryShortcut(c);
        var hasEnabledShortcut = HasEnabledShortcut(c);
        if (!buttonNumber.HasValue && !isCoinInput && !hasEnabledShortcut)
        {
            return null;
        }

        var rawShortcut = shortcut?.Text ?? string.Empty;
        var keyboardShortcut = string.Empty;
        if (shortcut is not null && MfmeShortcutKeyMapper.TryMap(rawShortcut, out var mappedKey))
        {
            keyboardShortcut = mappedKey.ToString();
        }

        var name = LampText(c) ?? (isCoinInput ? coinNote ?? "Imported Coin Input" : "Imported Button Input");
        return new InputDefinitionModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Kind = isCoinInput ? InputDefinitionKind.Coin : InputDefinitionKind.Button,
            ButtonNumber = buttonNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            CoinInput = isCoinInput,
            Inverted = Bool(c, "Inverted") ?? false,
            RawMfmeShortcut = rawShortcut,
            KeyboardShortcut = keyboardShortcut,
            LinkedVisualElementId = Guid.TryParse(linkedObjectId, out var id) ? id : null,
            Notes = shortcut is null && hasEnabledShortcut ? FormatUnknownShortcutNotes(c) : string.Empty
        };
    }

    private static uint? GetInputButtonNumber(BaseComponent c)
        => UInt(c, "Button Number") ?? UInt(c, "ButtonNumber");

    private static uint? GetExplicitInputButtonNumber(BaseComponent c)
        => c.WasValuePresent("Button Number") || c.WasValuePresent("ButtonNumber")
            ? GetInputButtonNumber(c)
            : null;

    private static string? GetSelectedCoinNote(BaseComponent c)
    {
        var value = Str(c, "SelectedCoinNote");
        return value is null || value.Equals("none", StringComparison.OrdinalIgnoreCase)
            || value.Equals("(none)", StringComparison.OrdinalIgnoreCase)
            || value.Equals("not selected", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }

    private static MfmeDecodedShortcut? GetPrimaryShortcut(BaseComponent c)
    {
        foreach (var number in new[] { 1, 2 })
        {
            if (Bool(c, $"Shortcut {number} Enabled") != true || UInt(c, $"Shortcut {number}") is not uint code)
            {
                continue;
            }

            if (TryDecodeMfmeShortcut(code, out var text))
            {
                return new MfmeDecodedShortcut(code, text);
            }
        }

        return null;
    }

    // MFME persists shortcuts as Win32 virtual-key codes, not character code points.
    private static bool TryDecodeMfmeShortcut(uint code, out string text)
    {
        text = code switch
        {
            0x08 => "BACKSPACE",
            0x09 => "TAB",
            0x0D => "ENTER",
            0x10 => "SHIFT",
            0x11 => "CTRL",
            0x12 => "ALT",
            0x1B => "ESCAPE",
            0x20 => "SPACE",
            0x25 => "LEFT",
            0x26 => "UP",
            0x27 => "RIGHT",
            0x28 => "DOWN",
            0x30 or >= 0x31 and <= 0x39 => ((char)code).ToString(),
            >= 0x41 and <= 0x5A => ((char)code).ToString(),
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            0xDF => "#",
            _ => string.Empty
        };
        return text.Length > 0;
    }

    private static bool HasEnabledShortcut(BaseComponent c)
        => Enumerable.Range(1, 2).Any(number =>
            c.WasValuePresent($"Shortcut {number} Enabled")
            && Bool(c, $"Shortcut {number} Enabled") == true);

    private static bool HasInputMetadata(BaseComponent c)
        => GetExplicitInputButtonNumber(c).HasValue
            || (c.WasValuePresent("SelectedCoinNoteId") && GetSelectedCoinNote(c) is not null)
            || (c.WasValuePresent("Coin / Note Selected") && Bool(c, "Coin / Note Selected") == true)
            || HasEnabledShortcut(c);

    private static string FormatUnknownShortcutNotes(BaseComponent c)
    {
        var codes = new[] { 1, 2 }
            .Where(number => Bool(c, $"Shortcut {number} Enabled") == true)
            .Select(number => UInt(c, $"Shortcut {number}") is uint code
                ? $"Shortcut {number} code: {code.ToString(CultureInfo.InvariantCulture)}"
                : $"Shortcut {number} code unavailable");
        return string.Join("; ", codes);
    }

    private sealed record MfmeDecodedShortcut(uint Code, string Text);
}

internal sealed class FmlToOasisMapResult
{
    public required IReadOnlyList<PanelElementModel> Elements { get; init; }
    public required IReadOnlyList<LayoutImportWarning> Warnings { get; init; }
    public required IReadOnlyList<InputDefinitionModel> InputDefinitions { get; init; }
    public required IReadOnlyList<string> InputDiagnostics { get; init; }
    public required IReadOnlyList<string> UnsupportedComponentTypes { get; init; }
}
