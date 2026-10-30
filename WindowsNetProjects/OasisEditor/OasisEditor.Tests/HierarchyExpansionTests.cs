using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Xunit.Abstractions;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class HierarchyExpansionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task LargeGroupExpansionKeepsSelectionHistoryAndOtherPanesIntact()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisHierarchyExpansion-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Expansion"); author.CreatePanel("Panel");
                var lamps = Enumerable.Range(0, 130).Select(i => author.AddSpecialistElement("lamp", i * 10, 10)).ToArray();
                author.SelectElements(lamps); var group = author.CreateGroup("Lamps");
                for (var i = 0; i < 8; i++) author.CreateGroup("Other " + i, groupSelection: false);
                author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var editor = capture.View.CommandView; var session = editor.CommandSession; session.Select(lamps[0]); capture.Capture();
                var owner = (Window)TopLevel.GetTopLevel(capture.View)!;
                void Layout() { owner.UpdateLayout(); Dispatcher.UIThread.RunJobs(); owner.UpdateLayout(); }
                var times = new List<double>(); var closes = new List<double>();
                StackPanel GroupRow() => owner.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, group));
                var disclosureRow = GroupRow();
                var inspectorField = owner.GetVisualDescendants().OfType<TextBox>().First(t => t.Text == "Lamp");
                var couldUndo = session.CanUndo;
                for (var cycle = 0; cycle < 5; cycle++)
                {
                    var watch = Stopwatch.StartNew(); editor.SetPanelGroupExpanded(group, true); Layout(); times.Add(watch.Elapsed.TotalMilliseconds);
                    Assert.Equal(139, editor.InspectPanelHierarchyRows().Count);
                    Assert.Equal(lamps[0], session.SelectedId);
                    Assert.False(session.IsDirty);
                    Assert.Same(disclosureRow, GroupRow());
                    Assert.Contains(inspectorField, owner.GetVisualDescendants());
                    Assert.Equal(couldUndo, session.CanUndo);
                    var realized = owner.GetVisualDescendants().OfType<StackPanel>().Count(p => p.Tag is string id && (id == group || lamps.Contains(id)));
                    Assert.True(realized < lamps.Length, "Offscreen children should remain virtualized.");
                    watch.Restart(); editor.SetPanelGroupExpanded(group, false); Layout(); closes.Add(watch.Elapsed.TotalMilliseconds);
                    Assert.Equal(9, editor.InspectPanelHierarchyRows().Count);
                    Assert.Equal(lamps[0], session.SelectedId);
                    Assert.False(session.IsDirty);
                    Assert.Same(disclosureRow, GroupRow());
                    Assert.Contains(inspectorField, owner.GetVisualDescendants());
                }
                output.WriteLine("Expand ms: " + string.Join(", ", times.Select(t => t.ToString("F1"))));
                output.WriteLine("Collapse ms: " + string.Join(", ", closes.Select(t => t.ToString("F1"))));
                Assert.False(session.CanRedo);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
