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
}
