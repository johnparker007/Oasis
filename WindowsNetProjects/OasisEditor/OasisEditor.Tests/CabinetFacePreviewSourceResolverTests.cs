using OasisEditor.Features.CabinetEditor.Services;
using Xunit;

namespace OasisEditor.Tests;

public sealed class CabinetFacePreviewSourceResolverTests
{
    [Fact]
    public void SavedFacesResolveWithoutOpenTabs_AndOpenFaceTakesPrecedenceThenFallsBack()
    {
        using var fixture = new FaceProjectFixture();
        var topPath = fixture.WriteFace("Top", "Saved Top");
        var bottomPath = fixture.WriteFace("Bottom", "Saved Bottom");
        var resolver = new CabinetFacePreviewSourceResolver();

        Assert.True(resolver.TryResolve(fixture.Project, topPath, [], out var closedTop, out var topError), topError);
        Assert.True(resolver.TryResolve(fixture.Project, bottomPath, [], out var closedBottom, out var bottomError), bottomError);
        Assert.False(closedTop.IsLive);
        Assert.False(closedBottom.IsLive);
        Assert.Equal("Saved Top", closedTop.FaceDocument.Title);
        Assert.Equal("Saved Bottom", closedBottom.FaceDocument.Title);
        Assert.StartsWith("saved:", closedTop.CacheIdentity);

        var topManifest = new ProjectAssetPathService().ResolveProjectRelativePath(fixture.Project, topPath);
        var liveTopFile = FaceDocumentStorage.CreateEmpty("Top") with
        {
            Summary = "Unsaved live summary"
        };
        var openTop = new DocumentTabViewModel(
            EditorDocument.CreateFromFile(topManifest, "Face", "Top"),
            faceDocumentJson: FaceDocumentStorage.Serialize(liveTopFile));
        Assert.True(resolver.TryResolve(fixture.Project, topPath, [openTop], out var liveTop, out var liveError), liveError);
        Assert.True(liveTop.IsLive);
        Assert.Same(openTop, liveTop.OpenDocument);
        Assert.Same(openTop.RuntimeState, liveTop.RuntimeState);
        Assert.Equal("Unsaved live summary", liveTop.FaceDocument.Summary);
        Assert.StartsWith("open:", liveTop.CacheIdentity);

        Assert.True(resolver.TryResolve(fixture.Project, topPath, [], out var fallbackTop, out var fallbackError), fallbackError);
        Assert.False(fallbackTop.IsLive);
        Assert.Equal("Saved Top", fallbackTop.FaceDocument.Title);
    }

    [Fact]
    public void MissingAndInvalidSavedFacesAreUnavailableWithoutThrowing()
    {
        using var fixture = new FaceProjectFixture();
        var invalidPath = fixture.WriteRawFace("Invalid", "{not-json");
        var resolver = new CabinetFacePreviewSourceResolver();

        Assert.False(resolver.TryResolve(fixture.Project, "Assets/Faces/Missing/asset.face", [], out _, out var missingError));
        Assert.Contains("not found", missingError, StringComparison.OrdinalIgnoreCase);
        Assert.False(resolver.TryResolve(fixture.Project, invalidPath, [], out _, out var invalidError));
        Assert.Contains("invalid", invalidError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SavedFaceContentChangeChangesCacheIdentityContent()
    {
        using var fixture = new FaceProjectFixture();
        var path = fixture.WriteFace("Top", "Version One");
        var resolver = new CabinetFacePreviewSourceResolver();
        Assert.True(resolver.TryResolve(fixture.Project, path, [], out var first, out _));

        fixture.WriteFace("Top", "Version Two");
        Assert.True(resolver.TryResolve(fixture.Project, path, [], out var second, out _));

        Assert.Equal(first.CacheIdentity, second.CacheIdentity);
        Assert.NotEqual(first.ContentIdentity, second.ContentIdentity);
        Assert.Equal("Version Two", second.FaceDocument.Title);
    }

    private sealed class FaceProjectFixture : IDisposable
    {
        public FaceProjectFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"OasisFacePreviewSource_{Guid.NewGuid():N}");
            var assets = Path.Combine(Root, "Assets");
            Directory.CreateDirectory(assets);
            Project = new EditorProject
            {
                Name = "Project", ProjectFilePath = Path.Combine(Root, "Project.oasisproj"), ProjectDirectory = Root,
                AssetsDirectory = assets, GeneratedDirectory = Path.Combine(Root, "Generated")
            };
        }

        public string Root { get; }
        public EditorProject Project { get; }

        public string WriteFace(string name, string title) => WriteRawFace(name, FaceDocumentStorage.Serialize(FaceDocumentStorage.CreateEmpty(title)));

        public string WriteRawFace(string name, string content)
        {
            var path = new ProjectAssetPathService().GetFaceManifestPath(Project, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return new ProjectAssetPathService().ToProjectRelativePath(Project, path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
