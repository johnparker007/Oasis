using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class KeyboardRoutingVerificationTests
{
    [Theory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Meta)]
    public async Task WorkspaceShortcutsReachTheSceneWhenFocusIsOutsideTextInputs(RawInputModifiers modifier)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var folder = CreateProject(out var session, out var first, out var second);
            Window? window = null;
            try
            {
                using var view = new WorkspaceView(session);
                window = new Window { Content = view, Width = 1200, Height = 800 };
                window.Show();
                window.UpdateLayout();
                await view.SynchronizeAssetDocumentsAsync();
                session = view.CommandSession;
                view.Focusable = true;
                Assert.True(view.Focus());

                session.Select(session.Elements[0].Id);
                Press(window, Key.C, modifier);
                Assert.True(session.CanPasteClipboard);

                Press(window, Key.X, modifier);
                Assert.Equal(new[] { second }, session.Elements.Select(element => element.Id));
                Press(window, Key.Z, modifier);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.Y, modifier);
                Assert.Equal(new[] { second }, session.Elements.Select(element => element.Id));

                Press(window, Key.V, modifier);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.Z, modifier);
                Assert.Single(session.Elements);
                Press(window, Key.Y, modifier);
                Assert.Equal(2, session.Elements.Count);

                session.Select(session.Elements[0].Id);
                Press(window, Key.D, modifier);
                Assert.Equal(3, session.Elements.Count);
                Press(window, Key.Z, modifier);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.Y, modifier);
                Assert.Equal(3, session.Elements.Count);

                Press(window, Key.A, modifier);
                Press(window, Key.D, modifier);
                Assert.Equal(6, session.Elements.Count);
                Press(window, Key.Z, modifier);
                Assert.Equal(3, session.Elements.Count);

                Press(window, Key.A, modifier);
                Press(window, Key.Delete);
                Assert.Empty(session.Elements);
                Press(window, Key.Z, modifier);
                Assert.Equal(3, session.Elements.Count);

                Press(window, Key.S, modifier);
                Assert.False(session.IsDirty);
            }
            finally
            {
                window?.Close();
                Directory.Delete(folder, recursive: true);
            }
        });
    }

    [Fact]
    public void TextBoxEditingShortcutsStayWithTheFocusedTextBox()
    {
        HeadlessTestHost.Run(() =>
        {
            var folder = CreateProject(out var session, out var element, out _);
            try
            {
                var elementId = element;
                session.UpdateElement(element, name: "Pending scene edit");
                session.Select(element);
                using var capture = new WorkspaceCapture(session);
                _ = capture.Capture();
                var view = capture.View;
                session = view.CommandSession;
                var window = Assert.IsType<Window>(TopLevel.GetTopLevel(view));
                var textBox = view.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "Pending scene edit");
                Assert.True(textBox.Focus());
                textBox.Text = "";
                window.KeyTextInput("draft");
                Assert.Equal("draft", textBox.Text);

                Press(window, Key.C, RawInputModifiers.Meta);
                Assert.False(session.CanPasteClipboard);
                Press(window, Key.X, RawInputModifiers.Meta);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.V, RawInputModifiers.Meta);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.A, RawInputModifiers.Meta);
                Assert.Equal(new[] { element }, session.SelectedIds);
                Press(window, Key.Z, RawInputModifiers.Meta);
                Press(window, Key.Y, RawInputModifiers.Meta);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);
                Press(window, Key.D, RawInputModifiers.Meta);
                Assert.Equal(2, session.Elements.Count);

                textBox.Text = "draft";
                session.Select(element);

                Press(window, Key.A, RawInputModifiers.Control);
                Assert.Equal(0, textBox.SelectionStart);
                Assert.Equal("draft".Length, textBox.SelectionEnd);
                Assert.Equal(new[] { element }, session.SelectedIds);

                Press(window, Key.C, RawInputModifiers.Control);
                Assert.False(session.CanPasteClipboard);
                Press(window, Key.X, RawInputModifiers.Control);
                Assert.Equal("", textBox.Text);
                Assert.Equal(2, session.Elements.Count);
                Assert.Equal(new[] { element }, session.SelectedIds);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);

                Press(window, Key.V, RawInputModifiers.Control);
                Assert.Equal("draft", textBox.Text);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.Z, RawInputModifiers.Control);
                Assert.Equal("", textBox.Text);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);
                Press(window, Key.Y, RawInputModifiers.Control);
                Assert.Equal("draft", textBox.Text);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);

                textBox.SelectAll();
                Press(window, Key.D, RawInputModifiers.Control);
                Assert.Equal(2, session.Elements.Count);
                Press(window, Key.Delete);
                Assert.Empty(textBox.Text);
                Assert.Equal(2, session.Elements.Count);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);

                Press(window, Key.S, RawInputModifiers.Control);
                Assert.False(session.IsDirty);
                Assert.Equal("Pending scene edit", session.Elements.Single(e => e.Id == elementId).Name);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        });
    }

    [Fact]
    public async Task OutputSearchCopyShortcutCopiesSelectedTextInsteadOfLogRows()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            using var service = new OutputLogService();
            service.Add("output row", OutputSeverity.Info);
            using var pane = new OutputPaneControl(service);
            var window = new Window { Content = pane, Width = 900, Height = 500 };
            window.Show();
            window.UpdateLayout();

            var search = Assert.Single(pane.GetVisualDescendants().OfType<TextBox>());
            Assert.True(search.Focus());
            window.KeyTextInput("needle");
            search.SelectAll();

            Press(window, Key.C, RawInputModifiers.Control);
            var copied = await window.Clipboard!.TryGetTextAsync();

            Assert.Equal("needle", copied);
            window.Close();
        });
    }

    private static void Press(TopLevel window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physicalKey = Enum.Parse<PhysicalKey>(key.ToString());
        window.KeyPress(key, modifiers, physicalKey, key.ToString());
        window.KeyRelease(key, modifiers, physicalKey, key.ToString());
    }

    private static string CreateProject(out AuthoringSession session, out string first, out string second)
    {
        var folder = Path.Combine(Path.GetTempPath(), "OasisAvaloniaKeyboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var project = Path.Combine(folder, "Keyboard.oasisproj");
        File.WriteAllText(project, """{"name":"Keyboard test","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
        session = new AuthoringSession(folder);
        session.OpenProject(project);
        session.CreatePanel("Panel");
        first = session.AddRectangle();
        session.UpdateElement(first, name: "First");
        second = session.AddRectangle();
        session.UpdateElement(second, name: "Second", x: 180);
        session.Save();
        return folder;
    }
}
