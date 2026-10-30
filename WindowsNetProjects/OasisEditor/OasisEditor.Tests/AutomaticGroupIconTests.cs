using System.Text.Json;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class AutomaticGroupIconTests
{
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ImportedGroupIconsMatchTheirChildrenAndPreserveOverridesAndHistory(string theme)
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisGroupIcons-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var author = new AuthoringSession(root); author.CreateProject("Icons"); var path = author.CreatePanel("Imported");
                var sources = new[] { PanelElementKind.Alpha, PanelElementKind.Alpha, PanelElementKind.Background, PanelElementKind.Background, PanelElementKind.VfdDotMatrix, PanelElementKind.VfdDotMatrix }
                    .Select((kind, i) => new PanelElementModel { ObjectId = Guid.NewGuid().ToString("N"), Kind = kind, Name = kind.ToString(),
                        Width = 100, Height = 40, X = 20, Y = 20 + i * 50, SourceComponentIndex = i, IsTransformLocked = kind == PanelElementKind.Background,
                        SegmentDisplayType = i == 0 ? "led14seg" : "led16seg" }).ToArray();
                var file = JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(path))!;
                File.WriteAllText(path, JsonSerializer.Serialize(AuthoringSession.AttachFmlControls(file, sources, [])));
                var baseline = File.ReadAllText(path);
                author.OpenProject(author.ProjectFilePath!); author.OpenPanel(path);
                using var capture = new WorkspaceCapture(author); await capture.View.SynchronizeAssetDocumentsAsync(); capture.Capture(theme: theme);
                var editor = capture.View.CommandView; var session = editor.CommandSession;
                Assert.Equal(3, editor.InspectPanelHierarchyRows().Count); // Imported sections start collapsed.
                var groups = session.Groups.ToDictionary(g => session.GroupLabel(g.Id).Type);
                var expected = new[] { ("Alpha display", EditorIconKind.SegmentedAlpha), ("Background", EditorIconKind.Background), ("VFD display", EditorIconKind.DotMatrixAlpha) };
                foreach (var (type, kind) in expected)
                {
                    var group = groups[type]; Assert.Equal(kind.ToString(), session.GroupLabel(group.Id).Icon);
                    editor.SetPanelGroupExpanded(group.Id, true);
                }
                capture.Capture(theme: theme);
                foreach (var (type, kind) in expected)
                    foreach (var id in session.Elements.Where(e => e.ParentId == groups[type].Id).Select(e => e.Id).Prepend(groups[type].Id))
                    {
                        var row = capture.View.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, id));
                        Assert.Equal(kind, Assert.Single(row.GetVisualDescendants().OfType<EditorIcon>().Where(i => i.Kind is not EditorIconKind.Lock and not EditorIconKind.Hidden)).Kind);
                        Assert.Equal(id != groups[type].Id && type == "Background" ? 1 : 0,
                            row.Children.OfType<Button>().Count(b => b.Name == "HierarchyLock" && !b.Classes.Contains("inactive")));
                        Assert.DoesNotContain(row.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains('▣') == true);
                        if (id != groups[type].Id && type == "Background")
                        {
                            Assert.True(session.Elements.Single(e => e.Id == id).Locked);
                            Assert.Equal("Transform locked", ToolTip.GetTip(row));
                            Assert.Equal("Transform locked", global::Avalonia.Automation.AutomationProperties.GetHelpText(row));
                        }
                    }
                Assert.False(session.IsDirty); Assert.Equal(baseline, File.ReadAllText(path));
                var output = Environment.GetEnvironmentVariable("OASIS_GROUP_ICON_OUTPUT");
                if (output is not null) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"group-icons-{theme}.png"), capture.Capture(theme: theme)); }

                var alpha = groups["Alpha display"].Id; var background = groups["Background"].Id;
                session.UpdateGroup(alpha, locked: true); capture.Capture(theme: theme);
                var child = session.Elements.First(e => e.ParentId == alpha).Id;
                var childRow = capture.View.GetVisualDescendants().OfType<StackPanel>().Single(p => Equals(p.Tag, child));
                Assert.Equal("Transform locked by parent group", ToolTip.GetTip(childRow));
                Assert.Single(childRow.GetVisualDescendants().OfType<EditorIcon>().Where(i => i.Kind == EditorIconKind.Lock));
                session.Undo(); Assert.False(session.IsDirty);
                session.UpdateGroup(alpha, icon: "Hierarchy", automaticIcon: false);
                Assert.Equal("Hierarchy", session.GroupLabel(alpha).Icon); // Explicit generic override remains generic.
                session.Undo(); Assert.Equal("SegmentedAlpha", session.GroupLabel(alpha).Icon);
                session.Select(session.Elements.First(e => e.ParentId == alpha).Id); session.ReparentSelection(background);
                Assert.Equal("Hierarchy", session.GroupLabel(background).Icon); Assert.Equal("Mixed", session.GroupLabel(background).Type);
                session.Undo(); Assert.Equal("Background", session.GroupLabel(background).Icon);
                session.UpdateGroup(background, icon: "DotMatrixAlpha");
                Assert.Equal("DotMatrixAlpha", session.GroupLabel(background).Icon);
                session.Save();
                var reopened = new AuthoringSession(root); reopened.OpenProject(session.ProjectFilePath!); reopened.OpenPanel(path);
                Assert.Equal("DotMatrixAlpha", reopened.GroupLabel(background).Icon);
                session.Undo(); Assert.Equal("Background", session.GroupLabel(background).Icon);
                session.Redo(); Assert.Equal("DotMatrixAlpha", session.GroupLabel(background).Icon);
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
