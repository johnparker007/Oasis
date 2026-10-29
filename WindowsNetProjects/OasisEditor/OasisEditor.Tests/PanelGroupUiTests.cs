using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class PanelGroupUiTests
{
    [Fact]
    public async Task RoutedDropsReparentAndReorderAndContextMenuExposesGroupActions()
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisGroupDrop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var author=new AuthoringSession(root);author.CreateProject("Drops");author.CreatePanel("Panel");
                var a=author.AddRectangle();var first=author.CreateGroup("First");
                var b=author.AddRectangle();var second=author.CreateGroup("Second");author.Save();
                using var capture=new WorkspaceCapture(author);await capture.View.SynchronizeAssetDocumentsAsync();capture.Capture();
                var editor=capture.View.CommandView;var session=editor.CommandSession;var owner=(Window)TopLevel.GetTopLevel(capture.View)!;
                StackPanel Target()=>owner.GetVisualDescendants().OfType<StackPanel>().Single(p=>Equals(p.Tag,second));
                var target=Target();target.ContextMenu!.Open(target);
                var labels=target.ContextMenu.Items.OfType<MenuItem>().Select(i=>i.Header).ToArray();
                Assert.Contains("Group Selection",labels);Assert.Contains("Ungroup",labels);Assert.Contains("Move to Group",labels);target.ContextMenu.Close();
                var original=session.ViewportElements.Single(e=>e.Id==a);session.Select(a);
                using var data=new DataTransfer();var item=new DataTransferItem();item.Set(DataFormat.CreateInProcessFormat<WorkspaceView>("Oasis.PanelHierarchy"),editor);data.Add(item);
                var drop=new DragEventArgs(DragDrop.DropEvent,data,target,new Point(10,target.Bounds.Height/2),KeyModifiers.None);target.RaiseEvent(drop);
                Assert.True(drop.Handled);Assert.Equal(second,session.Elements.Single(e=>e.Id==a).ParentId);
                var moved=session.ViewportElements.Single(e=>e.Id==a);Assert.Equal(original.X,moved.X,8);Assert.Equal(original.Y,moved.Y,8);
                session.Undo();Assert.Equal(first,session.Elements.Single(e=>e.Id==a).ParentId);capture.Capture();target=Target();session.Select(a);
                target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent,data,target,new Point(10,target.Bounds.Height-1),KeyModifiers.None));
                Assert.Null(session.Elements.Single(e=>e.Id==a).ParentId);Assert.Equal(new[]{b,a},session.Elements.Select(e=>e.Id));
                session.Undo();Assert.Equal(new[]{a,b},session.Elements.Select(e=>e.Id));
            }
            finally{Directory.Delete(root,true);}
        });
    }
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task CollapsedGroupsExpandWithoutDirtyingAndInspectorIconIsUndoable(string theme)
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisGroupUi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var author=new AuthoringSession(root);author.CreateProject("Groups");author.CreatePanel("Panel");
                var a=author.AddSpecialistElement("lamp",20,20);var b=author.AddSpecialistElement("lamp",120,20);
                author.SelectElements([a,b]);var group=author.CreateGroup("Gamble ladder",icon:"Lamp");author.Save();
                using var capture=new WorkspaceCapture(author);await capture.View.SynchronizeAssetDocumentsAsync();capture.Capture(theme:theme);
                var editor=capture.View.CommandView;var session=editor.CommandSession;
                Assert.Single(editor.InspectPanelHierarchyRows());Assert.Equal("group",editor.InspectPanelHierarchyRows()[0].Kind);Assert.False(session.IsDirty);
                var owner=(Window)TopLevel.GetTopLevel(capture.View)!;
                var toggle=owner.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="TogglePanelGroup");
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Equal(3,editor.InspectPanelHierarchyRows().Count);Assert.False(session.IsDirty);
                session.Select(group);capture.Capture(theme:theme);
                var icon=owner.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="PanelGroupIcon");icon.SelectedItem="Reel";
                owner.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Apply")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Reel",session.Groups.Single().Icon);session.Undo();Assert.Equal("Lamp",session.Groups.Single().Icon);Assert.False(session.IsDirty);
                var output=Environment.GetEnvironmentVariable("OASIS_GROUP_VERIFICATION_OUTPUT");
                if(output is not null){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,$"groups-{theme.ToLowerInvariant()}.png"),capture.Capture(theme:theme));}
                session.Select(group);var settings=Path.Combine(root,"workspace.json");capture.View.ConfigureWorkspacePersistence(settings,restore:false);capture.View.SaveWorkspace();
                Assert.Contains(group,capture.View.InspectWorkspaceState().AssetLayout!.Documents.Single().ExpandedGroups!);
                editor.SetPanelGroupExpanded(group,false);capture.View.RestoreWorkspace();await capture.View.WaitForSceneAsync();
                Assert.Contains(group,capture.View.CommandView.ExpandedPanelGroups);Assert.Equal(group,capture.View.CommandView.CommandSession.SelectedId);
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
