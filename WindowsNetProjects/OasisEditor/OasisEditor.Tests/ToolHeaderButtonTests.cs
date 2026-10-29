using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class ToolHeaderButtonTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("System")]
    public async Task DockGlyphsHaveVisibleContentInCompactToolButtons(string theme)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OasisToolButtons-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Buttons"); session.CreatePanel("Panel"); session.AddRectangle(); session.Save();
                using var capture = new WorkspaceCapture(session); await capture.View.SynchronizeAssetDocumentsAsync();
                var image = capture.Capture(theme: theme);
                var output = Environment.GetEnvironmentVariable("OASIS_TOOL_BUTTON_OUTPUT");
                if (output is not null) { Directory.CreateDirectory(output); File.WriteAllBytes(System.IO.Path.Combine(output, $"tool-buttons-{theme}.png"), image); }
                Check(capture.View);
                // Chrome styles must not shrink ordinary buttons inside tool content.
                Assert.True(capture.View.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Copy to Library…")).Bounds.Width > 30);
                capture.View.MoveWorkspacePane("Hierarchy", "float"); capture.Capture(theme: theme); capture.CaptureFloatingWindows();
                Check(Assert.Single(capture.View.FloatingWorkspaceWindows));
            }
            finally { Directory.Delete(root, true); }
        });
    }
    private static void Check(Control host)
    {
        var chromes = host.GetVisualDescendants().OfType<ToolChromeControl>().ToArray();
        Assert.NotEmpty(chromes);
        foreach (var chrome in chromes)
        {
            var buttons = chrome.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("PART_") == true).ToArray();
            Assert.Equal(3, buttons.Count(b => b.IsVisible));
            foreach (var button in buttons)
            {
                if (!button.IsVisible) continue;
                Assert.Equal(20, button.Bounds.Width);
                Assert.Equal(20, button.Bounds.Height);
                var glyphs = button.GetVisualDescendants().OfType<Shape>().ToArray();
                Assert.NotEmpty(glyphs);
                Assert.True(glyphs.Any(p =>
                {
                    // Dock's Viewbox scales its native geometry; measure in button space.
                    var start = p.TranslatePoint(default, button);
                    var end = p.TranslatePoint(new global::Avalonia.Point(p.Bounds.Width, p.Bounds.Height), button);
                    return p.IsVisible && start is { } a && end is { } b &&
                        b.X - a.X >= 4 && b.X - a.X <= 16 && b.Y - a.Y >= 4 && b.Y - a.Y <= 16 &&
                        Math.Max(b.X - a.X, b.Y - a.Y) >= 8 &&
                        (p.Fill is not null || p.Stroke is not null);
                }), button.Name + ": " + string.Join("; ", glyphs.Select(p => $"{p.Name}: {p.Bounds} -> {p.TranslatePoint(default, button)} / {p.TranslatePoint(new global::Avalonia.Point(p.Bounds.Width, p.Bounds.Height), button)}")));
            }
        }
    }
}
