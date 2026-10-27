using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class WorkspaceLifecycleTests
{
    [Fact]
    public async Task CloseProjectRefusesDirtyDocumentsAndClosesCleanProject()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Project");
                session.CreatePanel("Panel");
                session.AddRectangle();
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;

                await Assert.ThrowsAsync<InvalidOperationException>(() => view.CloseProjectAsync());
                Assert.NotNull(session.ProjectFilePath);
                Assert.True(session.IsDirty);

                session.Undo();
                await view.CloseProjectAsync();
                Assert.Null(session.ProjectFilePath);
                Assert.Null(view.Machines.MachinePath);
            }
            finally { Directory.Delete(root, recursive: true); }
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task FaceValidationAndOpenSourcePanelUseSavedNativeSource()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisFaceLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Project");
                var panelPath = session.CreatePanel("Source");
                File.WriteAllText(panelPath, Panel2DDocumentStorage.Serialize("Source", string.Empty,
                    [new PanelElementFile { ObjectId = "box", Name = "Box", Kind = "rectangle", X = 0, Y = 0, Width = 5, Height = 5 }],
                    [new PanelFaceSourceShapeFile { Id = "front", Name = "Front", Points = [new(0, 0), new(40, 0), new(40, 30), new(0, 30)] }]));
                session.CloseActiveDocument();
                session.OpenPanel(panelPath);
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.Faces.CreateFromPanelFaceSourceShape("front", "Front Face");
                view.ShowFace();

                var diagnostics = view.ValidateActiveFace();
                Assert.DoesNotContain(diagnostics, d => d.Code is "Face.SourcePanel2D.Missing" or "Face.SourceShape.Missing");
                view.OpenFaceSourcePanel();

                Assert.Equal("Panel2D", view.InspectWorkspaceState().ActiveDocument);
                Assert.Equal(Path.GetFullPath(panelPath), Path.GetFullPath(session.PanelPath!));
            }
            finally { Directory.Delete(root, recursive: true); }
            await Task.CompletedTask;
        });
    }
}
