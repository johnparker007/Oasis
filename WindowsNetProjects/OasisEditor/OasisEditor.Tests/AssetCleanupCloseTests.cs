using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class AssetCleanupCloseTests
{
    [Theory]
    [InlineData("document")]
    [InlineData("project")]
    [InlineData("switch")]
    [InlineData("exit")]
    public async Task DiscardCleansOnlyReleasedImportsAcrossCloseEntryPoints(string operation)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var workspace = Path.Combine(Path.GetTempPath(), "OasisCleanupClose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                var author = new AuthoringSession(workspace); var project = author.CreateProject("Original"); var panel = author.CreatePanel("Panel");
                author.CloseActiveDocument();
                var other = new AuthoringSession(workspace); var target = other.CreateProject("Target");
                using var capture = new WorkspaceCapture(author);
                await capture.View.OpenNativeAssetAsync("Panel2D", panel);
                var session = capture.View.CommandSession;
                var source = Path.Combine(workspace, "empty.bin"); File.WriteAllBytes(source, []);
                Assert.True(session.ImportFml(source).Succeeded);
                var imported = Assert.Single(session.CaptureCreatedFmlImportDirectories());
                var orphan = Path.Combine(imported, "unused.png"); File.WriteAllText(orphan, "orphan");
                var keep = Path.Combine(Path.GetDirectoryName(project)!, "Assets/FmlImport/previous/keep.png");
                Directory.CreateDirectory(Path.GetDirectoryName(keep)!); File.WriteAllText(keep, "keep");
                session.AddRectangle(); await capture.View.SynchronizeAssetDocumentsAsync();
                Assert.True(capture.View.FindMenu("File", "Delete unreferenced assets…").IsEnabled);
                var task = operation switch
                {
                    "document" => capture.View.RequestCloseDocumentAsync("Panel2D", panel),
                    "project" => capture.View.RequestCloseProjectAsync(),
                    "switch" => capture.View.RequestOpenProjectAsync(target),
                    _ => capture.View.RequestEditorExitAsync()
                };
                for (var i = 0; i < 100 && capture.View.UnsavedChangesWindow is null; i++) await Task.Delay(10);
                Assert.NotNull(capture.View.UnsavedChangesWindow);
                capture.View.UnsavedChangesWindow!.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "UnsavedChangesDiscard")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(await task); Assert.False(File.Exists(orphan)); Assert.False(Directory.Exists(imported)); Assert.True(File.Exists(keep));
            }
            finally { Directory.Delete(workspace, true); }
        });
    }
}
