using System.Text.Json.Nodes;
using OasisEditor.Features.FmlImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FmlDecodedLayoutAdapterTests
{
    [Fact]
    public void ToMfmeExtractManifestJson_WithFmlLampImages_MapsLampElementNumberAndBitmapFilenames()
    {
        const string decodedJson = """
        {
          "Components": [
            {
              "Type": "Lamp",
              "Geometry": { "X": 10, "Y": 20, "Width": 30, "Height": 40, "Number": 0 },
              "SubLampNumberTable": { "NumberOfDefinedLampNumbers": 1, "Lamp1": 224 },
              "Images": {
                "sublamp_1_main_image": { "Width": 16, "Height": 16, "BitsPerPixel": 32 },
                "sublamp_1_mask_image": { "Width": 16, "Height": 16, "BitsPerPixel": 32 },
                "brightmask_main": { "Width": 16, "Height": 16, "BitsPerPixel": 32 }
              }
            }
          ]
        }
        """;
        var imagePaths = new Dictionary<FmlDecodedImageKey, string>
        {
            [new(0, "sublamp_1_main_image")] = "lamps/0005_sublamp_1_main_image.bmp",
            [new(0, "sublamp_1_mask_image")] = "lamps/0005_sublamp_1_mask_image.bmp",
            [new(0, "brightmask_main")] = "lamps/0005_brightmask_main.bmp"
        };

        var manifestJson = FmlDecodedLayoutAdapter.ToMfmeExtractManifestJson(decodedJson, "layout", imagePaths);

        var component = JsonNode.Parse(manifestJson)!["Components"]!.AsArray()[0]!.AsObject();
        Assert.Equal("Oasis.FmlImport.ExtractComponentLamp, Oasis.FmlImport", component["$type"]!.GetValue<string>());
        Assert.True(component["Graphic"]!.GetValue<bool>());

        var lampElement = component["LampElements"]!.AsArray()[0]!.AsObject();
        Assert.Equal("224", lampElement["NumberAsText"]!.GetValue<string>());
        Assert.Equal("0005_sublamp_1_main_image.bmp", lampElement["BmpImageFilename"]!.GetValue<string>());
        Assert.Equal("0005_sublamp_1_mask_image.bmp", lampElement["BmpMaskImageFilename"]!.GetValue<string>());
        Assert.True(lampElement["Graphic"]!.GetValue<bool>());
    }

    [Fact]
    public void ToMfmeExtractManifestJson_WithOriginalFmlLampImageNames_MapsLampElementBitmapFilenames()
    {
        const string decodedJson = """
        {
          "Components": [
            {
              "Type": "Lamp",
              "Geometry": { "X": 10, "Y": 20, "Width": 30, "Height": 40, "Number": 12 },
              "Images": {
                "Sublamp 1 Main image": { "Width": 16, "Height": 16, "BitsPerPixel": 32 },
                "Sublamp 1 Mask image": { "Width": 16, "Height": 16, "BitsPerPixel": 32 }
              }
            }
          ]
        }
        """;
        var imagePaths = new Dictionary<FmlDecodedImageKey, string>
        {
            [new(0, "Sublamp 1 Main image")] = "lamps/0000_sublamp_1_main_image.bmp",
            [new(0, "Sublamp 1 Mask image")] = "lamps/0000_sublamp_1_mask_image.bmp"
        };

        var manifestJson = FmlDecodedLayoutAdapter.ToMfmeExtractManifestJson(decodedJson, "layout", imagePaths);

        var lampElement = JsonNode.Parse(manifestJson)!["Components"]!.AsArray()[0]!["LampElements"]!.AsArray()[0]!.AsObject();
        Assert.Equal("12", lampElement["NumberAsText"]!.GetValue<string>());
        Assert.Equal("0000_sublamp_1_main_image.bmp", lampElement["BmpImageFilename"]!.GetValue<string>());
        Assert.Equal("0000_sublamp_1_mask_image.bmp", lampElement["BmpMaskImageFilename"]!.GetValue<string>());
        Assert.True(lampElement["Graphic"]!.GetValue<bool>());
    }
}
