using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class EditorIconTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("System")]
    public void IconsInheritLabelForegroundAndSupportSelectedAndDisabledStates(string theme)
    {
        HeadlessTestHost.Run(() =>
        {
            var previous = Application.Current!.RequestedThemeVariant;
            Application.Current.RequestedThemeVariant = theme == "System" ? ThemeVariant.Default : theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
            var sheet = new StackPanel { Spacing = 5, Margin = new Thickness(12) };
            foreach (var kind in Enum.GetValues<EditorIconKind>().Where(k => k != EditorIconKind.None))
            {
                var row = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 16 };
                row.Children.Add(new TextBlock { Text = kind.ToString(), Width = 100 });
                row.Children.Add(new EditorIcon { Kind = kind });
                row.Children.Add(new EditorIcon { Kind = kind, Width = 20, Height = 20 });
                row.Children.Add(new EditorIcon { Kind = kind, IsEnabled = false });
                sheet.Children.Add(row);
            }
            var selected = new ListBoxItem { Content = new StackPanel { Children =
            {
                EditorIcons.Label(EditorIconKind.Inspector, "Selected Inspector"),
                EditorIcons.Label(EditorIconKind.SegmentedAlpha, "Selected segmented alpha"),
                EditorIcons.Label(EditorIconKind.DotMatrixAlpha, "Selected dot-matrix alpha")
            } } };
            sheet.Children.Add(new ListBox { ItemsSource = new[] { selected }, SelectedIndex = 0 });
            var window = new Window { Width = 380, Height = 350, Content = sheet };
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            try
            {
                foreach (var icon in selected.GetVisualDescendants().OfType<EditorIcon>())
                {
                    var label = ((Grid)icon.Parent!).Children.OfType<TextBlock>().Single();
                    Assert.Equal(label.Foreground?.ToString(), icon.Foreground?.ToString());
                    Assert.NotNull(icon.Foreground);
                }
                var normal = sheet.GetVisualDescendants().OfType<EditorIcon>().First(i => i.IsEnabled);
                var disabled = sheet.GetVisualDescendants().OfType<EditorIcon>().First(i => !i.IsEnabled);
                Assert.NotEqual(normal.Foreground?.ToString(), disabled.Foreground?.ToString());
                foreach (var kind in new[] { EditorIconKind.SegmentedAlpha, EditorIconKind.DotMatrixAlpha })
                    Assert.NotEqual(sheet.GetVisualDescendants().OfType<EditorIcon>().First(i => i.Kind == kind && i.IsEnabled).Foreground?.ToString(),
                        sheet.GetVisualDescendants().OfType<EditorIcon>().Single(i => i.Kind == kind && !i.IsEnabled).Foreground?.ToString());
                var path = Environment.GetEnvironmentVariable("OASIS_ICON_VERIFICATION_OUTPUT");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    Directory.CreateDirectory(path);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                    using var bitmap = new RenderTargetBitmap(new PixelSize(380, 350)); bitmap.Render(window);
                    bitmap.Save(Path.Combine(path, $"icons-{theme.ToLowerInvariant()}.png"));
                }
            }
            finally { window.Close(); Application.Current.RequestedThemeVariant = previous; }
        });
    }

    [Fact]
    public async Task ActualPaneHeadersKeepIconsAcrossFloatMaximiseAndRestore()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisIcons-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root); session.CreateProject("Icons");
                var faces = new FaceAuthoringSession(session);
                var face = faces.CreateFace("Components");
                faces.AddLamp("Lamp", 20, 20, 30, 30);
                faces.AddButton("Button", 70, 20);
                faces.AddSevenSegmentDisplay("7-segment display", 180, 20);
                faces.AddAlphaDisplay("Alpha display", 180, 100);
                faces.AddReelMount("Reel", 300, 20, 60, 100, 0); faces.Save();
                using var capture = new WorkspaceCapture(session, faces);
                await capture.View.OpenNativeAssetAsync("Face", face);
                capture.Capture();
                foreach (var kind in new[] { EditorIconKind.Lamp, EditorIconKind.Button, EditorIconKind.SevenSegment, EditorIconKind.Reel, EditorIconKind.SegmentedAlpha })
                    Assert.Contains(capture.View.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == kind);
                Assert.Contains(capture.View.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Hierarchy);
                Assert.Contains(capture.View.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Inspector);
                Assert.Contains(capture.View.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Project);
                AssertHeaderAlignment(capture.View);
                capture.View.MoveWorkspacePane("Output", "float"); capture.Capture();
                var floating = Assert.Single(capture.View.FloatingWorkspaceWindows);
                AssertHeaderAlignment(floating);
                Assert.Contains(floating.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Console);
                capture.View.ToggleTabMaximised("Output"); capture.Capture();
                Assert.Contains(floating.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Console);
                capture.View.ToggleTabMaximised("Output"); capture.Capture();
                Assert.Contains(floating.GetVisualDescendants().OfType<EditorIcon>(), i => i.Kind == EditorIconKind.Console);
                AssertHeaderAlignment(floating);
                var path = Environment.GetEnvironmentVariable("OASIS_ICON_VERIFICATION_OUTPUT");
                if (!string.IsNullOrWhiteSpace(path))
                { Directory.CreateDirectory(path); File.WriteAllBytes(Path.Combine(path, "workspace.png"), capture.Capture()); }
            }
            finally { Directory.Delete(root, true); }
        });
    }
    private static void AssertHeaderAlignment(Control host)
    {
        var icons = host.GetVisualDescendants().OfType<EditorIcon>()
            .Where(i => i.IsVisible && i.Parent is Panel && i.GetVisualAncestors().Any(c => c is Dock.Avalonia.Controls.ToolChromeControl)).ToArray();
        Assert.NotEmpty(icons);
        foreach (var icon in icons)
        {
            var parent = (Panel)icon.Parent!;
            var title = Assert.Single(parent.Children.OfType<TextBlock>());
            var lineCentre = title.Bounds.Y + title.TextLayout.Height / 2;
            Assert.True(Math.Abs(lineCentre - icon.Bounds.Center.Y) <= .75,
                $"{title.Text}: text line centre {lineCentre}, icon centre {icon.Bounds.Center.Y}");
        }
    }

}
