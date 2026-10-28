using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class UnsavedChangesWorkflowTests
{
    [Fact]
    public async Task CloseDocumentCancelKeepsDiskAndUndoHistoryThenDiscardKeepsCreatedPackage()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("Close");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                var editor = view.GetFloatingDocumentView("Panel2D", project.PanelA) ?? view.CommandView;
                var session = editor.CommandSession;
                var baseline = File.ReadAllText(project.PanelA);
                var id = session.AddRectangle();
                session.UpdateElement(id, name: "Unsaved");

                var cancelTask = view.RequestCloseDocumentAsync("Panel2D", project.PanelA);
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                Assert.False(await cancelTask);
                Assert.Equal(baseline, File.ReadAllText(project.PanelA));
                Assert.True(session.IsDirty);
                Assert.True(session.CanUndo);
                Assert.Contains(session.Elements, element => element.Name == "Unsaved");
                Assert.Contains(view.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelA));

                var created = project.PanelC;
                await view.OpenNativeAssetAsync("Panel2D", created);
                var createdBaseline = File.ReadAllText(created);
                var createdSession = view.GetFloatingDocumentView("Panel2D", created).CommandSession;
                var newId = createdSession.AddRectangle();
                createdSession.UpdateElement(newId, name: "Throw away edit");
                var discardTask = view.RequestCloseDocumentAsync("Panel2D", created);
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesDiscard");
                Assert.True(await discardTask);
                Assert.Equal(createdBaseline, File.ReadAllText(created));
                Assert.Contains(view.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelA));
                Assert.DoesNotContain(view.InspectOpenDocuments(), item => PathEquals(item.Path, created));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task CloseProjectSaveAllAndDiscardAllApplyToEveryModifiedAsset()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("CloseAll");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await OpenAndEditBoth(view, project);
                var saveTask = view.RequestCloseProjectAsync();
                await WaitForDialog(view);
                Assert.Equal(2, Assert.IsType<UnsavedChangesRequest>(view.PendingUnsavedChanges).Assets.Count);
                Click(view.UnsavedChangesWindow, "UnsavedChangesSave");
                Assert.True(await saveTask);
                Assert.Null(view.CommandSession.ProjectFilePath);
                Assert.Contains("Saved A", File.ReadAllText(project.PanelA));
                Assert.Contains("Saved B", File.ReadAllText(project.PanelB));

                var session = OpenProject(project);
                using var secondCapture = new WorkspaceCapture(session);
                view = secondCapture.View;
                await OpenAndEditBoth(view, project, "Discarded");
                var beforeA = File.ReadAllText(project.PanelA);
                var beforeB = File.ReadAllText(project.PanelB);
                var discardTask = view.RequestCloseProjectAsync();
                await WaitForDialog(view);
                Assert.Equal(2, Assert.IsType<UnsavedChangesRequest>(view.PendingUnsavedChanges).Assets.Count);
                Click(view.UnsavedChangesWindow, "UnsavedChangesDiscard");
                Assert.True(await discardTask);
                Assert.Null(view.CommandSession.ProjectFilePath);
                Assert.Equal(beforeA, File.ReadAllText(project.PanelA));
                Assert.Equal(beforeB, File.ReadAllText(project.PanelB));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task OpenProjectCancelAndInvalidTargetPreserveCurrentEditsAndSwitchCanDiscardOrSave()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var current = CreateProject("OpenCurrent");
            var target = CreateProjectAtRoot(current.Root, "OpenTarget");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(current));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", current.PanelA);
                var editor = view.CommandView;
                var id = editor.CommandSession.AddRectangle();
                editor.CommandSession.UpdateElement(id, name: "Pending current");
                var disk = File.ReadAllText(current.PanelA);

                await Assert.ThrowsAnyAsync<Exception>(() => view.RequestOpenProjectAsync(Path.Combine(current.Root, "missing.oasisproj")));
                Assert.Equal(disk, File.ReadAllText(current.PanelA));

                var cancelTask = view.RequestOpenProjectAsync(target.ProjectFile);
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                Assert.False(await cancelTask);
                Assert.Equal(Path.GetFullPath(current.ProjectFile), Path.GetFullPath(view.CommandSession.ProjectFilePath!));
                Assert.True(editor.CommandSession.IsDirty);

                var discardTask = view.RequestOpenProjectAsync(target.ProjectFile);
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesDiscard");
                Assert.True(await discardTask);
                Assert.Equal(Path.GetFullPath(target.ProjectFile), Path.GetFullPath(view.CommandSession.ProjectFilePath!));
                Assert.Equal(disk, File.ReadAllText(current.PanelA));

                Assert.True(await view.RequestOpenProjectAsync(current.ProjectFile));
                await view.OpenNativeAssetAsync("Panel2D", current.PanelA);
                var saveEditor = view.GetFloatingDocumentView("Panel2D", current.PanelA);
                saveEditor.CommandSession.UpdateElement(saveEditor.CommandSession.AddRectangle(), name: "Saved before switch");
                var saveTask = view.RequestOpenProjectAsync(target.ProjectFile);
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesSave");
                Assert.True(await saveTask);
                Assert.Equal(Path.GetFullPath(target.ProjectFile), Path.GetFullPath(view.CommandSession.ProjectFilePath!));
                Assert.Contains("Saved before switch", File.ReadAllText(current.PanelA));
            }
            finally
            {
                Directory.Delete(current.Root, recursive: true);
            }
        });
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task EditorWindowCloseUsesSaveAndDiscardDialogChoices(bool discard,bool maximised)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("Exit");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
                view.CommandView.CommandSession.UpdateElement(view.CommandView.CommandSession.AddRectangle(), name: "Exit edit");
                await view.FloatDocumentAsync("Panel2D",project.PanelA);
                if(maximised)
                {
                    var id=view.InspectWorkspaceState().AssetLayout!.Documents.Single().Id;
                    view.ToggleTabMaximised(id);capture.CaptureFloatingWindows();
                }
                var window = (Window)global::Avalonia.Controls.TopLevel.GetTopLevel(view)!;
                view.AttachEditorCloseHandler(window);

                window.Close();
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesCancel");
                await WaitUntil(() => !view.IsCloseWorkflowActive);
                Assert.True(view.HasUnsavedChanges);
                Assert.Single(view.FloatingWorkspaceWindows);
                Assert.Equal(maximised,view.InspectTabPresentation().MaximisedPaneId is not null);

                var exitTask = view.RequestEditorExitAsync();
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, discard ? "UnsavedChangesDiscard" : "UnsavedChangesSave");
                Assert.True(await exitTask);
                Assert.Empty(view.FloatingWorkspaceWindows);
                Assert.False(window.IsVisible);
                Assert.Equal(!discard,File.ReadAllText(project.PanelA).Contains("Exit edit"));
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailureReportsErrorAndLeavesEveryDocumentOpen(bool failSecond)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var project = CreateProject("SaveFailure");
            try
            {
                using var capture = new WorkspaceCapture(OpenProject(project));
                var view = capture.View;
                await OpenAndEditBoth(view, project, "Pending");
                var docs = view.InspectOpenDocuments().ToArray();
                var failedPath=failSecond ? project.PanelB : project.PanelA;
                File.WriteAllText(failedPath, "externally changed on disk");

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                view.CloseErrorDialogOpened += dialog =>
                {
                    dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name=="UnsavedChangesErrorOK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    completion.TrySetResult();
                };
                var closeTask = view.RequestCloseProjectAsync();
                await WaitForDialog(view);
                Click(view.UnsavedChangesWindow, "UnsavedChangesSave");
                await completion.Task;
                Assert.False(await closeTask);
                Assert.NotNull(view.CloseWorkflowError);
                Assert.Equal(docs.Length, view.InspectOpenDocuments().Count);
                Assert.Contains(view.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelA));
                Assert.Contains(view.InspectOpenDocuments(), item => PathEquals(item.Path, project.PanelB));
                Assert.True(view.InspectOpenDocuments().Single(item => PathEquals(item.Path, failedPath)).IsDirty);
                if(failSecond)Assert.False(view.InspectOpenDocuments().Single(item=>PathEquals(item.Path,project.PanelA)).IsDirty);
                Assert.True(view.HasUnsavedChanges);
            }
            finally { Directory.Delete(project.Root, recursive: true); }
        });
    }

    [Fact]
    public async Task DiscardDoesNotLoseEditsMadeWhileThePromptIsOpen()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var project=CreateProject("ConcurrentEdit");
            try
            {
                using var capture=new WorkspaceCapture(OpenProject(project));
                var view=capture.View;
                await view.OpenNativeAssetAsync("Panel2D",project.PanelA);
                var session=view.CommandSession;
                var id=session.AddRectangle();session.UpdateElement(id,name:"Original pending edit");
                var disk=File.ReadAllText(project.PanelA);
                view.CloseErrorDialogOpened+=dialog=>Click(dialog,"UnsavedChangesErrorOK");
                var task=view.RequestCloseDocumentAsync("Panel2D",project.PanelA);
                await WaitForDialog(view);
                // Models can still be edited by automation while the UI is modal.
                session.UpdateElement(id,name:"Newer automation edit");
                Click(view.UnsavedChangesWindow,"UnsavedChangesDiscard");
                Assert.False(await task);
                Assert.Contains("changed while confirming",view.CloseWorkflowError);
                Assert.Equal("Newer automation edit",session.Elements.Single(element=>element.Id==id).Name);
                Assert.True(session.IsDirty);Assert.True(session.CanUndo);
                Assert.Equal(disk,File.ReadAllText(project.PanelA));
                Assert.Contains(view.InspectOpenDocuments(),d=>PathEquals(d.Path,project.PanelA));
            }
            finally{Directory.Delete(project.Root,true);}
        });
    }

    [Fact]
    public async Task WorkspaceAutosaveConflictDoesNotTrapTheEditorAfterAssetsAreSaved()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var project=CreateProject("LayoutConflict");
            try
            {
                using var capture=new WorkspaceCapture(OpenProject(project));
                var view=capture.View;
                await view.OpenNativeAssetAsync("Panel2D",project.PanelA);
                var settings=Path.Combine(project.Root,"workspace.json");
                view.ConfigureWorkspacePersistence(settings,restore:false);view.SaveWorkspace();
                File.AppendAllText(settings," ");
                var externalSettings=File.ReadAllText(settings);
                var session=view.CommandSession;
                session.UpdateElement(session.AddRectangle(),name:"Saved on exit");
                var task=view.RequestEditorExitAsync();
                await WaitForDialog(view);Click(view.UnsavedChangesWindow,"UnsavedChangesSave");
                Assert.True(await task);
                Assert.Contains("Saved on exit",File.ReadAllText(project.PanelA));
                Assert.Equal(externalSettings,File.ReadAllText(settings));
                Assert.False(((Window)TopLevel.GetTopLevel(view)!).IsVisible);
            }
            finally{Directory.Delete(project.Root,true);}
        });
    }

    private static async Task OpenAndEditBoth(WorkspaceView view, TestProject project, string prefix = "Saved")
    {
        await view.OpenNativeAssetAsync("Panel2D", project.PanelA);
        await view.OpenNativeAssetAsync("Panel2D", project.PanelB);
        foreach (var path in new[] { project.PanelA, project.PanelB })
        {
            var editor = view.GetFloatingDocumentView("Panel2D", path) ?? view.CommandView;
            var session = editor.CommandSession;
            session.UpdateElement(session.AddRectangle(), name: $"{prefix} {Path.GetFileName(Path.GetDirectoryName(path))}");
        }
        await view.SynchronizeAssetDocumentsAsync();
    }

    private static void Click(Window? dialog, string name)
    {
        var actualDialog = Assert.IsType<Window>(dialog);
        actualDialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name==name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task WaitForDialog(WorkspaceView view)
    {
        for (var i = 0; i < 100 && view.UnsavedChangesWindow is null; i++)
            await Task.Delay(10);
        Assert.True(view.UnsavedChangesWindow is not null,$"Prompt missing: dirty={view.HasUnsavedChanges}, active={view.IsCloseWorkflowActive}, error={view.CloseWorkflowError}, visible={TopLevel.GetTopLevel(view)?.IsVisible}, docs={view.InspectOpenDocuments().Count}, floats={view.FloatingWorkspaceWindows.Count}");
    }

    private static async Task WaitUntil(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++) await Task.Delay(10);
        Assert.True(predicate());
    }

    private static TestProject CreateProject(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisUnsavedWorkflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return CreateProjectAtRoot(root, name);
    }

    private static TestProject CreateProjectAtRoot(string root, string name)
    {
        var author = new AuthoringSession(root);
        var projectFile = author.CreateProject(name);
        var panelA = author.CreatePanel("A"); author.AddRectangle(); author.Save();
        var panelB = author.CreatePanel("B"); author.AddRectangle(); author.Save();
        var panelC = author.CreatePanel("C"); author.Save();
        return new TestProject(root, projectFile, panelA, panelB, panelC);
    }

    private static AuthoringSession OpenProject(TestProject project)
    {
        var session = new AuthoringSession(project.Root);
        session.OpenProject(project.ProjectFile);
        return session;
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record TestProject(string Root, string ProjectFile, string PanelA, string PanelB, string PanelC);
}
