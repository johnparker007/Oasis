using OasisEditor.Automation;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ProjectAndPanelCreationServicesTests
{
    [Fact]
    public void CreatePanel2DStubDocument_ProvidesPanel2DDocumentWithLayout()
    {
        var service = new Panel2DDocumentCreationService();

        var document = service.CreatePanel2DStubDocument("Panel 42", 42);

        Assert.Equal(EditorDocumentType.Panel2D, document.Document.DocumentType);
        Assert.NotNull(document.PanelLayoutJson);
    }
}
