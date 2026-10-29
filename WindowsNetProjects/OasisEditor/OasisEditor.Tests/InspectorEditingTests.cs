using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class InspectorEditingTests
{
    private static TextBox Field(Control host, string label) => host.GetVisualDescendants().OfType<Grid>()
        .Single(g => g.Children.OfType<TextBlock>().Any(t => t.Text == label) && g.Children.OfType<TextBox>().Any()).Children.OfType<TextBox>().Single();
    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    { window.KeyPress(key, modifiers, physical, key.ToString()); window.KeyRelease(key, modifiers, physical, key.ToString()); }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task PropertyEditsCommitOnceValidateAndKeepOtherFieldsAndSelection(string theme)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisInspectorEdit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Edit"); author.CreatePanel("Panel");
                var lamp = author.AddSpecialistElement("lamp", 20, 20); var other = author.AddRectangle(); author.Select(lamp); author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture(theme: theme);
                var session = capture.View.CommandSession; var window = (Window)TopLevel.GetTopLevel(capture.View)!;
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Content is string s && s.StartsWith("Apply"));
                var name = Field(window, "Name"); Assert.True(name.Focus());
                name.SelectAll(); window.KeyTextInput("Gamble ladder");
                Assert.Equal("Lamp", session.Elements.Single(e => e.Id == lamp).Name);
                Press(window, Key.Enter, PhysicalKey.Enter);
                Assert.Equal("Gamble ladder", session.Elements.Single(e => e.Id == lamp).Name);
                Press(window, Key.Z, PhysicalKey.Z, RawInputModifiers.Control); Assert.False(session.IsDirty);
                Press(window, Key.Z, PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
                Assert.Equal("Gamble ladder", session.Elements.Single(e => e.Id == lamp).Name);

                var x = Field(window, "X"); Assert.True(x.Focus()); x.Text = "-";
                Press(window, Key.Enter, PhysicalKey.Enter); Assert.True(DataValidationErrors.GetHasErrors(x));
                Assert.Equal(20, session.Elements.Single(e => e.Id == lamp).X);
                x.Text = "120"; Press(window, Key.Enter, PhysicalKey.Enter);
                Assert.Equal(120, session.Elements.Single(e => e.Id == lamp).X); Assert.False(DataValidationErrors.GetHasErrors(x));
                Assert.Same(x, Field(window, "X"));
                Assert.True(x.Focus()); x.Text = "999"; Press(window, Key.Escape, PhysicalKey.Escape); Assert.Equal("120", x.Text);
                Assert.Equal(120, session.Elements.Single(e => e.Id == lamp).X);

                var border = window.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Border"));
                border.IsChecked = true; Assert.True(session.GetSpecialistSettings(lamp)!.HasBorder);
                var display = Field(window, "Display Number"); display.Focus(); display.Text = "43"; Press(window, Key.Enter, PhysicalKey.Enter);
                Assert.Equal(43, session.GetSpecialistSettings(lamp)!.DisplayNumber); Assert.True(session.GetSpecialistSettings(lamp)!.HasBorder);
                session.Undo(); Assert.NotEqual(43, session.GetSpecialistSettings(lamp)!.DisplayNumber); Assert.True(session.GetSpecialistSettings(lamp)!.HasBorder);
                session.Undo(); Assert.NotEqual(true, session.GetSpecialistSettings(lamp)!.HasBorder);

                name = Field(window, "Name"); name.Focus(); name.Text = "Committed before selection";
                var row = window.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, other));
                var point = row.TranslatePoint(new Point(35, row.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                Assert.Equal(other, session.SelectedId); Assert.Equal("Committed before selection", session.Elements.Single(e => e.Id == lamp).Name);
                // Removed fields cannot commit into a different object.
                name.Text = "Stale draft"; InspectorTestInput.Commit(name);
                Assert.Equal("Rectangle", session.Elements.Single(e => e.Id == other).Name);
                var output = Environment.GetEnvironmentVariable("OASIS_INSPECTOR_EDIT_OUTPUT");
                if (output is not null) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"inspector-{theme}.png"), capture.Capture(theme: theme)); }
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task SaveAllIncludesAnUnfocusedUncommittedInspectorDraft()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisInspectorSave-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Save"); var path = author.CreatePanel("Panel"); author.AddRectangle(); author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var name = Field(capture.View, "Name"); name.Text = "Saved draft";
                await capture.View.SaveAllDocumentsAsync();
                Assert.Contains("Saved draft", File.ReadAllText(path)); Assert.False(capture.View.CommandSession.IsDirty);
                capture.View.CommandSession.Undo(); Assert.Equal("Rectangle", Assert.Single(capture.View.CommandSession.Elements).Name);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task PhysicalReelFieldsApplyWithoutButtonsAndUndoIndependently()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisInspectorReel-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Reel");
                using var capture = new WorkspaceCapture(author);
                var path = capture.View.Reels.Create("Reel"); await capture.View.OpenReelAsync(path); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var reel = capture.View.CommandView.Reels; var before = reel.Document.DiameterMm;
                var field = Field(capture.View, "Diameter mm"); field.Text = "150"; InspectorTestInput.Commit(field);
                Assert.Equal(150, reel.Document.DiameterMm);
                var width = reel.Document.WidthMm; field = Field(capture.View, "Width mm"); field.Text = (width + 10).ToString(System.Globalization.CultureInfo.InvariantCulture); InspectorTestInput.Commit(field);
                Assert.Equal(width + 10, reel.Document.WidthMm); Assert.Equal(150, reel.Document.DiameterMm);
                reel.Undo(); Assert.Equal(width, reel.Document.WidthMm); Assert.Equal(150, reel.Document.DiameterMm);
                reel.Undo(); Assert.Equal(before, reel.Document.DiameterMm);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task PrefabTransformColliderAndRigidbodyEditWithoutApplyingActionFields()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisInspectorPrefab-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Prefab");
                using var capture = new WorkspaceCapture(author);
                var path = capture.View.Prefabs.Create("Prefab"); await capture.View.OpenPrefab3DAsync(path); await capture.View.SynchronizeAssetDocumentsAsync();
                var prefab = capture.View.CommandView.Prefabs; var id = prefab.Document.Root.Id;
                prefab.AddComponent(id, Object3DComponentDefinition.ColliderKind); prefab.AddComponent(id, Object3DComponentDefinition.RigidbodyKind);
                await capture.View.WaitForSceneAsync(); capture.Capture();
                var x = Field(capture.View, "Position X"); x.Text = "12"; InspectorTestInput.Commit(x);
                Assert.Equal(12, prefab.Document.Root.Transform.Position.X);
                var y = Field(capture.View, "Position Y"); y.Text = "Uncommitted draft";
                await capture.View.WaitForSceneAsync(); Assert.Same(y, Field(capture.View, "Position Y")); Assert.Equal("Uncommitted draft", y.Text);
                y.Text = "0";
                var frame = capture.View.GetVisualDescendants().OfType<ComboBox>().Single(c => c.ItemsSource is string[] options && options.Contains("NodeLocal") && options.Contains("ModelLocal"));
                frame.SelectedItem = "NodeLocal";
                var colliderKind = capture.View.GetVisualDescendants().OfType<ComboBox>().Single(c => c.ItemsSource is string[] options && options.Contains("Box") && options.Contains("Sphere"));
                colliderKind.SelectedItem = "Box";
                Assert.True(prefab.Document.Root.Components.Single(c => c.Collider is not null).Collider!.Kind == Object3DColliderKind.Box,
                    string.Join("; ", DataValidationErrors.GetErrors(colliderKind) ?? []));
                var isTrigger = capture.View.GetVisualDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Is trigger"));
                isTrigger.IsChecked = true;
                Assert.True(prefab.Document.Root.Components.Single(c => c.Collider is not null).Collider!.IsTrigger,
                    string.Join("; ", DataValidationErrors.GetErrors(isTrigger) ?? []));
                prefab.Undo(); Assert.False(prefab.Document.Root.Components.Single(c => c.Collider is not null).Collider!.IsTrigger);
                Assert.Equal(12, prefab.Document.Root.Transform.Position.X);
                capture.Capture(); var mass = Field(capture.View, "Rigidbody mass");
                var mode = capture.View.GetVisualDescendants().OfType<ComboBox>().Single(c => c.ItemsSource is string[] options && options.Contains("Dynamic") && options.Contains("Kinematic"));
                mode.SelectedItem = "Dynamic"; mass.Text = "3"; InspectorTestInput.Commit(mass);
                Assert.Equal(3, prefab.Document.Root.Components.Single(c => c.Rigidbody is not null).Rigidbody!.Mass);
                var before = prefab.Document; var child = Field(capture.View, "Child name"); child.Text = "Not yet created"; InspectorTestInput.Commit(child);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(prefab.Document)); Assert.Empty(prefab.Document.Root.Children);
            }
            finally { Directory.Delete(root, true); }
        });
    }

    [Fact]
    public async Task ClosingIncludesPendingTextAndCancelKeepsItsUndoHistory()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisInspectorClose-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Close"); var path = author.CreatePanel("Panel"); author.AddRectangle(); author.Save();
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture();
                var name = Field(capture.View, "Name"); name.Focus(); name.Text = "Pending close edit";
                var close = capture.View.RequestCloseDocumentAsync("Panel2D", path);
                for (var i = 0; capture.View.UnsavedChangesWindow is null && i < 100; i++) await Task.Delay(10);
                var dialog = Assert.IsType<Window>(capture.View.UnsavedChangesWindow);
                dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UnsavedChangesCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.False(await close); Assert.Equal("Pending close edit", Assert.Single(capture.View.CommandSession.Elements).Name);
                capture.View.CommandSession.Undo(); Assert.False(capture.View.CommandSession.IsDirty);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
