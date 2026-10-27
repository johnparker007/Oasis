using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class ProjectAssetContextTests
{
    [Fact]
    public async Task PackageActionsRefuseCleanDockedAndFloatingDocuments()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisAssetContext_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Context");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                var manifest = session.CreatePanel("Panel");
                session.Save();

                var docked = Assert.Throws<InvalidOperationException>(() => view.GetProjectAssetPackageFingerprint(manifest));
                Assert.Contains("open in the", docked.Message, StringComparison.OrdinalIgnoreCase);

                await view.FloatDocumentAsync("Panel2D", manifest);
                var floating = Assert.Throws<InvalidOperationException>(() => view.GetProjectAssetPackageFingerprint(manifest));
                Assert.Contains("floating", floating.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    [Fact]
    public void PackageActionsRefuseAnyDirtyOpenDocument()
    {
        HeadlessTestHost.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisAssetContext_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Dirty Context");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                var manifest = session.CreatePanel("Panel");
                session.Save();
                session.AddRectangle();

                var dirty = Assert.Throws<InvalidOperationException>(() => view.GetProjectAssetPackageFingerprint(manifest));
                Assert.Contains("Save or undo", dirty.Message);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }
}
