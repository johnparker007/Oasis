using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class WorkspaceAssetLayoutTests
{
    [Fact]
    public void ValidMultiDocumentStateRoundTripsAndValidatesConfinedPaths()
    {
        var project = CreateTempDirectory();
        try
        {
            var state = CreateState(
                new AssetDocumentViewState("Panel2D", "Assets/Panels/Main.panel", null, "asset:panel", new(.8, 4, -2, false), ["object-1"], "object-1"),
                new AssetDocumentViewState("Cabinet3D", "Assets/Cabinets/Cabinet.object3d", null, "asset:cabinet",
                    Scene: new("Cabinet3D", Cabinet: "Assets/Cabinets/Cabinet.object3d")));
            var json = AssetWorkspaceLayoutState.Serialize(state);
            var restored = AssetWorkspaceLayoutState.Deserialize(json, project);

            Assert.Equal(json, AssetWorkspaceLayoutState.Serialize(restored));
            Assert.Equal("asset:cabinet", restored.ActiveId);
        }
        finally { Directory.Delete(project, recursive: true); }
    }

    [Fact]
    public void EmptyWorkspaceMayRetainAuthoringPlaceholder()
    {
        var state = CreateState([], activeId: null, authoring: true);
        AssetWorkspaceLayoutState.Validate(state);

        AssetWorkspaceLayoutState.Validate(CreateState([], activeId: null, authoring: false));
    }

    [Fact]
    public void RejectsInvalidVersionAndDocumentIdentityOrKind()
    {
        var valid = CreateState(new AssetDocumentViewState("Face", "Assets/Faces/Face.face", null, "asset:face"));

        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(valid with { Version = 1 }));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(valid with
        {
            Documents = [valid.Documents[0], valid.Documents[0] with { Path = "Assets/Faces/Other.face" }]
        }));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(valid with
        {
            Documents = [valid.Documents[0] with { Kind = "Unknown" }]
        }));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(valid with { ActiveId = "missing" }));
    }

    [Theory]
    [InlineData("../outside.asset")]
    [InlineData("Assets/../outside.asset")]
    [InlineData("C:/outside.asset")]
    [InlineData("/outside.asset")]
    public void RejectsRootedOrEscapingDocumentPaths(string path)
    {
        var state = CreateState(new AssetDocumentViewState("Panel2D", path, null, "asset:panel"));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(state));
    }

    [Fact]
    public void RejectsDuplicateAssetPathsAndUnsupportedLibraryKinds()
    {
        var first = new AssetDocumentViewState("Face", "Assets/Faces/Face.face", null, "asset:one");
        var duplicate = first with { Id = "asset:two" };
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(CreateState(first, duplicate)));

        var panelInLibrary = new AssetDocumentViewState("Panel2D", "Panels/Main.panel", CreateTempDirectory(), "asset:panel");
        try { Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(CreateState(panelInLibrary))); }
        finally { Directory.Delete(panelInLibrary.LibraryRoot!, recursive: true); }
    }

    [Fact]
    public void ValidatesLayoutLeafSetAgainstDynamicDocumentsBeforeRestore()
    {
        var state = CreateState(new AssetDocumentViewState("Panel2D", "Assets/Panels/Main.panel", null, "asset:panel"));
        var unknown = state with { Layout = AddLeaf(state.Layout, new("Document", "stale-document")) };
        var missing = state with { Layout = RemoveLeaf(state.Layout, "asset:panel") };
        var duplicate = state with { Layout = AddLeaf(state.Layout, new("Tool", "Inspector")) };

        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(unknown));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(missing));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Validate(duplicate));
    }

    [Fact]
    public void DeserializerRejectsUnknownPropertiesAndOversizedPayloads()
    {
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Deserialize("""{"Version":2,"Layout":{},"Documents":[],"ActiveId":null,"$type":"Unexpected"}"""));
        Assert.Throws<InvalidDataException>(() => AssetWorkspaceLayoutState.Deserialize(new string(' ', 512 * 1024 + 1)));
    }

    private static AssetWorkspaceLayout CreateState(params AssetDocumentViewState[] documents)
        => CreateState(documents, documents.LastOrDefault()?.Id, authoring: documents.Length == 0);

    private static AssetWorkspaceLayout CreateState(AssetDocumentViewState[] documents, string? activeId, bool authoring)
    {
        var documentLeaves = documents.Select(document => new WorkspaceDockNode("Document", document.Id)).ToArray();
        if (authoring) documentLeaves = [.. documentLeaves, new WorkspaceDockNode("Document", "Authoring")];
        var documentDock = new WorkspaceDockNode("Documents", "AssetDocuments", Active: Math.Max(-1, documentLeaves.Length - 1),
            Default: Math.Max(-1, documentLeaves.Length - 1), Children: documentLeaves);
        var tools = new WorkspaceDockNode("Tools", "WorkspaceTools", Alignment: "Left", Active: 0, Default: 0,
            Children: [new("Tool", "Hierarchy"), new("Tool", "Project"), new("Tool", "Inspector"), new("Tool", "Output")]);
        var layout = new WorkspaceDockNode("Root", "WorkspaceRoot", Children: [tools, documentDock], Active: 1, Default: 1);
        return new(2, layout, documents, activeId);
    }

    private static WorkspaceDockNode AddLeaf(WorkspaceDockNode root, WorkspaceDockNode leaf)
    {
        var documents = root.Children![1];
        return root with { Children = [root.Children[0], documents with { Children = [.. documents.Children!, leaf] }] };
    }

    private static WorkspaceDockNode RemoveLeaf(WorkspaceDockNode root, string id)
    {
        var documents = root.Children![1];
        return root with { Children = [root.Children[0], documents with { Children = documents.Children!.Where(node => node.Id != id).ToArray() }] };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OasisAssetLayout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
