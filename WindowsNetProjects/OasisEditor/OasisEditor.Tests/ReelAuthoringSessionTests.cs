using OasisEditor;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class ReelAuthoringSessionTests
{
    [Fact]
    public void CreateEditHistorySaveAndReopenPreservesStableIdentity()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Standard Reel");
        var originalId = session.Document.Id;
        Assert.Equal(Path.Combine(fixture.Assets, "Reels", "Standard Reel", "asset.reel"), manifest);
        Assert.True(File.Exists(manifest));
        Assert.False(session.IsDirty);

        Assert.True(session.Update(displayName: "Wide Reel", diameterMm: 290, widthMm: 72));
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);
        Assert.False(session.Update(displayName: "Wide Reel", diameterMm: 290, widthMm: 72));
        session.Undo();
        Assert.Equal("Standard Reel", session.Document.DisplayName);
        Assert.False(session.IsDirty);
        Assert.True(session.CanRedo);
        session.Redo();
        session.Save();
        Assert.False(session.IsDirty);

        var reopened = new ReelAuthoringSession(fixture.ProjectSession);
        reopened.Open(manifest);
        Assert.Equal(originalId, reopened.Document.Id);
        Assert.Equal("Wide Reel", reopened.Document.DisplayName);
        Assert.Equal(290, reopened.Document.DiameterMm);
        Assert.Equal(72, reopened.Document.WidthMm);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public void InvalidValuesNoOpsAndDirtyReplacementAreHandledWithoutLosingEdits()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Reel");
        var notifications = 0;
        session.Changed += () => notifications++;

        Assert.False(session.Update(displayName: "Reel", diameterMm: 200, widthMm: 70));
        Assert.Equal(0, notifications);
        Assert.Throws<InvalidOperationException>(() => session.Update(diameterMm: 0));
        Assert.Throws<InvalidOperationException>(() => session.Update(widthMm: double.NaN));
        Assert.False(session.IsDirty);

        session.Update(displayName: "Edited");
        Assert.Throws<InvalidOperationException>(() => session.Open(manifest));
        Assert.Throws<InvalidOperationException>(() => session.Create("Would Be Lost"));
        Assert.Throws<InvalidOperationException>(() => session.Close());
        Assert.Equal(manifest, session.FilePath);
        Assert.Equal("Edited", session.Document.DisplayName);
        Assert.True(Directory.Exists(Path.Combine(fixture.Assets, "Reels", "Reel")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Assets, "Reels", "Would Be Lost")));
    }

    [Fact]
    public void FailedOpenPreservesCurrentDocumentAndRejectsEscapingPaths()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Current");
        var currentId = session.Document.Id;
        var invalidPackage = Path.Combine(fixture.Assets, "Reels", "Broken");
        Directory.CreateDirectory(invalidPackage);
        File.WriteAllText(Path.Combine(invalidPackage, "asset.reel"), "{ bad json");

        Assert.Throws<InvalidDataException>(() => session.Open(Path.Combine(invalidPackage, "asset.reel")));
        Assert.Throws<InvalidDataException>(() => session.Open("../outside/asset.reel"));
        var outside = Path.Combine(fixture.Root, "outside.reel");
        File.WriteAllText(outside, ReelDocumentStorage.Serialize(ReelDocument.Create("Outside")));
        Assert.Throws<InvalidDataException>(() => session.Open(outside));
        Assert.Equal(manifest, session.FilePath);
        Assert.Equal(currentId, session.Document.Id);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ExternalManifestAndPackageChangesBlockSaving()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Reel");
        session.Update(displayName: "Session edit");
        var originalManifest = File.ReadAllText(manifest);
        File.WriteAllText(manifest, originalManifest + " ");
        Assert.Throws<IOException>(session.Save);
        Assert.EndsWith(" ", File.ReadAllText(manifest));

        session.Undo();
        session.Open(manifest);
        session.Update(widthMm: 75);
        var note = Path.Combine(Path.GetDirectoryName(manifest)!, "notes.txt");
        File.WriteAllText(note, "external package content");
        Assert.Throws<IOException>(session.Save);
        Assert.Equal("external package content", File.ReadAllText(note));
    }

    [Fact]
    public void ProjectRebindBlocksStaleSessionAndPackageCollisionNeverOverwrites()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var manifest = session.Create("Shared");
        var original = File.ReadAllText(manifest);
        Assert.Throws<IOException>(() => session.Create("Shared"));
        Assert.Equal(original, File.ReadAllText(manifest));

        fixture.OpenSecondProject();
        Assert.False(session.IsProjectCurrent);
        Assert.False(session.CanUndo);
        Assert.Throws<InvalidOperationException>(() => session.Update(displayName: "Stale"));
        Assert.Throws<InvalidOperationException>(session.Save);
        Assert.Equal(original, File.ReadAllText(manifest));
    }

    [Fact]
    public void LibraryReelCanBeEditedAndSavedWithExplicitRootAndPackageConflictGuards()
    {
        using var fixture = new Fixture();
        var libraryRoot = Path.Combine(fixture.Root, "Library");
        var relativeManifest = Path.Combine("Reels", "Legacy", "Nested", "asset.reel");
        var package = Path.Combine(libraryRoot, "Reels", "Legacy", "Nested");
        Directory.CreateDirectory(package);
        var manifest = Path.Combine(package, "asset.reel");
        var original = ReelDocument.Create("Library standard");
        File.WriteAllText(manifest, ReelDocumentStorage.Serialize(original));

        var session = new ReelAuthoringSession(fixture.ProjectSession);
        session.OpenLibrary(libraryRoot, relativeManifest);
        Assert.True(session.IsLibraryAsset);
        Assert.Equal(Path.GetFullPath(libraryRoot), session.LibraryRoot);
        Assert.Equal(original.Id, session.Document.Id);
        Assert.True(session.Update(displayName: "Library wide", diameterMm: 285));

        var otherRoot = Path.Combine(fixture.Root, "OtherLibrary");
        var otherManifest = Path.Combine(otherRoot, "Reels", "Second", "asset.reel");
        Directory.CreateDirectory(Path.GetDirectoryName(otherManifest)!);
        File.WriteAllText(otherManifest, ReelDocumentStorage.Serialize(ReelDocument.Create("Second")));
        Assert.Throws<InvalidOperationException>(() => session.OpenLibrary(otherRoot, Path.Combine("Reels", "Second", "asset.reel")));
        Assert.Equal(original.Id, session.Document.Id);

        session.Save();
        var reopened = new ReelAuthoringSession(fixture.ProjectSession);
        reopened.OpenLibrary(libraryRoot, relativeManifest);
        Assert.Equal(original.Id, reopened.Document.Id);
        Assert.Equal("Library wide", reopened.Document.DisplayName);
        Assert.Equal(285, reopened.Document.DiameterMm);

        reopened.Update(widthMm: 75);
        File.WriteAllText(Path.Combine(package, "outside.txt"), "preserve");
        Assert.Throws<IOException>(reopened.Save);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(package, "outside.txt")));
    }

    [Fact]
    public void LibraryOpenRejectsEscapingAndWrongTypePathsWithoutReplacingDocument()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        var projectManifest = session.Create("Project reel");
        var libraryRoot = Path.Combine(fixture.Root, "Library");
        Directory.CreateDirectory(libraryRoot);
        Assert.Throws<InvalidDataException>(() => session.OpenLibrary(libraryRoot, "../escape/asset.reel"));
        Assert.Throws<InvalidDataException>(() => session.OpenLibrary(libraryRoot, "Cabinets/Reel/asset.reel"));
        Assert.Equal(projectManifest, session.FilePath);
        Assert.False(session.IsLibraryAsset);
    }

    [Fact]
    public void CloseClearsCleanDocumentAndCreateReusesCurrentProjectBinding()
    {
        using var fixture = new Fixture();
        var session = new ReelAuthoringSession(fixture.ProjectSession);
        session.Create("First");
        session.Close();
        Assert.Null(session.FilePath);
        Assert.False(session.IsDirty);
        Assert.Throws<InvalidOperationException>(() => _ = session.Document);

        var second = session.Create("Second");
        Assert.True(session.IsProjectCurrent);
        Assert.EndsWith(Path.Combine("Reels", "Second", "asset.reel"), second);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "OasisReelAuthoring-" + Guid.NewGuid().ToString("N"));
        public string ProjectRoot => Path.Combine(Root, "Project");
        public string Assets => Path.Combine(ProjectRoot, "Assets");
        public AuthoringSession ProjectSession { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Assets);
            var projectFile = Path.Combine(ProjectRoot, "Project.oasisproj");
            File.WriteAllText(projectFile, """{"name":"Project","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession = new AuthoringSession(Root);
            ProjectSession.OpenProject(projectFile);
        }

        public void OpenSecondProject()
        {
            var other = Path.Combine(Root, "Other");
            Directory.CreateDirectory(Path.Combine(other, "Assets"));
            var projectFile = Path.Combine(other, "Other.oasisproj");
            File.WriteAllText(projectFile, """{"name":"Other","version":9,"layout":{"assets":"Assets","generated":"Generated"}}""");
            ProjectSession.OpenProject(projectFile);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
