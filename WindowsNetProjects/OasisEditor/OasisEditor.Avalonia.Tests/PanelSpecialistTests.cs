using System.Text.Json;
using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class PanelSpecialistTests
{
    [Fact]
    public void AddSpecialistElement_UsesNativeWpfDefaultsAndUndoHistory()
    {
        using var fixture = new Fixture();
        var session = fixture.Open();
        var lamp = session.AddSpecialistElement("lamp", 20, 30);
        var reel = session.AddSpecialistElement("reel", 40, 50);
        var seven = session.AddSpecialistElement("sevenSegment", 60, 70);
        var alpha = session.AddSpecialistElement("alpha", 80, 90);
        var vfd = session.AddSpecialistElement("vfdDotMatrix", 100, 110);

        Assert.Equal(new[] { "lamp", "reel", "sevenSegment", "alpha", "vfdDotMatrix" }, session.Elements.Where(element=>element.Id!="background").Select(element => element.Kind));
        Assert.Equal(new[] { vfd }, session.SelectedIds);
        Assert.Equal("#FF3030", session.GetSpecialistSettings(lamp)?.OnColorHex);
        Assert.Equal(24, session.GetSpecialistSettings(reel)?.Stops);
        Assert.Equal(1, session.GetSpecialistSettings(seven)?.DisplayNumber);
        Assert.Equal("led16seg", session.GetSpecialistSettings(alpha)?.SegmentDisplayType);
        Assert.Equal("#FFB000", session.GetSpecialistSettings(vfd)?.OnColorHex);

        session.Undo();
        Assert.Equal(5, session.Elements.Count);
    }

    [Fact]
    public void SpecialistSettings_UpdateAllFaceRelevantValuesAsOneCommand()
    {
        using var fixture = new Fixture();
        var session = fixture.Open();
        var id = session.AddSpecialistElement("reel", 1, 2);
        var initial = session.GetSpecialistSettings(id)!;
        var update = initial with
        {
            DisplayNumber = 3, Stops = 32, VisibleScale = .25, BandOffset = .1, IsReversed = true,
            ReelLampsEnabled = true, TopLampNumber = 6, MiddleLampNumber = 7, BottomLampNumber = 8
        };

        Assert.True(session.UpdateSpecialistSettings(id, update));
        Assert.Equal(update, session.GetSpecialistSettings(id));
        session.Save();
        var stored = JsonSerializer.Deserialize<Panel2DDocumentFile>(File.ReadAllText(fixture.PanelPath))!;
        var reel = Assert.Single(stored.Elements.Where(e=>e.ObjectId==id));
        Assert.Equal(3, reel.DisplayNumber);
        Assert.Equal(32, reel.Stops);
        Assert.Equal(new int?[] { 6, 7, 8 }, reel.ReelLamps!.Select(lamp => lamp.LampNumber));

        session.Undo();
        Assert.Equal(initial, session.GetSpecialistSettings(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateSpecialistSettings(id, update with { VisibleScale = 2 }));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisPanelSpecialistTests", Guid.NewGuid().ToString("N"));
        public string ProjectPath => Path.Combine(Root, "Components.oasisproj");
        public string PanelPath => Path.Combine(Root, "Assets", "Panel2D", "Panel", "asset.panel2d");
        public Fixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelPath)!); Directory.CreateDirectory(Path.Combine(Root, "Generated"));
            File.WriteAllText(ProjectPath, """{"version":9,"name":"Components","layout":{"assets":"Assets","generated":"Generated"}}""");
            File.WriteAllText(PanelPath, JsonSerializer.Serialize(new Panel2DDocumentFile
            {
                SchemaVersion = Panel2DDocumentStorage.CurrentSchemaVersion, Title = "Components",
                Elements = [new() { ObjectId = "background", Name = "Background", Kind = "rectangle", X = 0, Y = 0, Width = 320, Height = 240 }]
            }));
        }
        public AuthoringSession Open() { var session = new AuthoringSession(Root); session.OpenProject(ProjectPath); session.OpenPanel(PanelPath); return session; }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
