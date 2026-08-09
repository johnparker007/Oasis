using MfmeFmlDecoder.src.Model;
using MfmeFmlDecoder.src.Model.Component;
using OasisEditor.Features.FmlImport;
using System.Windows.Media;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlToOasisMapperTests
{
    [Fact]
    public void Map_WithFourMfmeReels_PreservesZeroBasedMachineIdentifiers()
    {
        var reels = Enumerable.Range(0, 4)
            .Select(number => new BandReel { Number = number, Width = 30, Height = 100 })
            .ToArray();

        var result = new FmlToOasisMapper().Map(new Layout(reels), new Dictionary<FmlDecodedImageKey, string>());
        var mappedReels = result.Elements.Where(element => element.Kind == PanelElementKind.Reel).ToArray();

        Assert.Equal(["Reel 0", "Reel 1", "Reel 2", "Reel 3"], mappedReels.Select(element => element.Name));
        Assert.Equal([0, 1, 2, 3], mappedReels.Select(element => element.DisplayNumber.GetValueOrDefault()));
        Assert.DoesNotContain(mappedReels, element => element.Name == "Reel 4");
    }

    [Fact]
    public void Map_WithTextOnlyLamp_PreservesNumberTextColourAndNoAssets()
    {
        var lamp = new Lamp
        {
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 42)]
        };
        lamp.Strings["OffText"] = "HOLD";
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#204060FF";
        lamp.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Arial", 12, 0, "Western", "#0080C0FF", 0);

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal(42, element.DisplayNumber);
        Assert.Equal("HOLD", element.DisplayText);
        Assert.Equal("#FF0000FF", element.OnColorHex);
        Assert.Equal("#FF204060", element.OffColorHex);
        Assert.Equal("#FF0080C0", element.TextColorHex);
        AssertOasisArgbChannels(element.OnColorHex, 255, 0, 0, 255);
        AssertOasisArgbChannels(element.OffColorHex, 255, 32, 64, 96);
        AssertOasisArgbChannels(element.TextColorHex, 255, 0, 128, 192);
        Assert.Null(element.AssetPath);
        Assert.Null(element.SecondaryAssetPath);
        Assert.Equal("Arial", element.TextBoxFontName);
        Assert.Equal("12", element.TextBoxFontSize);
    }

    [Fact]
    public void Map_WithMultiSublampLamp_PreservesNumberColourPairingAndSharedSourceGroup()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 3,
            Height = 4,
            SublampTable =
            [
                new LampSublampTableEntry(1, 10),
                new LampSublampTableEntry(2, 11),
                new LampSublampTableEntry(3, -2)
            ]
        };
        lamp.Colours["Sublamp1Colour"] = "#102030FF";
        lamp.Colours["Sublamp2Colour"] = "#40506080";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.Equal(10, result.Elements[0].DisplayNumber);
        Assert.Equal("#FF102030", result.Elements[0].OnColorHex);
        Assert.Equal(11, result.Elements[1].DisplayNumber);
        Assert.Equal("#80405060", result.Elements[1].OnColorHex);
        Assert.Equal(result.Elements[0].SharedSourceSetId, result.Elements[1].SharedSourceSetId);
        Assert.All(result.Elements, element => Assert.Equal(2, element.SharedSourceSetCount));
    }

    [Fact]
    public void Map_WithMultiSublampMasks_InheritsFirstMainImageAndPreservesIndividualMasks()
    {
        var lamp = new Lamp
        {
            Width = 30,
            Height = 40,
            SublampTable =
            [
                new LampSublampTableEntry(1, 10),
                new LampSublampTableEntry(2, 11),
                new LampSublampTableEntry(3, 12)
            ]
        };
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Sublamp 1 Main")] = "lamps/shared-main.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 1 Mask")] = "lamps/mask-1.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 2 Mask")] = "lamps/mask-2.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 3 Mask")] = "lamps/mask-3.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), images);

        Assert.Equal([10, 11, 12], result.Elements.Select(element => element.DisplayNumber));
        Assert.All(result.Elements, element => Assert.Equal("lamps/shared-main.bmp", element.AssetPath));
        Assert.Equal(
            ["lamps/mask-1.bmp", "lamps/mask-2.bmp", "lamps/mask-3.bmp"],
            result.Elements.Select(element => element.SecondaryAssetPath));
    }

    [Fact]
    public void Map_WithSublampSpecificMainImage_PrefersItOverInheritedFirstMainImage()
    {
        var lamp = new Lamp
        {
            Width = 30,
            Height = 40,
            SublampTable =
            [
                new LampSublampTableEntry(1, 10),
                new LampSublampTableEntry(2, 11)
            ]
        };
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Sublamp 1 Main")] = "lamps/main-1.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 1 Mask")] = "lamps/mask-1.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 2 Main")] = "lamps/main-2.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 2 Mask")] = "lamps/mask-2.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), images);

        Assert.Equal(["lamps/main-1.bmp", "lamps/main-2.bmp"], result.Elements.Select(element => element.AssetPath));
        Assert.Equal(["lamps/mask-1.bmp", "lamps/mask-2.bmp"], result.Elements.Select(element => element.SecondaryAssetPath));
    }

    [Fact]
    public void Map_WithCoreComponentTypes_PreservesDirectMappingBehavior()
    {
        var background = new Background { X = 1, Y = 2, Width = 300, Height = 200 };
        var reel = new BandReel { X = 10, Y = 20, Width = 30, Height = 100, View = 2 };
        reel.UInt32s["Stops"] = 20;
        reel.Booleans["Reverse"] = true;
        var sevenSeg = new SevenSeg { X = 3, Y = 4, Width = 50, Height = 12, Number = 7 };
        sevenSeg.Colours["OnColour"] = "#AA0001FF";
        var alpha = new Alpha { X = 5, Y = 6, Width = 70, Height = 20 };
        alpha.Colours["OnColour"] = "#00AA02FF";
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Caption"] = "HELLO";
        label.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Tahoma", 9, 0, "Western", "#010203FF", 0);

        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Background")] = "background/bg.bmp",
            [new FmlDecodedImageKey(1, "Reel Band")] = "reels/band.bmp",
            [new FmlDecodedImageKey(1, "Window Overlay")] = "reels/overlay.bmp",
            [new FmlDecodedImageKey(2, "Window Overlay")] = "reels/seg-overlay.bmp",
            [new FmlDecodedImageKey(3, "Window Overlay")] = "reels/alpha-overlay.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([background, reel, sevenSeg, alpha, label]), images);

        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Background && e.AssetPath == "background/bg.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Reel && e.AssetPath == "reels/band.bmp" && e.SecondaryAssetPath == "reels/overlay.bmp" && e.IsReversed == true && e.VisibleScale == 0.1d);
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.SevenSegment && e.DisplayNumber == 7 && e.OnColorHex == "#FFAA0001" && e.SecondaryAssetPath == "reels/seg-overlay.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Alpha && e.OnColorHex == "#FF00AA02" && e.SecondaryAssetPath == "reels/alpha-overlay.bmp");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Label && e.DisplayText == "HELLO" && e.TextBoxFontName == "Tahoma");
    }



    [Fact]
    public void Bitmap_MapsToImageNotBackground()
    {
        var bitmap = new Bitmap { X = 11, Y = 22, Width = 33, Height = 44 };
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Bitmap")] = "images/bitmap.png"
        };

        var result = new FmlToOasisMapper().Map(new Layout([bitmap]), images);

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Image, element.Kind);
        Assert.Equal("Image", element.Name);
        Assert.Equal("images/bitmap.png", element.AssetPath);
        Assert.Equal(11, element.X);
        Assert.Equal(22, element.Y);
        Assert.Equal(33, element.Width);
        Assert.Equal(44, element.Height);
        Assert.False(element.IsTransformLocked);
        Assert.Equal(0, element.SourceComponentIndex);
        Assert.DoesNotContain(result.Elements, e => e.Kind == PanelElementKind.Background);
    }

    [Fact]
    public void OnlyMfmeBackground_MapsToOasisBackground()
    {
        var background = new Background { X = 0, Y = 0, Width = 100, Height = 100 };
        var firstBitmap = new Bitmap { X = 1, Y = 2, Width = 3, Height = 4 };
        var secondBitmap = new Bitmap { X = 5, Y = 6, Width = 7, Height = 8 };
        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Background")] = "images/background.png",
            [new FmlDecodedImageKey(1, "Bitmap")] = "images/first.png",
            [new FmlDecodedImageKey(2, "Bitmap")] = "images/second.png"
        };

        var result = new FmlToOasisMapper().Map(new Layout([background, firstBitmap, secondBitmap]), images);

        Assert.Single(result.Elements.Where(e => e.Kind == PanelElementKind.Background));
        Assert.Equal(2, result.Elements.Count(e => e.Kind == PanelElementKind.Image));
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Image && e.AssetPath == "images/first.png");
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Image && e.AssetPath == "images/second.png");
    }

    [Fact]
    public void MfmeBackground_ImageOffsetsDoNotChangeMappedElementGeometry()
    {
        var background = new Background { X = 12, Y = 34, Width = 800, Height = 600 };
        background.Int32s["OffsetX"] = -50;
        background.Int32s["OffsetY"] = 25;

        var result = new FmlToOasisMapper().Map(new Layout([background]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal((12d, 34d, 800d, 600d), (element.X, element.Y, element.Width, element.Height));
        Assert.Equal(-50, element.SourceImageOffsetX);
        Assert.Equal(25, element.SourceImageOffsetY);
    }

    [Fact]
    public void Frame_IsReportedAsUnsupported()
    {
        var frame = new Frame { X = 1, Y = 2, Width = 3, Height = 4 };

        var result = new FmlToOasisMapper().Map(new Layout([frame]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Empty(result.Elements);
        Assert.Contains(nameof(Frame), result.UnsupportedComponentTypes);
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.component.unsupported" && warning.Context == nameof(Frame));
        Assert.DoesNotContain(result.Elements, e => e.Kind is PanelElementKind.Background or PanelElementKind.Image);
    }

    [Fact]
    public void Border_IsReportedAsUnsupported()
    {
        var border = new Border { X = 1, Y = 2, Width = 3, Height = 4 };

        var result = new FmlToOasisMapper().Map(new Layout([border]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Empty(result.Elements);
        Assert.Contains(nameof(Border), result.UnsupportedComponentTypes);
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.component.unsupported" && warning.Context == nameof(Border));
        Assert.DoesNotContain(result.Elements, e => e.Kind is PanelElementKind.Background or PanelElementKind.Image);
    }

    [Fact]
    public void Map_WithDecodedMfmeLabel_UsesLabelKeyFontTextColourAndLampNumber()
    {
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Label"] = "COLLECT";
        label.Lamp = 0;
        label.Fonts["Primary"] = new FontTagEntry(0, "Primary", "Tahoma", 9, 0, "Western", "#010203FF", 1);

        var result = new FmlToOasisMapper().Map(new Layout([label]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Label, element.Kind);
        Assert.Equal("COLLECT", element.DisplayText);
        Assert.Equal(0, element.LampNumber);
        Assert.Equal("Tahoma", element.TextBoxFontName);
        Assert.Equal("Bold", element.TextBoxFontStyle);
        Assert.Equal("9", element.TextBoxFontSize);
        Assert.Equal("#FF010203", element.TextColorHex);
    }

    [Fact]
    public void Map_WithStaticMfmeLabel_LeavesLampNumberNullAndKeepsCompatibilityAliases()
    {
        var label = new Label { X = 7, Y = 8, Width = 90, Height = 24 };
        label.Strings["Label (UTF-16)"] = "NUDGE";

        var result = new FmlToOasisMapper().Map(new Layout([label]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Label, element.Kind);
        Assert.Equal("NUDGE", element.DisplayText);
        Assert.Null(element.LampNumber);
    }
    [Fact]
    public void Map_WithGraphicalLampAndButton_PreservesAssetsAndInputDefinition()
    {
        var lamp = new Lamp { X = 1, Y = 2, Width = 30, Height = 40, SublampTable = [new LampSublampTableEntry(1, 9)] };
        lamp.Colours["Sublamp1Colour"] = "#112233FF";
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        SetExplicitUInt(button, "Button Number", 1);
        button.Strings["Label"] = "START";

        var images = new Dictionary<FmlDecodedImageKey, string>
        {
            [new FmlDecodedImageKey(0, "Sublamp 1 Main")] = "lamps/lamp.bmp",
            [new FmlDecodedImageKey(0, "Sublamp 1 Mask")] = "lamps/lamp-mask.bmp"
        };

        var result = new FmlToOasisMapper().Map(new Layout([lamp, button]), images);

        var graphicalLamp = Assert.Single(result.Elements.Where(e => e.DisplayNumber == 9));
        Assert.Equal("lamps/lamp.bmp", graphicalLamp.AssetPath);
        Assert.Equal("lamps/lamp-mask.bmp", graphicalLamp.SecondaryAssetPath);
        Assert.Equal("#FF112233", graphicalLamp.OnColorHex);
        Assert.Contains(result.Elements, e => e.Kind == PanelElementKind.Lamp && e.DisplayNumber == 12 && e.DisplayText == "START");
        Assert.Single(result.InputDefinitions);
    }

    [Fact]
    public void Map_WithButtonLabel_MapsToLampDisplayTextAndInputDefinition()
    {
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        SetExplicitUInt(button, "Button Number", 1);
        button.Strings["Label"] = "START";

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal(12, element.DisplayNumber);
        Assert.Equal("START", element.DisplayText);
        var input = Assert.Single(result.InputDefinitions);
        Assert.Equal("1", input.ButtonNumber);
        Assert.Equal(element.ObjectId, input.LinkedVisualElementId?.ToString("N"));
    }

    [Fact]
    public void Map_WithDecodedButtonUtf16Label_MapsToLampDisplayText()
    {
        var button = new Button { X = 5, Y = 6, Width = 20, Height = 20, SublampTable = [new LampSublampTableEntry(1, 12)] };
        SetExplicitUInt(button, "Button Number", 1);
        button.Strings["Label (UTF-16)"] = "START";

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal("START", element.DisplayText);
        Assert.Single(result.InputDefinitions);
    }

    [Fact]
    public void Map_WithLampSpecificTextAndLabel_PrefersLampSpecificText()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Strings["OffText"] = "HOLD";
        lamp.Strings["Label"] = "START";
        lamp.Strings["Label (UTF-16)"] = "COLLECT";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(PanelElementKind.Lamp, element.Kind);
        Assert.Equal("HOLD", element.DisplayText);
    }

    [Fact]
    public void Map_WithCurrentButtonInputFields_PreservesZeroShortcutInversionAndVisualLink()
    {
        var button = CreateButton();
        button.Strings["Label"] = "START";
        SetExplicitUInt(button, "Button Number", 0);
        SetExplicitBool(button, "Shortcut 1 Enabled", true);
        SetExplicitUInt(button, "Shortcut 1", 0x20);
        button.Booleans["Inverted"] = true;

        var result = Map(button);

        var element = Assert.Single(result.Elements);
        var input = Assert.Single(result.InputDefinitions);
        Assert.Equal("START", input.Name);
        Assert.Equal("0", input.ButtonNumber);
        Assert.Equal("SPACE", input.RawMfmeShortcut);
        Assert.Equal("Space", input.KeyboardShortcut);
        Assert.True(input.Inverted);
        Assert.Equal(element.ObjectId, input.LinkedVisualElementId?.ToString("N"));
    }

    [Fact]
    public void Map_WithCurrentLampInputFields_UsesSecondEnabledShortcut()
    {
        var lamp = CreateLamp();
        SetExplicitUInt(lamp, "ButtonNumber", 2);
        SetExplicitBool(lamp, "Shortcut 1 Enabled", false);
        SetExplicitUInt(lamp, "Shortcut 1", 0x41);
        SetExplicitBool(lamp, "Shortcut 2 Enabled", true);
        SetExplicitUInt(lamp, "Shortcut 2", 0x31);

        var input = Assert.Single(Map(lamp).InputDefinitions);

        Assert.Equal("2", input.ButtonNumber);
        Assert.Equal("1", input.RawMfmeShortcut);
        Assert.Equal("D1", input.KeyboardShortcut);
    }

    [Fact]
    public void Map_WithUnknownPrimaryShortcut_FallsBackToValidSecondaryShortcut()
    {
        var button = CreateButton();
        SetExplicitUInt(button, "Button Number", 3);
        SetExplicitBool(button, "Shortcut 1 Enabled", true);
        SetExplicitUInt(button, "Shortcut 1", uint.MaxValue);
        SetExplicitBool(button, "Shortcut 2 Enabled", true);
        SetExplicitUInt(button, "Shortcut 2", 0x25);

        var input = Assert.Single(Map(button).InputDefinitions);

        Assert.Equal("LEFT", input.RawMfmeShortcut);
        Assert.Equal("Left", input.KeyboardShortcut);
    }

    [Fact]
    public void Map_WithNoOrUnknownShortcut_StillCreatesInputRows()
    {
        var noShortcut = CreateButton();
        SetExplicitUInt(noShortcut, "Button Number", 4);
        var unknownShortcut = CreateButton();
        SetExplicitUInt(unknownShortcut, "Button Number", 5);
        SetExplicitBool(unknownShortcut, "Shortcut 1 Enabled", true);
        SetExplicitUInt(unknownShortcut, "Shortcut 1", uint.MaxValue);

        var result = new FmlToOasisMapper().Map(new Layout([noShortcut, unknownShortcut]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.InputDefinitions.Count);
        Assert.All(result.InputDefinitions, input => Assert.Equal(string.Empty, input.KeyboardShortcut));
        Assert.Contains(result.InputDefinitions, input => input.ButtonNumber == "5" && input.Notes.Contains(uint.MaxValue.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Map_WithCoinMarker_CreatesUnresolvedCoinWithoutGuessingFromSwitch()
    {
        var lamp = CreateLamp();
        SetExplicitUInt(lamp, "ButtonNumber", 6);
        SetExplicitBool(lamp, "Coin / Note Selected", true);
        lamp.Strings["SelectedCoinNote"] = "£1 Coin";
        lamp.PresentValueKeys.Add("SelectedCoinNoteId");

        var input = Assert.Single(Map(lamp).InputDefinitions);

        Assert.Equal(InputDefinitionKind.Coin, input.Kind);
        Assert.True(input.CoinInput);
        Assert.Equal(string.Empty, input.ButtonNumber);
        Assert.Null(input.CoinChannel);
        Assert.Null(input.CoinValue);
        Assert.Contains("without a resolved Amber coin channel or denomination", input.Notes);
        Assert.Equal("£1 Coin", input.Name);
    }

    [Fact]
    public void Map_WithPlaceholderCoinNote_DoesNotCreateInput()
    {
        var lamp = CreateLamp();
        lamp.Strings["SelectedCoinNote"] = "(none)";

        Assert.Empty(Map(lamp).InputDefinitions);
    }

    [Fact]
    public void Map_WithMultiSublampInput_CreatesOneInputLinkedToLastVisual()
    {
        var lamp = new Lamp
        {
            Width = 20,
            Height = 20,
            SublampTable = [new LampSublampTableEntry(1, 7), new LampSublampTableEntry(2, 8)]
        };
        SetExplicitUInt(lamp, "ButtonNumber", 7);

        var result = Map(lamp);

        Assert.Equal(2, result.Elements.Count);
        var input = Assert.Single(result.InputDefinitions);
        Assert.Equal(result.Elements[1].ObjectId, input.LinkedVisualElementId?.ToString("N"));
    }

    [Fact]
    public void Map_WithoutCurrentInputMetadata_DoesNotCreateInput()
    {
        Assert.Empty(Map(CreateButton()).InputDefinitions);
    }

    [Fact]
    public void Map_WithDefaultedLampInputFields_DoesNotCreateInput()
    {
        var lamp = CreateLamp();
        lamp.UInt32s["ButtonNumber"] = 0;
        lamp.UInt32s["Shortcut 1"] = 0;
        lamp.UInt32s["Shortcut 2"] = 0;
        lamp.Booleans["Shortcut 1 Enabled"] = false;
        lamp.Booleans["Shortcut 2 Enabled"] = false;
        lamp.Booleans["Coin / Note Selected"] = false;

        Assert.Empty(Map(lamp).InputDefinitions);
    }

    [Fact]
    public void Map_WithExplicitZeroLampButtonNumber_CreatesInputWithZero()
    {
        var lamp = CreateLamp();
        lamp.UInt32s["ButtonNumber"] = 0; // Decoder value alone is not source presence.
        lamp.PresentValueKeys.Add("ButtonNumber");

        var input = Assert.Single(Map(lamp).InputDefinitions);

        Assert.Equal("0", input.ButtonNumber);
    }

    [Fact]
    public void Map_WithEnabledLampShortcutAndNoButtonNumber_CreatesInput()
    {
        var lamp = CreateLamp();
        SetExplicitBool(lamp, "Shortcut 1 Enabled", true);
        SetExplicitUInt(lamp, "Shortcut 1", 0x41);

        var input = Assert.Single(Map(lamp).InputDefinitions);

        Assert.Equal(string.Empty, input.ButtonNumber);
        Assert.Equal("A", input.KeyboardShortcut);
    }

    [Fact]
    public void Map_WithMultiSublampDefaultedLamp_CreatesNoInput()
    {
        var lamp = CreateLamp();
        lamp.SublampTable = [new LampSublampTableEntry(1, 7), new LampSublampTableEntry(2, 8)];
        lamp.UInt32s["ButtonNumber"] = 0;

        var result = Map(lamp);

        Assert.Equal(2, result.Elements.Count);
        Assert.Empty(result.InputDefinitions);
    }

    private static void SetExplicitUInt(BaseComponent component, string key, uint value)
    {
        component.UInt32s[key] = value;
        component.PresentValueKeys.Add(key);
    }

    private static void SetExplicitBool(BaseComponent component, string key, bool value)
    {
        component.Booleans[key] = value;
        component.PresentValueKeys.Add(key);
    }

    private static Button CreateButton() => new()
    {
        Width = 20,
        Height = 20,
        SublampTable = [new LampSublampTableEntry(1, 1)]
    };

    private static Lamp CreateLamp() => new()
    {
        Width = 20,
        Height = 20,
        SublampTable = [new LampSublampTableEntry(1, 1)]
    };

    private static FmlToOasisMapResult Map(BaseComponent component)
        => new FmlToOasisMapper().Map(new Layout([component]), new Dictionary<FmlDecodedImageKey, string>());


    [Theory]
    [InlineData("OffImageColour", "#204060FF", "#FF204060", 255, 32, 64, 96)]
    [InlineData("OffImageColor", "#20406080", "#80204060", 128, 32, 64, 96)]
    public void Map_WithLampOffImageColorAliases_MapsOffColor(string key, string decoderValue, string expected, byte a, byte r, byte g, byte b)
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours[key] = decoderValue;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal(expected, element.OffColorHex);
        AssertOasisArgbChannels(element.OffColorHex, a, r, g, b);
    }

    [Fact]
    public void Map_WithLampOffImageColour_PrefersDecoderKeyOverLegacyAliases()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#204060FF";
        lamp.Colours["OffColour"] = "#010203FF";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Equal("#FF204060", element.OffColorHex);
    }

    [Fact]
    public void Map_WithMalformedLampOffImageColour_LeavesOffColorUnset()
    {
        var lamp = new Lamp
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = 40,
            SublampTable = [new LampSublampTableEntry(1, 9)]
        };
        lamp.Colours["Sublamp1Colour"] = "#0000FFFF";
        lamp.Colours["OffImageColour"] = "#20406GFF";
        lamp.Colours["OffColour"] = "#010203FF";

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        var element = Assert.Single(result.Elements);
        Assert.Null(element.OffColorHex);
    }

    [Fact]
    public void Map_WithUnsupportedComponent_AddsWarningAndSkipsComponent()
    {
        var unsupported = new Border { X = 1, Y = 2, Width = 3, Height = 4 };

        var result = new FmlToOasisMapper().Map(new Layout([unsupported]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Empty(result.Elements);
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.component.unsupported" && warning.Context == nameof(Border));
    }

    [Theory]
    [InlineData("#0080C0FF", "#FF0080C0", 255, 0, 128, 192)]
    [InlineData("#12345678", "#78123456", 0x78, 0x12, 0x34, 0x56)]
    [InlineData("#123456", "#FF123456", 255, 0x12, 0x34, 0x56)]
    public void ConvertDecoderRgbaToOasisArgb_ReordersChannels(string decoderValue, string expected, byte a, byte r, byte g, byte b)
    {
        var converted = FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(decoderValue);

        Assert.Equal(expected, converted);
        AssertOasisArgbChannels(converted, a, r, g, b);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("#1234567890")]
    [InlineData("#12345G")]
    [InlineData("123456")]
    public void ConvertDecoderRgbaToOasisArgb_RejectsMalformedValues(string decoderValue)
    {
        Assert.Null(FmlToOasisMapper.ConvertDecoderRgbaToOasisArgb(decoderValue));
    }

    private static void AssertOasisArgbChannels(string? value, byte a, byte r, byte g, byte b)
    {
        Assert.True(InspectorColorHex.TryParse(value, out Color color));
        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Map_Button_MapsBorderFromNoOutlineWithButtonDefault(bool? noOutline, bool expectedHasBorder)
    {
        var button = new Button { SublampTable = [new LampSublampTableEntry(1, 5)] };
        if (noOutline.HasValue) button.Booleans["NoOutline"] = noOutline.Value;

        var result = new FmlToOasisMapper().Map(new Layout([button]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(expectedHasBorder, Assert.Single(result.Elements).HasBorder);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Map_Lamp_MapsBorderFromNoOutlineWithDefaultDisabled(bool? noOutline, bool expectedHasBorder)
    {
        var lamp = new Lamp { SublampTable = [new LampSublampTableEntry(1, 5)] };
        if (noOutline.HasValue) lamp.Booleans["NoOutline"] = noOutline.Value;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(expectedHasBorder, Assert.Single(result.Elements).HasBorder);
    }

    [Fact]
    public void Map_MultiSublamp_AppliesBorderToEveryGeneratedLamp()
    {
        var lamp = new Lamp { SublampTable = [new LampSublampTableEntry(1, 5), new LampSublampTableEntry(2, 6)] };
        lamp.Booleans["NoOutline"] = false;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.All(result.Elements, element => Assert.True(element.HasBorder));
    }

    [Fact]
    public void Map_Lamp_PreservesMfmeBlendFlagOnEveryGeneratedSublamp()
    {
        var lamp = new Lamp { SublampTable = [new LampSublampTableEntry(1, 5), new LampSublampTableEntry(2, 6)] };
        lamp.Booleans["Blend"] = true;

        var result = new FmlToOasisMapper().Map(new Layout([lamp]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Equal(2, result.Elements.Count);
        Assert.All(result.Elements, element => Assert.True(element.SourceBlend));
    }

}

public sealed class FmlReelLampImportTests
{
    [Fact]
    public void Map_WithCommonMfmeReelLampSlots_MapsSlots234ToTopMiddleBottom()
    {
        var reel = CreateReel([new LampSublampTableEntry(2, 5), new LampSublampTableEntry(3, 4), new LampSublampTableEntry(4, 3)]);
        reel.Booleans["LampsEnabled"] = true;

        var result = new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>());
        var element = Assert.Single(result.Elements);

        Assert.Equal(PanelElementKind.Reel, element.Kind);
        Assert.True(element.ReelLampsEnabled);
        Assert.Equal([5, 4, 3], element.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
        Assert.All(element.ReelLamps, lamp => Assert.Equal(0d, lamp.Radius));
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "fml.import.reel.lamps.common3");
    }

    [Theory]
    [InlineData(2, null, 4, 3)]
    [InlineData(3, 5, null, 3)]
    [InlineData(4, 5, 4, null)]
    public void Map_WithMissingCommonMfmeSlot_KeepsOnlyThatOasisSlotUnassigned(int missingSlot, int? expectedTop, int? expectedMiddle, int? expectedBottom)
    {
        var entries = new[] { new LampSublampTableEntry(2, 5), new LampSublampTableEntry(3, 4), new LampSublampTableEntry(4, 3) }
            .Where(entry => entry.SublampIndex != missingSlot)
            .ToArray();
        var element = Assert.Single(new FmlToOasisMapper().Map(new Layout([CreateReel(entries)]), new Dictionary<FmlDecodedImageKey, string>()).Elements);

        Assert.Equal([expectedTop, expectedMiddle, expectedBottom], element.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
    }

    [Fact]
    public void Map_WithUndefinedCommonMfmeSlots_KeepsSlotsUnassigned()
    {
        var result = new FmlToOasisMapper().Map(new Layout([CreateReel([new LampSublampTableEntry(2, -2), new LampSublampTableEntry(3, -2), new LampSublampTableEntry(4, -2)])]), new Dictionary<FmlDecodedImageKey, string>());
        var element = Assert.Single(result.Elements);

        Assert.Equal(3, element.ReelLamps.Count);
        Assert.All(element.ReelLamps, lamp => Assert.Null(lamp.LampNumber));
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.reel.lamps.common3.undefined");
    }

    [Fact]
    public void Map_WithExtraMfmeTraySlots_IgnoresExtrasWithoutShiftingCommonSlots()
    {
        // Oasis intentionally imports only the common three-lamp subset of MFME's larger 15-slot tray.
        var reel = CreateReel(
        [
            new LampSublampTableEntry(1, 99),
            new LampSublampTableEntry(2, 5),
            new LampSublampTableEntry(3, 4),
            new LampSublampTableEntry(4, 3),
            new LampSublampTableEntry(5, 88),
            new LampSublampTableEntry(6, 77),
            new LampSublampTableEntry(11, 66)
        ]);

        var result = new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>());
        var element = Assert.Single(result.Elements);

        Assert.Equal([5, 4, 3], element.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.reel.lamps.extraIgnored");
    }


    [Fact]
    public void Map_WithLampsEnabledAndNoCommonSlots_WarnsClearly()
    {
        var reel = CreateReel([]);
        reel.Booleans["LampsEnabled"] = true;

        var result = new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>());

        Assert.Contains(result.Warnings, warning => warning.Code == "fml.import.reel.lamps.common3.missing");
    }

    [Fact]
    public void Map_WithLampsEnabledFalse_PreservesAssignmentsButMarksDisabled()
    {
        var reel = CreateReel([new LampSublampTableEntry(2, 5), new LampSublampTableEntry(3, 4), new LampSublampTableEntry(4, 3)]);
        reel.Booleans["LampsEnabled"] = false;

        var element = Assert.Single(new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>()).Elements);

        Assert.False(element.ReelLampsEnabled);
        Assert.Equal([5, 4, 3], element.ReelLamps.Select(lamp => lamp.LampNumber).ToArray());
    }

    [Fact]
    public void Map_WithOpaqueReel_DecodesOpaqueFlag()
    {
        var reel = CreateReel([]);
        reel.Booleans["OpaqueBand"] = true;

        var element = Assert.Single(new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>()).Elements);

        Assert.True(element.IsOpaqueReel);
    }

    [Fact]
    public void Map_WithNonOpaqueReel_DecodesOpaqueFlagFalse()
    {
        var reel = CreateReel([]);
        reel.Booleans["OpaqueBand"] = false;

        var element = Assert.Single(new FmlToOasisMapper().Map(new Layout([reel]), new Dictionary<FmlDecodedImageKey, string>()).Elements);

        Assert.False(element.IsOpaqueReel);
        Assert.Null(element.ReelLampTransmissionMaskAssetPath);
    }

    private static Reel CreateReel(IReadOnlyList<LampSublampTableEntry> entries)
    {
        var reel = new Reel { X = 1, Y = 2, Width = 30, Height = 90, SublampTable = entries };
        reel.UInt32s["Stops"] = 20;
        return reel;
    }
}
