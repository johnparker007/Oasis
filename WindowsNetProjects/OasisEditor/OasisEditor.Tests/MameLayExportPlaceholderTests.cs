using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameLayExportPlaceholderTests
{
    [Fact]
    public void Export_ReturnsClearNotImplementedFailure()
    {
        var service = new PlaceholderMameLayExportService();
        var panel = new DocumentTabViewModel(EditorDocument.CreatePanel2DStub("Panel"), Panel2DDocumentStorage.SerializeLayout([]));

        var result = service.Export(panel, "out.lay");

        Assert.False(result.Succeeded);
        Assert.Contains("not implemented", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
