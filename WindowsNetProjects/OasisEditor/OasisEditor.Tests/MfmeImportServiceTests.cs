using OasisEditor.Features.MfmeImport;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MfmeImportServiceTests
{
    [Fact]
    public void Import_WhenReaderFails_ReturnsErrorsAndNoElements()
    {
        var service = new MfmeImportService(
            new StubReader(
                new MfmeExtractReadResult
                {
                    Extract = null,
                    Warnings = [new MfmeImportWarning("warn", "warning")],
                    Errors = ["bad extract"]
                }));

        var result = service.Import(CreateContext());

        Assert.False(result.Succeeded);
        Assert.Single(result.Errors);
        Assert.Equal("bad extract", result.Errors[0]);
        Assert.Empty(result.ImportedElements);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Import_WhenReaderSucceeds_MapsElements()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = @"C:\extract",
            ManifestPath = @"C:\extract\layout.json",
            LayoutName = "Layout",
            Components =
            [
                new MfmeLegacyBackgroundComponent(
                    new MfmeLegacyPoint(0, 0),
                    new MfmeLegacyPoint(640, 480),
                    "bg.bmp",
                    null)
            ]
        };
        var service = new MfmeImportService(
            new StubReader(
                new MfmeExtractReadResult
                {
                    Extract = extract,
                    Warnings = [],
                    Errors = []
                }));

        var result = service.Import(CreateContext(copyAssets: false));

        Assert.True(result.Succeeded);
        var imported = Assert.Single(result.ImportedElements);
        Assert.Equal(PanelElementKind.Background, imported.Kind);
        Assert.Equal(640, imported.Width);
        Assert.Equal(480, imported.Height);
    }


    [Fact]
    public void Import_WithMixedLegacyComponents_ReturnsNativeElementsAndGenericImportBoundary()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = @"C:\extract",
            ManifestPath = @"C:\extract\layout.json",
            LayoutName = "Layout",
            Components =
            [
                new MfmeLegacyLampComponent(
                    new MfmeLegacyPoint(10, 20),
                    new MfmeLegacyPoint(30, 40),
                    "HOLD",
                    null,
                    null,
                    null,
                    new MfmeLegacyLampElement("7", 7, new MfmeLegacyColor(1f, 1f, 0f, 1f), "lamp.bmp", null, Graphic: true),
                    new MfmeLegacyColor(0f, 0f, 0f, 1f),
                    new MfmeLegacyColor(1f, 1f, 1f, 1f),
                    NoOutline: false, HasButtonInput: false, HasCoinInput: false, ButtonNumberAsString: null, Inverted: false, Shortcut1: null, Shortcut2: null),
                new UnsupportedLegacyComponent()
            ]
        };

        var service = new MfmeImportService(
            new StubReader(
                new MfmeExtractReadResult
                {
                    Extract = extract,
                    Warnings = [],
                    Errors = []
                }));

        var result = service.Import(CreateContext(copyAssets: false));

        Assert.True(result.Succeeded);
        var lamp = Assert.Single(result.ImportedElements);
        Assert.Equal(PanelElementKind.Lamp, lamp.Kind);
        Assert.Equal(7, lamp.DisplayNumber);
        Assert.Equal("HOLD", lamp.DisplayText);
        Assert.Equal("Tahoma", lamp.TextBoxFontName);
        Assert.Equal("Regular", lamp.TextBoxFontStyle);
        Assert.Equal("8", lamp.TextBoxFontSize);
        Assert.NotNull(lamp.ImportSource);
        Assert.Equal("LegacyImport", lamp.ImportSource!.Format);
        Assert.Equal("Lamp:7", lamp.ImportSource.Reference);

        Assert.Single(result.SkippedLegacyComponentTypes);
        Assert.Equal("ExtractComponentButton", result.SkippedLegacyComponentTypes[0]);
        Assert.Empty(result.InputDefinitions);
        Assert.Contains(result.Warnings, warning => warning.Code == "mfme.import.component.unsupported");
    }

    private static MfmeImportContext CreateContext(bool copyAssets = true)
    {
        return new MfmeImportContext
        {
            SourceExtractPath = @"C:\extract\layout.json",
            ProjectRootPath = @"C:\project",
            ProjectAssetsPath = @"C:\project\Assets",
            CopyAssets = copyAssets
        };
    }


    private sealed record UnsupportedLegacyComponent()
        : MfmeLegacyComponentBase(
            "ExtractComponentButton",
            new MfmeLegacyPoint(0, 0),
            new MfmeLegacyPoint(1, 1),
            null,
            null,
            null,
            null);

    private sealed class StubReader : IMfmeExtractReader
    {
        private readonly MfmeExtractReadResult _result;

        public StubReader(MfmeExtractReadResult result)
        {
            _result = result;
        }

        public MfmeExtractReadResult Read(MfmeImportContext context)
        {
            return _result;
        }
    }

    [Fact]
    public void Import_WithMfmeButtonInput_CreatesInputDefinition()
    {
        var extract = new MfmeLegacyExtractData
        {
            ExtractRootPath = @"C:\extract",
            ManifestPath = @"C:\extract\layout.json",
            LayoutName = "Layout",
            Components =
            [
                new MfmeLegacyButtonComponent(
                    new MfmeLegacyPoint(10, 20),
                    new MfmeLegacyPoint(30, 40),
                    "Start",
                    null,
                    null,
                    null,
                    HasButtonInput: true,
                    HasCoinInput: false,
                    ButtonNumberAsString: "5",
                    Inverted: false,
                    Shortcut1: "1",
                    Shortcut2: null,
                    FirstLampElement: null,
                    OffImageColor: null,
                    TextColor: null,
                    NoOutline: false)
            ]
        };

        var service = new MfmeImportService(new StubReader(new MfmeExtractReadResult { Extract = extract, Warnings = [], Errors = [] }));

        var result = service.Import(CreateContext(copyAssets: false));

        Assert.Single(result.ImportedElements);
        var input = Assert.Single(result.InputDefinitions);
        Assert.Equal(InputDefinitionKind.Button, input.Kind);
        Assert.Equal("5", input.ButtonNumber);
        Assert.Equal("1", input.RawMfmeShortcut);
        Assert.Equal("D1", input.KeyboardShortcut);
    }

}
