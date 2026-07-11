using System.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementHasBorderStorageTests
{
    [Fact]
    public void Panel2DStorage_RoundTripsHasBorder()
    {
        var document = new Panel2DDocumentModel
        {
            Title = "Panel",
            Elements =
            [
                new PanelElementModel { ObjectId = "lamp", Name = "Lamp", Kind = PanelElementKind.Lamp, Width = 10, Height = 10, HasBorder = true }
            ]
        };

        var json = Panel2DDocumentStorage.Serialize("Panel", string.Empty, Panel2DDocumentStorage.ToStorageElements(document));
        var loaded = Panel2DDocumentStorage.DeserializeModel(json);

        Assert.True(Assert.Single(loaded.Elements).HasBorder);
    }

    [Fact]
    public void Panel2DStorage_MissingHasBorder_DeserializesFalse()
    {
        const string json = """
        {"schemaVersion":2,"title":"Panel","elements":[{"objectId":"lamp","name":"Lamp","kind":"lamp","width":10,"height":10}]}
        """;

        var loaded = Panel2DDocumentStorage.DeserializeModel(json);

        Assert.False(Assert.Single(loaded.Elements).HasBorder);
    }
}
