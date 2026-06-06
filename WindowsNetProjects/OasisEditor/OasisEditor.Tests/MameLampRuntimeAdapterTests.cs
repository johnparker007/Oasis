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
        Assert.Equal(0d, document.RuntimeState.GetLampIntensity(MachineObjectReference.Lamp(7)));
        Assert.Equal(1d, document.RuntimeState.GetLampIntensity(MachineObjectReference.Lamp(8)));
    }

    [Fact]
    public void ApplyLampState_WhenNoMatchingLamp_DoesNotPublishVisualPreviewEvent()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var adapter = new MameLampRuntimeAdapter(
            () => [document],
            () => false,
            _ => { },
            action => dispatches.Add(action));

        var previewEventCount = 0;
        document.PanelVisualStateChanged += _ => previewEventCount++;

        adapter.ApplyLampState(999, 255);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        Assert.Equal(0, previewEventCount);
        Assert.Equal(0d, document.RuntimeState.GetLampIntensity("lamp-7"));
        Assert.Equal(0d, document.RuntimeState.GetLampIntensity("lamp-8"));
    }

    [Fact]
    public void ApplyLampState_WhenDebugEnabled_LogsAppliedLampState()
    {
        var document = CreateDocument();
        var dispatches = new List<Action>();
        var logs = new List<string>();
        var adapter = new MameLampRuntimeAdapter(
            () => [document],
            () => true,
            logs.Add,
            action => dispatches.Add(action));

        adapter.ApplyLampState(7, 255);

        var dispatch = Assert.Single(dispatches);
        dispatch();

        var log = Assert.Single(logs);
        Assert.Contains("lamp7", log);
        Assert.Contains("intensity=1", log);
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
