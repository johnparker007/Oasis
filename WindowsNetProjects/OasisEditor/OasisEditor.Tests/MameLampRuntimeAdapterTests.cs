using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameLampRuntimeAdapterTests
{
    [Fact]
    public void ApplyLampState_CoalescesPendingUpdatesIntoSingleUiDispatch()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameLampRuntimeAdapter(
            () => [document],
            () => false,
            _ => { },
            action => dispatches.Add(action));

        adapter.ApplyLampState(7, 255);
        adapter.ApplyLampState(7, 0);
        adapter.ApplyLampState(8, 255);

        Assert.Single(dispatches);
        dispatches[0]();

        Assert.Equal(0d, document.RuntimeState.GetLampIntensity("lamp-7"));
        Assert.Equal(1d, document.RuntimeState.GetLampIntensity("lamp-8"));
    }

    private static DocumentTabViewModel CreateDocument()
    {
        var panelDocument = EditorDocument.CreateFromFile(
            "panel.panel2d",
            "panel",
            "panel");
        var tab = new DocumentTabViewModel(panelDocument);
        tab.SetPanelElements(
        [
            new PanelElementModel { ObjectId = "lamp-7", Kind = PanelElementKind.Lamp, DisplayNumber = 7 },
            new PanelElementModel { ObjectId = "lamp-8", Kind = PanelElementKind.Lamp, DisplayNumber = 8 }
        ]);
        return tab;
    }
}
