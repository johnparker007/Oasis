using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class PreviewWorkspaceTests
{
    [Fact]
    public async Task FocusLossReleasesAndRearmsAuthoredShortcutAndPointerMappingUsesRealVisualLink()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "OasisPreviewWorkspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var session = new AuthoringSession(folder); session.CreateProject("Project"); session.CreatePanel("Panel");
                var visual = session.AddRectangle(); session.UpdateElement(visual, x: 10, y: 10, width: 40, height: 40); session.Save();
                var backend = new NativePreviewSessionTests.Backend();
                using var view = new WorkspaceView(session, preview: new NativePreviewSession((_, _, _) => backend));
                view.Machines.OpenMachine(view.Machines.ListMachines().Single().ManifestPath);
                view.Machines.SetRuntime(EmulationRuntimeDefinition.Create(FruitMachinePlatformType.Impact));
                view.Machines.SetInputs([new() { Id = "door", Name = "Door", ButtonNumber = "7", KeyboardShortcut = "A", LinkedVisualElementId = Guid.Parse(visual) }]);
                view.Machines.Save(); view.ShowPanel();
                await view.StartNativePreviewAsync();
                Assert.Equal("door", view.ResolvePreviewPointerInput(new Point(20, 20)));
                Assert.Null(view.ResolvePreviewPointerInput(new Point(200, 200)));
                view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A });
                Assert.Equal(new[] { "door:True" }, backend.Calls);
                view.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
                Assert.Equal(new[] { "door:True", "door:False" }, backend.Calls);
                view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A });
                Assert.Equal(new[] { "door:True", "door:False", "door:True" }, backend.Calls);
                await view.Preview.ControlAsync("stop");
                Assert.Equal("door:False", backend.Calls.Last());
            }
            finally { Directory.Delete(folder, recursive: true); }
        });
    }
}
