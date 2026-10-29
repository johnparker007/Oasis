using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class PanelControlUiTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ActualControlInspectorEditsInputAndResetsCustomGroupName(string theme)
    {
        await HeadlessTestHost.RunAsync(async()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"OasisControlUi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                var author=new AuthoringSession(root);author.CreateProject("Controls");author.CreatePanel("Panel");PanelControlTests.InstallControl(author,12);
                using var capture=new WorkspaceCapture(author);await capture.View.SynchronizeAssetDocumentsAsync();capture.Capture(theme:theme);
                var editor=capture.View.CommandView;var session=editor.CommandSession;var control=session.Groups.Single(g=>g.Control is not null);var section=control.ParentId!;
                Assert.Single(editor.InspectPanelHierarchyRows());Assert.Equal("Lamp (1)",editor.InspectPanelHierarchyRows().Single().Name);
                editor.SetPanelGroupExpanded(section,true);session.Select(control.Id);capture.Capture(theme:theme);
                var owner=(Window)TopLevel.GetTopLevel(capture.View)!;
                Assert.Contains(owner.GetVisualDescendants().OfType<Expander>(),e=>Equals(e.Header,"Lamp channels (12)"));
                var input=owner.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Text=="5");input.Text="7";
                owner.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Apply input")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("7",session.ImportedInputDefinitions.Single().ButtonNumber);session.Undo();Assert.Equal("5",session.ImportedInputDefinitions.Single().ButtonNumber);
                var output=Environment.GetEnvironmentVariable("OASIS_CONTROL_VERIFICATION_OUTPUT");
                if(output is not null){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,$"control-{theme.ToLowerInvariant()}.png"),capture.Capture(theme:theme));}
                session.Select(section);session.UpdateGroup(section,name:"Gamble ladder");capture.Capture(theme:theme);
                Assert.Equal("Gamble ladder (1)",editor.InspectPanelHierarchyRows().First().Name);
                owner.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Content,"Reset to Automatic")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Lamp (1)",editor.InspectPanelHierarchyRows().First().Name);session.Undo();Assert.Equal("Gamble ladder (1)",editor.InspectPanelHierarchyRows().First().Name);
                session.UpdateGroup(section,automaticName:true);session.Save();Assert.False(session.IsDirty);
            }
            finally{Directory.Delete(root,true);}
        });
    }
}
