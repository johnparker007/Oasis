using Xunit;
using System.Collections.ObjectModel;

namespace OasisEditor.Tests;

public sealed class Panel2DRoundTripTests
{
    [Fact]
    public void ToModel_AndToStorageElements_PreserveExplicitValues()
    {
        var file = new Panel2DDocumentFile
        {
            SchemaVersion = 1,
            Title = "Panel X",
            Summary = "Summary X",
            SavedAtUtc = DateTime.UtcNow,
            Elements =
            [
                new PanelElementFile
                {
                    ObjectId = "abc123",
                    Name = "Name 1",
                    Kind = "lamp",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    AssetPath = "Assets/FmlImport/layout/Lamps/lamp.png",
                    SecondaryAssetPath = "Assets/FmlImport/layout/Lamps/lamp-off.png",
                    DisplayNumber = 8,
                    LampNumber = 0,
                    OnColorHex = "#FFFFFFFF",
                    OffColorHex = "#FF111111",
                    TextColorHex = "#FFFF0000",
                    DisplayText = "HOLD",
                    IsReversed = true,
                    Stops = 24,
                    VisibleScale = 0.75,
                    ImportSource = new PanelElementImportSourceFile
                    {
                        Format = "LegacyImport",
                        Reference = "layout.json#lamp-8"
                    }
                }
            ]
        };

        var model = Panel2DDocumentStorage.ToModel(file);
        var storageElements = Panel2DDocumentStorage.ToStorageElements(model);

        var element = Assert.Single(storageElements);
        Assert.Equal("abc123", element.ObjectId);
        Assert.Equal("Name 1", element.Name);
        Assert.Equal("lamp", element.Kind);
        Assert.Equal(1, element.X);
        Assert.Equal(2, element.Y);
        Assert.Equal(3, element.Width);
        Assert.Equal(4, element.Height);
        Assert.Equal("Assets/FmlImport/layout/Lamps/lamp.png", element.AssetPath);
        Assert.Equal("Assets/FmlImport/layout/Lamps/lamp-off.png", element.SecondaryAssetPath);
        Assert.Equal(8, element.DisplayNumber);
        Assert.Equal(0, element.LampNumber);
        Assert.Equal("#FFFFFFFF", element.OnColorHex);
        Assert.Equal("#FF111111", element.OffColorHex);
        Assert.Equal("#FFFF0000", element.TextColorHex);
        Assert.Equal("HOLD", element.DisplayText);
        Assert.True(element.IsReversed);
        Assert.Equal(24, element.Stops);
        Assert.Equal(0.75, element.VisibleScale);
        Assert.NotNull(element.ImportSource);
        Assert.Equal("LegacyImport", element.ImportSource!.Format);
        Assert.Equal("layout.json#lamp-8", element.ImportSource.Reference);
    }



    [Fact]
    public void LabelLampNumber_NullZeroAndPositive_RoundTripThroughStorageElements()
    {
        var model = new Panel2DDocumentModel
        {
            Title = "Panel",
            Elements =
            [
                new PanelElementModel { ObjectId = "label-null", Name = "Static", Kind = PanelElementKind.Label, Width = 10, Height = 10, DisplayText = "A" },
                new PanelElementModel { ObjectId = "label-zero", Name = "Lamp Zero", Kind = PanelElementKind.Label, Width = 10, Height = 10, DisplayText = "B", LampNumber = 0 },
                new PanelElementModel { ObjectId = "label-five", Name = "Lamp Five", Kind = PanelElementKind.Label, Width = 10, Height = 10, DisplayText = "C", LampNumber = 5 }
            ]
        };

        var roundTripped = Panel2DDocumentStorage.ToModel(new Panel2DDocumentFile
        {
            SchemaVersion = 1,
            Elements = Panel2DDocumentStorage.ToStorageElements(model).ToArray()
        });

        Assert.Null(roundTripped.Elements[0].LampNumber);
        Assert.Equal(0, roundTripped.Elements[1].LampNumber);
        Assert.Equal(5, roundTripped.Elements[2].LampNumber);
    }

