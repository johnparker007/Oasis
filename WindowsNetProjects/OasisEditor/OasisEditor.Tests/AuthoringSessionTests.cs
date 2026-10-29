using System.Text.Json;
using System.Text.Json.Nodes;
using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class AuthoringSessionTests
{
    [Fact]
    public void RenderDocumentRetainsFullSpecialistModelInsteadOfViewportProjection()
    {
        var folder = Path.Combine(Path.GetTempPath(), "OasisRenderModel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var project = Path.Combine(folder, "Render.oasisproj");
            File.WriteAllText(project, """{"name":"Render model","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            var session = new AuthoringSession(folder);
            session.OpenProject(project);
            var manifest = session.CreatePanel("Panel");
            var reel = new PanelElementFile
            {
                ObjectId = "reel-main", Name = "Main reel", Kind = "reel", X = 13, Y = 17, Width = 90, Height = 160,
                AssetPath = "Assets/Reels/Main.png", SecondaryAssetPath = "Assets/Reels/Transmission.png", IsVisible = true,
                IsReversed = true, Stops = 96, VisibleScale = .64, BandOffset = -.125, ReelLampsEnabled = true,
                ReelLamps = [new PanelElementReelLampFile { Position = "middle", LampNumber = 17, LocalVerticalCenter = .43, Radius = .1, Intensity = .8 }],
                IsOpaqueReel = true, ReelLampTransmissionMaskAssetPath = "Assets/Reels/Transmission.png",
                SourceComponentIndex = 4, SourceElementIndex = 12, SharedSourceSetId = "shared-reel", SharedSourceSetCount = 3,
                SourceBlend = true,
                Native = new PanelElementNativeFile { TextBoxFontName = "MFME", TextBoxFontStyle = "Bold", TextBoxFontSize = "12", DisplayColorHex = "#FFB000" }
            };
            File.WriteAllText(manifest, Panel2DDocumentStorage.Serialize("Panel", "Full renderer state", [reel]));
            session.OpenProject(project);
            session.OpenPanel(manifest);

            var model = Assert.Single(session.RenderDocument!.Elements);
            Assert.Equal(PanelElementKind.Reel, model.Kind);
            Assert.Equal("Assets/Reels/Main.png", model.AssetPath);
            Assert.Equal("Assets/Reels/Transmission.png", model.SecondaryAssetPath);
            Assert.Equal("MFME", model.TextBoxFontName);
            Assert.Equal("Bold", model.TextBoxFontStyle);
            Assert.Equal("12", model.TextBoxFontSize);
            Assert.Equal("#FFB000", model.DisplayColorHex);
            Assert.True(model.IsReversed);
            Assert.Equal(96, model.Stops);
            Assert.Equal(.64, model.VisibleScale);
            Assert.Equal(-.125, model.BandOffset);
            Assert.True(model.ReelLampsEnabled);
            Assert.Equal(17, Assert.Single(model.ReelLamps).LampNumber);
            Assert.Equal(.43, model.ReelLamps[0].LocalVerticalCenter);
            Assert.True(model.IsOpaqueReel);
            Assert.Equal("Assets/Reels/Transmission.png", model.ReelLampTransmissionMaskAssetPath);
            Assert.Equal(4, model.SourceComponentIndex);
            Assert.Equal(12, model.SourceElementIndex);
            Assert.Equal("shared-reel", model.SharedSourceSetId);
            Assert.Equal(3, model.SharedSourceSetCount);
            Assert.True(model.SourceBlend);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void RuntimeOutputSnapshotCopiesLiveValuesAndProducesAStableFrameFingerprint()
    {
        var levels = new[] { 1d, .5d };
        IReadOnlyDictionary<string, IReadOnlyList<double>> brightness = new Dictionary<string, IReadOnlyList<double>>
        { ["display:1"] = levels };
        var lamps = new Dictionary<string, double> { ["lamp:1"] = .25 };
        var snapshot = new RuntimeOutputSnapshot(lampIntensities: lamps, segmentBrightness: brightness);
        var fingerprint = snapshot.Fingerprint;

        levels[0] = 0;
        lamps["lamp:1"] = 0;

        Assert.Equal(.25, snapshot.LampIntensities["lamp:1"]);
        Assert.Equal(1, snapshot.SegmentBrightness["display:1"][0]);
        Assert.Equal(fingerprint, new RuntimeOutputSnapshot(
            lampIntensities: new Dictionary<string, double> { ["lamp:1"] = .25 },
            segmentBrightness: new Dictionary<string, IReadOnlyList<double>> { ["display:1"] = new[] { 1d, .5d } }).Fingerprint);
        Assert.NotEqual(fingerprint, new RuntimeOutputSnapshot(lampIntensities: new Dictionary<string, double> { ["lamp:1"] = 1 }).Fingerprint);
    }

    [Fact]
    public void ProjectPathsResolveAgainstTheExplicitWorkspace()
    {
        using var project = new TestProject();
        var session = new AuthoringSession(project.Root);
        session.OpenProject(Path.GetFileName(project.ProjectFilePath));
        Assert.Equal(project.ProjectFilePath, session.ProjectFilePath);
        session.OpenProject(project.Root);
        Assert.Equal(project.ProjectFilePath, session.ProjectFilePath);
    }

    [Fact]
    public void UpdateUndoRedoAndSave_TrackTheExactSavedPoint()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;

        Assert.True(session.UpdateElement(id, name: "Renamed"));
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);

        session.Undo();
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);

        session.Redo();
        Assert.True(session.IsDirty);
        session.Save();
        Assert.False(session.IsDirty);

        session.Undo();
        Assert.True(session.IsDirty);
        session.Redo();
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void NoOpEdit_DoesNotDiscardRedoHistory()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;
        session.UpdateElement(id, name: "First edit");
        session.UpdateElement(id, x: 31);
        session.Undo();

        Assert.True(session.CanRedo);
        Assert.False(session.UpdateElement(id, name: "First edit"));
        Assert.True(session.CanRedo);
        session.Redo();
        Assert.Equal(31, Assert.Single(session.Elements).X);
    }

    [Fact]
    public void InvalidEdit_IsAtomicAndDoesNotEnterHistory()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateElement(id, name: "Must not apply", width: -1));

        var element = Assert.Single(session.Elements);
        Assert.Equal("Reel", element.Name);
        Assert.Equal(64, element.Width);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankElementName_IsRejectedWithoutDirtyingOrAddingHistory(string name)
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;

        Assert.Throws<ArgumentException>(() => session.UpdateElement(id, name: name));

        Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void ZeroDimensionEdit_IsRejectedAsInvalidStorageGeometry()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.UpdateElement(id, width: 0));

        Assert.Equal(64, Assert.Single(session.Elements).Width);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void LockedElement_RejectsGeometryButAllowsMetadataEdits()
    {
        using var project = new TestProject(locked: true);
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;

        Assert.Throws<InvalidOperationException>(() => session.UpdateElement(id, name: "Must not apply", x: 40));
        Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        Assert.False(session.IsDirty);

        Assert.True(session.UpdateElement(id, name: "Reel label"));
        Assert.Equal("Reel label", Assert.Single(session.Elements).Name);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void SaveAndReload_PreserveProjectPanelNativeReelAndSourceData()
    {
        using var project = new TestProject(richPanel: true);
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;
        Assert.True(session.UpdateElement(id, x: 123));
        session.Save();

        var saved = ReadManifest(project.PanelPath);
        Assert.Equal(Panel2DDocumentStorage.CurrentSchemaVersion, saved.SchemaVersion);
        Assert.Equal("Main panel", saved.Title);
        Assert.Equal("Preserve this package metadata", saved.Summary);
        var reel = Assert.Single(saved.Elements);
        Assert.Equal(123, reel.X);
        Assert.Equal("reel-art.png", reel.AssetPath);
        Assert.Equal("reel-mask.png", reel.SecondaryAssetPath);
        Assert.Equal("vendor.reel", reel.ImportSource?.Format);
        Assert.Equal("board/layout.xml", reel.ImportSource?.Reference);
        Assert.Equal(4, reel.SourceComponentIndex);
        Assert.Equal(9, reel.SourceElementIndex);
        Assert.Equal("shared-set-a", reel.SharedSourceSetId);
        Assert.Equal(2, reel.SharedSourceSetCount);
        Assert.True(reel.SourceBlend);
        Assert.Equal(8, reel.Native?.Stops);
        Assert.Equal("#ffcc00", reel.Native?.DisplayColorHex);
        Assert.Equal(2, Assert.Single(reel.ReelLamps!).LampNumber);
        var faceShape = Assert.Single(saved.FaceSourceShapes);
        Assert.Equal("source-window", faceShape.Id);
        Assert.Equal("Source window", faceShape.Name);
        Assert.Equal(4, faceShape.Points.Length);

        var reloaded = project.OpenSession();
        Assert.Equal(123, Assert.Single(reloaded.Elements).X);
        Assert.Equal("Main panel", reloaded.PanelTitle);
        Assert.False(reloaded.IsDirty);
        Assert.Equal(9, ReadProjectVersion(project.ProjectFilePath));
        Assert.True(reloaded.UpdateElement(id, y: 456));
        reloaded.Save();
        var resaved = ReadManifest(project.PanelPath);
        Assert.Equal(456, Assert.Single(resaved.Elements).Y);
        Assert.Equal("reel-art.png", Assert.Single(resaved.Elements).AssetPath);
        Assert.Equal(8, Assert.Single(resaved.Elements).Native?.Stops);
        Assert.Equal("source-window", Assert.Single(resaved.FaceSourceShapes).Id);
    }

    [Fact]
    public void NativeOnlyAssetMetadata_IsProjectedFromNormalizedStorage()
    {
        using var project = new TestProject(richPanel: true, nativeOnlyAsset: true);
        var session = project.OpenSession();
        var element = Assert.Single(session.Elements);

        Assert.Equal("reel-art.png", element.AssetPath);
        Assert.Equal("reel-mask.png", element.SecondaryAssetPath);
        Assert.False(session.IsDirty);

        Assert.True(session.UpdateElement(element.Id, x: 99));
        session.Save();
        var reloaded = ReadManifest(project.PanelPath);
        Assert.Equal("reel-art.png", Assert.Single(reloaded.Elements).AssetPath);
        Assert.Equal("reel-art.png", Assert.Single(reloaded.Elements).Native?.AssetPath);
    }

    [Fact]
    public void Save_RejectsExternalFileChangesWithoutOverwritingThem()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;
        session.UpdateElement(id, name: "Local edit");
        var externalContent = File.ReadAllText(project.PanelPath) + Environment.NewLine + " ";
        File.WriteAllText(project.PanelPath, externalContent);

        var error = Assert.Throws<IOException>(session.Save);

        Assert.Contains("changed outside this session", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(externalContent, File.ReadAllText(project.PanelPath));
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void ResolveAsset_RejectsTraversalAndSymbolicLinkSegments()
    {
        using var project = new TestProject();
        var session = project.OpenSession();

        Assert.Throws<InvalidDataException>(() => session.ResolveAsset("../../outside.txt"));

        var target = Path.Combine(project.Root, "outside");
        Directory.CreateDirectory(target);
        var link = Path.Combine(project.Root, "Assets", "linked");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return;
        }

        Assert.Throws<InvalidDataException>(() => session.ResolveAsset("Assets/linked/secret.txt"));
    }

    [Fact]
    public void DirtyDocument_CannotBeReplacedUntilSavedOrUndone()
    {
        using var project = new TestProject(twoPanels: true);
        var session = project.OpenSession();
        var firstPanel = session.PanelPath!;
        var secondPanel = project.SecondPanelPath!;
        var projectFile = project.ProjectFilePath;
        session.UpdateElement(session.Elements.Single().Id, name: "Unsaved");

        Assert.Throws<InvalidOperationException>(() => session.OpenPanel(secondPanel));
        Assert.Throws<InvalidOperationException>(() => session.OpenProject(project.Root));
        Assert.Equal(firstPanel, session.PanelPath);
        Assert.Equal("Unsaved", Assert.Single(session.Elements).Name);

        session.Undo();
        session.OpenPanel(secondPanel);
        Assert.Equal("Second panel", session.PanelTitle);
        session.OpenProject(projectFile);
        Assert.Null(session.PanelPath);
    }

    [Fact]
    public void OpenProject_RejectsProjectsOutsideWorkspaceWithoutReplacingCurrentState()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var outsideProjectPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.oasisproj");
        File.WriteAllText(outsideProjectPath, """{"version":9,"name":"Outside","layout":{"assets":"Assets","generated":"Generated"}}""");

        try
        {
            Assert.Throws<InvalidDataException>(() => session.OpenProject(outsideProjectPath));
            Assert.Equal(project.ProjectFilePath, session.ProjectFilePath);
            Assert.Equal("Test Project", session.ProjectName);
            Assert.Equal(project.PanelPath, session.PanelPath);
            Assert.Equal("Main panel", session.PanelTitle);
            Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        }
        finally
        {
            File.Delete(outsideProjectPath);
        }
    }

    [Fact]
    public void OpenProject_RejectsInvalidSchemaOrLayoutAndLeavesCurrentStateUntouched()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var invalidDirectory = Path.Combine(project.Root, "InvalidProjects");
        Directory.CreateDirectory(invalidDirectory);
        var invalidVersionPath = Path.Combine(invalidDirectory, "WrongVersion.oasisproj");
        File.WriteAllText(invalidVersionPath, """{"version":8,"name":"Wrong version","layout":{"assets":"Assets","generated":"Generated"}}""");
        var invalidLayoutPath = Path.Combine(invalidDirectory, "WrongLayout.oasisproj");
        File.WriteAllText(invalidLayoutPath, """{"version":9,"name":"Wrong layout","layout":{"assets":"Content","generated":"Generated"}}""");

        Assert.Throws<InvalidDataException>(() => session.OpenProject(invalidVersionPath));
        Assert.Throws<InvalidDataException>(() => session.OpenProject(invalidLayoutPath));
        Assert.Equal(project.ProjectFilePath, session.ProjectFilePath);
        Assert.Equal("Test Project", session.ProjectName);
        Assert.Equal(project.PanelPath, session.PanelPath);
        Assert.Equal("Main panel", session.PanelTitle);
        Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void OpenProject_RejectsNonStringLayoutMetadataAndLeavesCurrentStateUntouched()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var invalidPath = Path.Combine(project.Root, "InvalidProjects", "InvalidMetadata.oasisproj");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidPath)!);
        File.WriteAllText(invalidPath, """{"version":9,"name":"Invalid metadata","layout":{"assets":17,"generated":"Generated"}}""");

        Assert.Throws<InvalidDataException>(() => session.OpenProject(invalidPath));
        Assert.Equal(project.ProjectFilePath, session.ProjectFilePath);
        Assert.Equal("Test Project", session.ProjectName);
        Assert.Equal(project.PanelPath, session.PanelPath);
        Assert.Equal("Main panel", session.PanelTitle);
    }

    [Fact]
    public void OpenPanel_RequiresTheExactPackageManifestLocation()
    {
        using var project = new TestProject();
        var session = project.OpenSession();
        var wrongPath = Path.Combine(project.Root, "Assets", "Panel2D", "asset.panel2d");
        File.WriteAllText(wrongPath, CreateValidPanelJson("Wrong location"));

        Assert.Throws<InvalidDataException>(() => session.OpenPanel(wrongPath));
        Assert.Equal(project.PanelPath, session.PanelPath);
        Assert.Equal("Main panel", session.PanelTitle);
        Assert.Equal(project.PanelPath, Assert.Single(session.ListPanels()));
    }

    [Fact]
    public void OpenPanel_RejectsUnknownFieldsWithoutReplacingCurrentState()
    {
        using var project = new TestProject(twoPanels: true);
        var session = project.OpenSession();
        var otherPanelPath = project.SecondPanelPath!;
        var json = JsonNode.Parse(File.ReadAllText(otherPanelPath))!;
        json["futurePanelProperty"] = true;
        File.WriteAllText(otherPanelPath, json.ToJsonString());

        var error = Assert.Throws<InvalidDataException>(() => session.OpenPanel(otherPanelPath));

        Assert.Contains("unsupported", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(project.PanelPath, session.PanelPath);
        Assert.Equal("Main panel", session.PanelTitle);
        Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void OpenPanel_RejectsUnknownElementFields()
    {
        using var project = new TestProject(twoPanels: true);
        var session = project.OpenSession();
        var otherPanelPath = project.SecondPanelPath!;
        var json = JsonNode.Parse(File.ReadAllText(otherPanelPath))!;
        json["Elements"]![0]!["futureElementProperty"] = "unsupported";
        File.WriteAllText(otherPanelPath, json.ToJsonString());

        Assert.Throws<InvalidDataException>(() => session.OpenPanel(otherPanelPath));
        Assert.Equal(project.PanelPath, session.PanelPath);
        Assert.Equal("Main panel", session.PanelTitle);
    }

    [Fact]
    public void SwitchingAwayAndBackToSavedPanel_PreservesItsUndoHistory()
    {
        using var project = new TestProject(twoPanels: true);
        var session = project.OpenSession();
        var id = session.Elements.Single().Id;
        session.UpdateElement(id, name: "Saved edit");
        session.Save();
        Assert.False(session.IsDirty);
        Assert.True(session.CanUndo);

        session.OpenPanel(project.SecondPanelPath!);
        Assert.Equal("Second panel", session.PanelTitle);
        session.OpenPanel(project.PanelPath);

        Assert.Equal("Saved edit", Assert.Single(session.Elements).Name);
        Assert.False(session.IsDirty);
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Equal("Reel", Assert.Single(session.Elements).Name);
        Assert.True(session.IsDirty);
        session.Redo();
        Assert.False(session.IsDirty);
    }

    private static Panel2DDocumentFile ReadManifest(string path)
    {
        Assert.True(Panel2DDocumentStorage.TryRead(File.ReadAllText(path), out var manifest));
        return manifest;
    }

    private static int ReadProjectVersion(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("version").GetInt32();
    }

    private static string CreateValidPanelJson(string title)
    {
        var element = new PanelElementFile
        {
            ObjectId = "wrong-location-element",
            Name = "Rectangle",
            Kind = "rectangle",
            Width = 10,
            Height = 10
        };
        var shape = new PanelFaceSourceShapeFile
        {
            Id = "shape",
            Name = "Shape",
            Type = "perspectiveRectangle",
            Points = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)]
        };
        return Panel2DDocumentStorage.Serialize(title, string.Empty, [element], [shape]);
    }

    private sealed class TestProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisAuthoringSessionTests", Guid.NewGuid().ToString("N"));
        public string ProjectFilePath => Path.Combine(Root, "TestProject.oasisproj");
        public string PanelPath => Path.Combine(Root, "Assets", "Panel2D", "MainPanel", "asset.panel2d");
        public string? SecondPanelPath => _twoPanels ? Path.Combine(Root, "Assets", "Panel2D", "SecondPanel", "asset.panel2d") : null;
        private readonly bool _twoPanels;

        public TestProject(bool richPanel = false, bool locked = false, bool twoPanels = false, bool nativeOnlyAsset = false)
        {
            _twoPanels = twoPanels;
            Directory.CreateDirectory(Path.Combine(Root, "Assets"));
            Directory.CreateDirectory(Path.Combine(Root, "Generated"));
            Directory.CreateDirectory(Path.Combine(Root, "Assets", "Panel2D", "MainPanel"));
            File.WriteAllText(ProjectFilePath, """{"version":9,"name":"Test Project","layout":{"assets":"Assets","generated":"Generated"}}""");
            File.WriteAllText(PanelPath, CreatePanelJson("Main panel", richPanel, locked, nativeOnlyAsset));
            if (twoPanels)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SecondPanelPath!)!);
                File.WriteAllText(SecondPanelPath!, CreatePanelJson("Second panel", false, false));
            }
        }

        public AuthoringSession OpenSession()
        {
            var session = new AuthoringSession(Root);
            session.OpenProject(ProjectFilePath);
            session.OpenPanel(PanelPath);
            return session;
        }

        private static string CreatePanelJson(string title, bool rich, bool locked, bool nativeOnlyAsset = false)
        {
            var element = new PanelElementFile
            {
                ObjectId = "test-element",
                Name = "Reel",
                Kind = rich ? "reel" : "rectangle",
                X = 10,
                Y = 20,
                Width = 64,
                Height = 80,
                LockTransform = locked,
                IsVisible = true,
                AssetPath = rich && !nativeOnlyAsset ? "reel-art.png" : null,
                SecondaryAssetPath = rich && !nativeOnlyAsset ? "reel-mask.png" : null,
                SourceComponentIndex = rich ? 4 : null,
                SourceElementIndex = rich ? 9 : null,
                SharedSourceSetId = rich ? "shared-set-a" : null,
                SharedSourceSetCount = rich ? 2 : null,
                SourceBlend = rich,
                ImportSource = rich ? new PanelElementImportSourceFile { Format = "vendor.reel", Reference = "board/layout.xml" } : null,
                Native = rich ? new PanelElementNativeFile
                {
                    AssetPath = nativeOnlyAsset ? "reel-art.png" : null,
                    SecondaryAssetPath = nativeOnlyAsset ? "reel-mask.png" : null,
                    Stops = 8,
                    VisibleScale = 0.75,
                    BandOffset = 0.125,
                    DisplayColorHex = "#ffcc00",
                    Text = "native label"
                } : null,
                ReelLamps = rich ? [new PanelElementReelLampFile { Position = "top", LampNumber = 2, LocalVerticalCenter = 0.2, Radius = 0.15, Intensity = 0.8 }] : null
            };
            var shapes = new[]
            {
                new PanelFaceSourceShapeFile
                {
                    Id = "source-window",
                    Name = "Source window",
                    Type = "perspectiveRectangle",
                    Points = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)]
                }
            };
            var content = Panel2DDocumentStorage.Serialize(title, "Preserve this package metadata", [element], shapes);
            Assert.True(Panel2DDocumentStorage.TryRead(content, out var parsed));
            return content;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
