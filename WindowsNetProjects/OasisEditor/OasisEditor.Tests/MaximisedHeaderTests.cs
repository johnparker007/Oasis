using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class MaximisedHeaderTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MaximisedHeaderKeepsActiveColoursAcrossThemeChanges(bool document, bool floating)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var previous = Application.Current!.RequestedThemeVariant;
            var root = Path.Combine(Path.GetTempPath(), "OasisActiveHeader-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Headers"); var panel = session.CreatePanel("Panel");
                using var capture = new WorkspaceCapture(session);
                await capture.View.OpenNativeAssetAsync("Panel2D", panel); capture.Capture();
                var id = document ? WorkspaceLayoutState.LeafMap(capture.View.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DockControl>().Single().Layout!).Values.Single(d => d is Dock.Model.Controls.IDocument && d.Context is WorkspaceView).Id : "Hierarchy";
                if (floating)
                {
                    if (document) await capture.View.FloatDocumentAsync("Panel2D", panel);
                    else capture.View.MoveWorkspacePane(id, "float");
                }
                capture.View.ToggleTabMaximised(id);
                foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light, ThemeVariant.Default })
                {
                    Application.Current.RequestedThemeVariant = theme; capture.Capture();
                    var host = floating ? Assert.Single(capture.View.FloatingWorkspaceWindows) : (Control)capture.View;
                    var header = Assert.Single(host.GetVisualDescendants().OfType<Border>().Where(c => c.Name == "MaximisedTabHeader"));
                    var label = Assert.Single(header.GetVisualDescendants().OfType<TextBlock>());
                    Assert.True(header.TryFindResource("DockSurfaceHeaderActiveBrush", out var active));
                    Assert.True(header.TryFindResource("DockTabActiveForegroundBrush", out var foreground));
                    Assert.Equal(((IBrush)active!).ToString(), header.Background?.ToString());
                    Assert.Equal(((IBrush)foreground!).ToString(), label.Foreground?.ToString());
                    Assert.NotEqual(capture.View.FindResource("ToolbarBrush")?.ToString(), header.Background?.ToString());
                    foreach (var icon in header.GetVisualDescendants().OfType<EditorIcon>())
                        Assert.Equal(label.Foreground?.ToString(), icon.Foreground?.ToString());
                    var output = Environment.GetEnvironmentVariable("OASIS_ICON_VERIFICATION_OUTPUT");
                    if (!floating && theme == ThemeVariant.Dark && !string.IsNullOrWhiteSpace(output))
                    { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"maximised-{(document ? "document" : "pane")}.png"), capture.Capture()); }
                }
                capture.View.RestoreMaximisedTab();
                Assert.Null(capture.View.InspectTabPresentation().MaximisedPaneId);
            }
            finally { Application.Current.RequestedThemeVariant = previous; Directory.Delete(root, true); }
        });
    }
}