    [Fact]
    public void Serialize_AndRead_RoundTripsLampOffColorHexWithoutReconversion()
    {
        var json = Panel2DDocumentStorage.Serialize(
            "Panel",
            "Panel with imported lamp off color",
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-off-color",
                    Name = "Lamp",
                    Kind = "lamp",
                    X = 1,
                    Y = 2,
                    Width = 30,
                    Height = 40,
                    OnColorHex = "#FF0000FF",
                    OffColorHex = "#FF204060",
                    TextColorHex = "#FFFFFFFF"
                }
            ]);

        Assert.True(Panel2DDocumentStorage.TryReadValidated(json, out var parsed, out var error), error);

        var model = Panel2DDocumentStorage.ToModel(parsed);
        var element = Assert.Single(model.Elements);
        Assert.Equal("#FF204060", element.OffColorHex);

        var storageElement = Assert.Single(Panel2DDocumentStorage.ToStorageElements(model));
        Assert.Equal("#FF204060", storageElement.OffColorHex);
    }

    [Fact]
    public void Serialize_WithNativeElementMetadata_WritesCurrentSchemaVersion()
    {
        var json = Panel2DDocumentStorage.Serialize(
            "Panel V2",
            "Native",
            [
                new PanelElementFile
                {
                    ObjectId = "lamp-v2",
                    Name = "Lamp V2",
                    Kind = "lamp",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    Native = new PanelElementNativeFile
                    {
                        Number = 12
                    }
                }
            ]);

        Assert.True(Panel2DDocumentStorage.TryRead(json, out var parsed));
        Assert.Equal(Panel2DDocumentStorage.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void ToStorageElement_WithLegacyRectangle_DoesNotEmitNativePayload()
    {
        var model = new PanelElementModel
        {
            ObjectId = "rect-1",
            Name = "Rect 1",
            Kind = PanelElementKind.Rectangle,
            X = 10,
            Y = 20,
            Width = 30,
            Height = 40
        };

        var storage = Panel2DDocumentStorage.ToStorageElement(model);

        Assert.Null(storage.Native);
    }

    [Fact]
    public void TryReadValidated_WithFutureSchemaVersion_ReturnsUnsupportedVersionError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 999,
          "Title": "Future Panel",
          "Summary": "Future",
          "Elements": []
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("Unsupported schema version '999'", errorMessage);
    }

    [Fact]
    public void TryReadValidated_WithSchemaVersion1_MigratesToCurrentSchema()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Legacy Panel",
          "Summary": "Legacy",
          "Elements": [
            {
              "ObjectId": "legacy-lamp",
              "Name": "Legacy Lamp",
              "Kind": "lamp",
              "X": 10,
              "Y": 20,
              "Width": 30,
              "Height": 40,
              "AssetPath": " Assets/legacy/lamp.png ",
              "DisplayNumber": 7,
              "OnColorHex": " #FFFFFFFF "
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var parsed, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        Assert.Equal(Panel2DDocumentStorage.CurrentSchemaVersion, parsed.SchemaVersion);
        var element = Assert.Single(parsed.Elements);
        Assert.Equal("Assets/legacy/lamp.png", element.AssetPath);
        Assert.Equal(7, element.DisplayNumber);
        Assert.Equal("#FFFFFFFF", element.OnColorHex);
        Assert.NotNull(element.Native);
        Assert.Equal("Assets/legacy/lamp.png", element.Native!.AssetPath);
        Assert.Equal(7, element.Native.Number);
        Assert.Equal("#FFFFFFFF", element.Native.OnColorHex);
    }

    [Fact]
    public void TryReadValidated_WithInvalidElementKind_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Invalid Kind Panel",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "obj-1",
              "Name": "Bad Kind",
              "Kind": "triangle",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("unsupported kind", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_WithDuplicateObjectIds_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Duplicate ObjectIds",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "dup-1",
              "Name": "A",
              "Kind": "rectangle",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4
            },
            {
              "ObjectId": "dup-1",
              "Name": "B",
              "Kind": "image",
              "X": 5,
              "Y": 6,
              "Width": 7,
              "Height": 8
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("duplicated", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_WithInvalidNativeMetadata_ReturnsValidationError()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Invalid Native Metadata",
          "Summary": "Invalid",
          "Elements": [
            {
              "ObjectId": "lamp-1",
              "Name": "Lamp 1",
              "Kind": "lamp",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4,
              "DisplayNumber": -1
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out _, out var errorMessage);

        Assert.False(success);
        Assert.Contains("invalid display number", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReadValidated_NormalizesOptionalNativeMetadata()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Normalize Native Metadata",
          "Summary": "Normalize",
          "Elements": [
            {
              "ObjectId": "alpha-1",
              "Name": "Alpha",
              "Kind": "alpha",
              "X": 1,
              "Y": 2,
              "Width": 3,
              "Height": 4,
              "AssetPath": "  Assets/alpha.png  ",
              "DisplayText": "  HELLO  ",
              "ImportSource": {
                "Format": "  LegacyImport  ",
                "Reference": "  layout.json#alpha-1  "
              }
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var normalized, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        var element = Assert.Single(normalized.Elements);
        Assert.Equal("Assets/alpha.png", element.AssetPath);
        Assert.Equal("HELLO", element.DisplayText);
        Assert.NotNull(element.ImportSource);
        Assert.Equal("LegacyImport", element.ImportSource!.Format);
        Assert.Equal("layout.json#alpha-1", element.ImportSource.Reference);
    }

    [Theory]
    [InlineData("background", "background")]
    [InlineData("lamp", "lamp")]
    [InlineData("reel", "reel")]
    [InlineData("sevenSegment", "sevenSegment")]
    [InlineData("alpha", "alpha")]
    [InlineData("vfdDotMatrix", "vfdDotMatrix")]
    public void ParseElementKind_WithNativeKinds_ReturnsExpectedKind(string serializedKind, string expectedRoundTripKind)
    {
        var parsedKind = Panel2DDocumentStorage.ParseElementKind(serializedKind);
        var roundTripKind = Panel2DDocumentStorage.SerializeElementKind(parsedKind);

        Assert.NotEqual(PanelElementKind.Unknown, parsedKind);
        Assert.Equal(expectedRoundTripKind, roundTripKind);
    }

    [Theory]
    [InlineData("background", "Background ")]
    [InlineData("lamp", "Lamp ")]
    [InlineData("reel", "Reel ")]
    [InlineData("sevenSegment", "7 Segment ")]
    [InlineData("alpha", "Alpha ")]
    [InlineData("vfdDotMatrix", "VFD Dot Matrix ")]
    public void CreateDefaultElementName_WithNativeKinds_UsesNativePrefix(string kind, string expectedPrefix)
    {
        var name = Panel2DDocumentStorage.CreateDefaultElementName(kind, "abcdef123456");

        Assert.StartsWith(expectedPrefix, name);
    }

    [Fact]
    public void TryReadValidated_WithMissingNameAndObjectId_NormalizesValues()
    {
        const string sourceJson = """
        {
          "SchemaVersion": 1,
          "Title": "Normalizable Panel",
          "Summary": "Normalizable",
          "Elements": [
            {
              "ObjectId": "",
              "Name": "",
              "Kind": "rectangle",
              "X": 1,
              "Y": 2,
              "Width": 30,
              "Height": 40
            }
          ]
        }
        """;

        var success = Panel2DDocumentStorage.TryReadValidated(sourceJson, out var parsed, out var errorMessage);

        Assert.True(success);
        Assert.Equal(string.Empty, errorMessage);
        var element = Assert.Single(parsed.Elements);
        Assert.False(string.IsNullOrWhiteSpace(element.ObjectId));
        Assert.StartsWith("Rectangle ", element.Name);
    }

}
