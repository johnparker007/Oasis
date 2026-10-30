using System.Text.Json;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FmlSingletonSectionTests
{
    [Fact]
    public void SingletonSectionsStayAtRootWhileConsecutiveMultiObjectSectionsStayGrouped()
    {
        var source = new[] { PanelElementKind.Background, PanelElementKind.Alpha, PanelElementKind.Reel, PanelElementKind.Reel, PanelElementKind.Lamp, PanelElementKind.Lamp, PanelElementKind.Alpha }
            .Select((kind, i) => new PanelElementModel { ObjectId = Guid.NewGuid().ToString("N"), Kind = kind, Name = kind.ToString(), X = 20 + i * 5, Y = 40, Width = 100, Height = 40,
                SourceComponentIndex = i == 5 ? 4 : i, SharedSourceSetId = kind == PanelElementKind.Lamp ? "lamp" : null }).ToArray();
        var root = Path.Combine(Path.GetTempPath(), "OasisSingletonImport-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var session = new AuthoringSession(root); session.CreateProject("Import"); var path = session.CreatePanel("Panel");
            var original = JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(path))!;
            var result = AuthoringSession.AttachFmlControls(original, source, []);
            Assert.Equal(source.Select(s => Panel2DDocumentStorage.SerializeElementKind(s.Kind)), result.Elements.Select(e => e.Kind));
            Assert.Null(result.Elements.Single(e => e.Kind == "background").ParentId);
            Assert.All(result.Elements.Where(e => e.Kind == "alpha"), e => Assert.Null(e.ParentId));
            var section = Assert.Single(result.Groups.Where(g => g.Control is null));
            Assert.Equal(2, result.Elements.Count(e => e.ParentId == section.Id)); Assert.All(result.Elements.Where(e => e.ParentId == section.Id), e => Assert.Equal("reel", e.Kind));
            var control = Assert.Single(result.Groups.Where(g => g.Control is not null)); Assert.Null(control.ParentId);
            Assert.Equal(2, result.Elements.Count(e => e.ParentId == control.Id));
            Assert.Equal(result.Elements.Select(e => e.ObjectId), result.HierarchyOrder.Where(id => result.Elements.Any(e => e.ObjectId == id)));
            // Reopening uses the imported tree; explicitly authored singleton groups are untouched.
            File.WriteAllText(path, JsonSerializer.Serialize(result)); session.OpenProject(session.ProjectFilePath!); session.OpenPanel(path);
            session.Select(session.Elements.First(e => e.Kind == "alpha").Id); var manual = session.CreateGroup("My section");
            Assert.Single(session.Elements.Where(e => e.ParentId == manual)); session.Undo(); Assert.DoesNotContain(session.Groups, g => g.Id == manual);
            session.Redo(); Assert.Contains(session.Groups, g => g.Id == manual);
        }
        finally { Directory.Delete(root, true); }
    }
}
