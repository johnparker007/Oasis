using OasisEditor.Features.CabinetEditor.Models;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetDocumentTests
{
    [Fact]
    public void NewCabinetDocumentsDefaultToLiveLampPreview()
    {
        Assert.Equal(CabinetLampPreviewMode.Live, new CabinetPreviewSettings(true, true).LampPreviewMode);
        Assert.Equal(CabinetLampPreviewMode.Live, CabinetPreviewSettings.Default.LampPreviewMode);
        Assert.Equal(CabinetLampPreviewMode.Live, CabinetDocument.Empty.Preview.LampPreviewMode);
        Assert.Equal(CabinetLampPreviewMode.Live, CabinetDocument.FromModelPath("cabinet.glb").Preview.LampPreviewMode);
    }

    [Fact]
    public void ExplicitlySavedLampPreviewModeIsPreserved()
    {
        var source = CabinetDocument.FromModelPath("cabinet.glb") with
        {
            Preview = CabinetPreviewSettings.Default with { LampPreviewMode = CabinetLampPreviewMode.BackgroundOnly }
        };

        Assert.True(CabinetDocumentStorage.TryRead(CabinetDocumentStorage.Serialize(source), out var parsed));
        Assert.Equal(CabinetLampPreviewMode.BackgroundOnly, parsed.Preview.LampPreviewMode);
    }
}
