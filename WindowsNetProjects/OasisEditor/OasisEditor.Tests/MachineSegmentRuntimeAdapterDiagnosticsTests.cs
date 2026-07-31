using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineSegmentRuntimeAdapterDiagnosticsTests
{
    [Fact]
    public void AlphaUpdate_LogsRawIdentityBaseAddressMappingAndCanonicalArray()
    {
        var document = new DocumentTabViewModel(EditorDocument.CreateFromFile("panel.panel2d", "panel", "panel"));
        document.SetPanelElements([
            new PanelElementModel
            {
                ObjectId = "alpha-impact-0",
                Kind = PanelElementKind.Alpha,
                DisplayNumber = 0,
                IsReversed = true
            }
        ]);
        var dispatches = new List<Action>();
        var diagnostics = new List<string>();
        var adapter = new MachineSegmentRuntimeAdapter(
            () => [document],
            dispatches.Add,
            () => FruitMachinePlatformType.Impact,
            diagnostics.Add);

        adapter.ApplySegmentState(0, 0x41, SegmentOutputType.NativeAlpha);
        adapter.ApplySegmentState(15, 0x50, SegmentOutputType.NativeAlpha);
        Assert.Single(dispatches)();

        var trace = Assert.Single(diagnostics);
        Assert.Contains("platform=Impact", trace);
        Assert.Contains("displayNumber=0", trace);
        Assert.Contains("reference=alpha:0", trace);
        Assert.Contains("baseIndex=0", trace);
        Assert.Contains("reversed=True", trace);
        Assert.Contains("NativeAlpha:id=0:mask=0x41", trace);
        Assert.Contains("NativeAlpha:id=15:mask=0x50", trace);
        Assert.Contains("dst=0<-srcOffset=0:id=0:NativeAlpha", trace);
        Assert.Contains("dst=15<-srcOffset=15:id=15:NativeAlpha", trace);
        Assert.Contains("canonical=[0x41", trace);
        Assert.EndsWith("0x50]", trace);
    }
}
