using Dock.Model.Controls;
using System.Buffers.Binary;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class NativeAssetPersistenceTests
{
    [Fact]
    public async Task NativeAssetDocumentsRestoreWithStableIdsSplitFloatAndSelection()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = CreateTempDirectory("OasisNativePersist-");
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Native persistence");
                var panelA = session.CreatePanel("A");
                var rectangleA = session.AddRectangle();
                session.UpdateElement(rectangleA, name: "Saved A");
                session.Save();
                var panelB = session.CreatePanel("B");
                var rectangleB = session.AddRectangle();
                session.UpdateElement(rectangleB, name: "Saved B");
                session.Save();

                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                await view.OpenNativeAssetAsync("Panel2D", panelA);
                await view.OpenNativeAssetAsync("Panel2D", panelB);

                var initial = view.InspectWorkspaceState();
                var assetLayout = Assert.IsType<AssetWorkspaceLayout>(initial.AssetLayout);
                var projectRoot = Path.GetDirectoryName(session.ProjectFilePath!)!;
                var relativeA = Path.GetRelativePath(projectRoot, panelA).Replace('\\', '/');
                var relativeB = Path.GetRelativePath(projectRoot, panelB).Replace('\\', '/');
                var savedA = Assert.Single(assetLayout.Documents.Where(document => document.Path == relativeA));
                var savedB = Assert.Single(assetLayout.Documents.Where(document => document.Path == relativeB));

                var layout = CurrentDockLayout(view);
                var documentA = Assert.IsAssignableFrom<IDocument>(WorkspaceLayoutState.LeafMap(layout)[savedA.Id]);
                var documentB = Assert.IsAssignableFrom<IDocument>(WorkspaceLayoutState.LeafMap(layout)[savedB.Id]);
                var editorA = Assert.IsType<WorkspaceView>(documentA.Context);
                var editorB = Assert.IsType<WorkspaceView>(documentB.Context);
                editorA.CommandSession.SelectElements([rectangleA]);
                editorB.CommandSession.SelectElements([rectangleB]);

                await view.SplitDocumentAsync("Panel2D", panelA, "Panel2D", panelB, "right");
                view.MoveWorkspacePane(savedA.Id, "float", savedB.Id);
                Assert.NotEmpty(view.FloatingWorkspaceWindows);
                Assert.NotEmpty(capture.Capture());
                Assert.NotEmpty(capture.CaptureFloatingWindows());

                await view.SaveAllDocumentsAsync();
                Assert.False(view.HasUnsavedChanges);
                var serialized = WorkspaceLayoutState.Serialize(view.InspectWorkspaceState());
                var persisted = WorkspaceLayoutState.Deserialize(serialized);
                var persistedLayout = Assert.IsType<AssetWorkspaceLayout>(persisted.AssetLayout);
                Assert.Equal(new[] { savedA.Id, savedB.Id }.OrderBy(id => id), persistedLayout.Documents.Select(document => document.Id).OrderBy(id => id));
                Assert.Equal(savedA.Id, Assert.Single(persistedLayout.Documents.Where(document => document.Path == relativeA)).Id);
                Assert.Equal(savedB.Id, Assert.Single(persistedLayout.Documents.Where(document => document.Path == relativeB)).Id);
                Assert.Contains(savedA.Id, LeafIds(persistedLayout.Layout));
                Assert.Contains(savedB.Id, LeafIds(persistedLayout.Layout));
                Assert.NotEmpty(persistedLayout.Layout.Windows ?? []);

                view.ResetLayout();
                view.ApplyWorkspaceState(persisted);
                await view.WaitForSceneAsync();
                Assert.NotEmpty(capture.Capture());
                Assert.NotEmpty(capture.CaptureFloatingWindows());

                var restored = Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout);
                Assert.Equal(persistedLayout.ActiveId, restored.ActiveId);
                Assert.Equal(persistedLayout.Documents.Select(document => document.Id).OrderBy(id => id),
                    restored.Documents.Select(document => document.Id).OrderBy(id => id));
                Assert.Equal(2, view.InspectOpenDocuments().Count);
                var floating = Assert.Single(view.InspectOpenDocuments().Where(document => document.IsFloating));
                Assert.Equal(savedA.Id, restored.Documents.Single(document => document.Path == relativeA).Id);
                Assert.Equal("Panel2D", floating.Kind);
                Assert.True(floating.Path.EndsWith(Path.GetFileName(panelA), StringComparison.OrdinalIgnoreCase));

                var restoredLayout = CurrentDockLayout(view);
                var restoredA = Assert.IsType<WorkspaceView>(WorkspaceLayoutState.LeafMap(restoredLayout)[savedA.Id].Context);
                var restoredB = Assert.IsType<WorkspaceView>(WorkspaceLayoutState.LeafMap(restoredLayout)[savedB.Id].Context);
                Assert.Equal(new[] { rectangleA }, restoredA.CommandSession.SelectedIds);
                Assert.Equal(new[] { rectangleB }, restoredB.CommandSession.SelectedIds);
                Assert.Equal("Saved A", Assert.Single(restoredA.CommandSession.Elements).Name);
                Assert.Equal("Saved B", Assert.Single(restoredB.CommandSession.Elements).Name);
                Assert.False(restoredA.CommandSession.IsDirty);
                Assert.False(restoredB.CommandSession.IsDirty);

                var beforeRejectedRestore = view.InspectWorkspaceState();
                var beforeIds = Assert.IsType<AssetWorkspaceLayout>(beforeRejectedRestore.AssetLayout).Documents.Select(document => document.Id).OrderBy(id => id).ToArray();
                var duplicateIds = persisted with
                {
                    AssetLayout = persistedLayout with
                    {
                        Documents = [persistedLayout.Documents[0], persistedLayout.Documents[1] with { Id = persistedLayout.Documents[0].Id }]
                    }
                };
                var unsafePath = persisted with
                {
                    AssetLayout = persistedLayout with
                    {
                        Documents = [persistedLayout.Documents[0] with { Path = "../outside.panel" }, persistedLayout.Documents[1]]
                    }
                };
                var unknownLeaf = persisted with
                {
                    AssetLayout = persistedLayout with { Layout = ReplaceLeafId(persistedLayout.Layout, savedA.Id, "stale-document") }
                };
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(duplicateIds));
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(unsafePath));
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(unknownLeaf));
                var afterRejectedRestore = Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout);
                Assert.Equal(beforeIds, afterRejectedRestore.Documents.Select(document => document.Id).OrderBy(id => id));
                Assert.Equal(2, view.InspectOpenDocuments().Count);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    [Fact]
    public async Task LegacySingleAuthoringWorkspaceMigratesToNativeAssetDocument()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = CreateTempDirectory("OasisLegacyPersist-");
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Legacy migration");
                var panel = session.CreatePanel("Panel");
                var rectangle = session.AddRectangle();
                session.Save();
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                var initial = view.InspectWorkspaceState();
                var relative = Path.GetRelativePath(Path.GetDirectoryName(session.ProjectFilePath!)!, panel).Replace('\\', '/');
                var legacy = initial with
                {
                    Version = 1,
                    AssetLayout = null,
                    ActiveDocument = "Panel2D",
                    Panel = relative,
                    Documents = [new WorkspaceDocumentState("Panel2D", relative)],
                    PanelSelection = [rectangle],
                    FloatingDocuments = []
                };

                view.ApplyWorkspaceState(legacy);
                Assert.True(view.IsMovingAssetWindow);
                await view.WaitForSceneAsync();

                var docs = Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout).Documents;
                var migrated = Assert.Single(docs);
                Assert.Equal("Panel2D", migrated.Kind);
                Assert.Equal(relative, migrated.Path);
                Assert.Equal(new[] { rectangle }, migrated.Selection);
                Assert.Single(view.InspectOpenDocuments());
                Assert.Equal(rectangle, Assert.Single(Assert.IsType<WorkspaceView>(
                    WorkspaceLayoutState.LeafMap(CurrentDockLayout(view))[migrated.Id].Context).CommandSession.Elements).Id);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    [Fact]
    public async Task LegacyFloatingAssetMigratesAfterPreflightWithItsBoundsAndSelection()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = CreateTempDirectory("OasisLegacyFloating-");
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Legacy floating migration");
                var projectPath = session.ProjectFilePath!;
                var panel = session.CreatePanel("Panel");
                var rectangle = session.AddRectangle();
                session.Save();
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                await view.WaitForSceneAsync();
                await view.SaveAllDocumentsAsync();
                await view.CloseProjectAsync();
                view.OpenProject(projectPath);
                var initial = view.InspectWorkspaceState();
                var projectRelative = Path.GetRelativePath(session.WorkspacePath, projectPath).Replace('\\', '/');
                var panelRelative = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, panel).Replace('\\', '/');
                var floating = new FloatingAssetState("Panel2D", panelRelative, null, new(140, 155, 980, 740),
                    new(.72, 13, -8, false), PanelSelection: [rectangle]);
                var legacy = initial with
                {
                    Version = 1,
                    AssetLayout = null,
                    Project = projectRelative,
                    Panel = panelRelative,
                    ActiveDocument = "Panel2D",
                    Documents = [new WorkspaceDocumentState("Panel2D", panelRelative)],
                    PanelSelection = [rectangle],
                    FloatingDocuments = [floating]
                };
                var invalid = legacy with
                {
                    FloatingDocuments = [floating with { Bounds = new(0, 0, double.NaN, 740) }]
                };
                Assert.Throws<InvalidDataException>(() => view.ApplyWorkspaceState(invalid));
                Assert.Empty(view.InspectOpenDocuments());
                Assert.Equal(initial.Project, view.InspectWorkspaceState().Project);

                view.ApplyWorkspaceState(legacy);
                Assert.True(view.IsMovingAssetWindow);
                await view.WaitForSceneAsync();

                var native = Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout);
                var migrated = Assert.Single(native.Documents);
                Assert.Equal("Panel2D", migrated.Kind);
                Assert.Equal(panelRelative, migrated.Path);
                Assert.Equal(new[] { rectangle }, migrated.Selection);
                Assert.Equal(new WorkspaceViewportState(.72, 13, -8, false), migrated.Viewport);
                Assert.Single(view.InspectOpenDocuments());
                Assert.True(Assert.Single(view.InspectOpenDocuments()).IsFloating);
                var floatingWindow = Assert.Single(view.FloatingWorkspaceWindows);
                Assert.Equal(140, floatingWindow.Position.X);
                Assert.Equal(155, floatingWindow.Position.Y);
                Assert.Equal(980, floatingWindow.Width);
                Assert.Equal(rectangle, view.GetFloatingDocumentView("Panel2D", panel).CommandSession.SelectedIds.Single());
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    [Fact]
    public async Task LegacyThreeDSceneMigratesWithCameraAndSelectionIntoItsNativeDocument()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = CreateTempDirectory("OasisLegacy3D-");
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Legacy 3D migration");
                var projectPath = session.ProjectFilePath!;
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                var source = Path.Combine(Path.GetDirectoryName(projectPath)!, "Assets", "Source.glb");
                File.WriteAllBytes(source, CreateMinimalGlb());
                var cabinet = view.Cabinets.Create("Cabinet", "Assets/Source.glb");
                view.Cabinets.Save();
                await view.WaitForSceneAsync();
                await view.CloseProjectAsync();
                view.OpenProject(projectPath);
                var initial = view.InspectWorkspaceState();
                var projectRelative = Path.GetRelativePath(session.WorkspacePath, projectPath).Replace('\\', '/');
                var cabinetRelative = Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, cabinet).Replace('\\', '/');
                var camera = new WorkspaceSceneCameraState(1, 2, 3, 9, .6, .3, 60);
                var legacy = initial with
                {
                    Version = 1,
                    AssetLayout = null,
                    Project = projectRelative,
                    Documents = [new WorkspaceDocumentState("Cabinet3D", cabinetRelative)],
                    Scene3D = new("Cabinet3D", Cabinet: cabinetRelative, Selection: "cabinet", Camera: camera),
                    ActiveDocument = "Panel2D"
                };

                view.ApplyWorkspaceState(legacy);
                Assert.True(view.IsMovingAssetWindow);
                await view.WaitForSceneAsync();

                var asset = Assert.Single(Assert.IsType<AssetWorkspaceLayout>(view.InspectWorkspaceState().AssetLayout).Documents);
                Assert.Equal("Cabinet3D", asset.Kind);
                Assert.Equal("Cabinet3D", asset.Scene?.Mode);
                Assert.Equal("cabinet", asset.Scene?.Selection);
                var restoredCamera=Assert.IsType<WorkspaceSceneCameraState>(asset.Scene?.Camera);
                Assert.Equal(camera.TargetX,restoredCamera.TargetX,5);Assert.Equal(camera.TargetY,restoredCamera.TargetY,5);Assert.Equal(camera.TargetZ,restoredCamera.TargetZ,5);
                Assert.Equal(camera.Distance,restoredCamera.Distance,5);Assert.Equal(camera.Yaw,restoredCamera.Yaw,5);Assert.Equal(camera.Pitch,restoredCamera.Pitch,5);Assert.Equal(camera.FieldOfView,restoredCamera.FieldOfView,5);
                Assert.Equal(new OasisEditor.Avalonia.Rendering3D.SceneCameraState(new(1, 2, 3), 9, .6f, .3f, 60), view.SceneView.Camera);
                Assert.Equal("Cabinet3D", view.SceneKind);
                Assert.Null(view.Preview.Backend);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    private static IDockable CurrentDockLayout(WorkspaceView view)
        => Assert.IsAssignableFrom<IDockable>(view.GetVisualDescendants().OfType<DockControl>().Single().Layout);

    private static HashSet<string> LeafIds(WorkspaceDockNode root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Visit(WorkspaceDockNode node)
        {
            if ((node.Kind is "Tool" or "Document") && node.Id is not null) ids.Add(node.Id);
            foreach (var child in node.Children ?? []) Visit(child);
            foreach (var child in node.Hidden ?? []) Visit(child);
            foreach (var child in node.Left ?? []) Visit(child);
            foreach (var child in node.Right ?? []) Visit(child);
            foreach (var child in node.Top ?? []) Visit(child);
            foreach (var child in node.Bottom ?? []) Visit(child);
            foreach (var window in node.Windows ?? []) Visit(window.Layout);
        }
        Visit(root);
        return ids;
    }

    private static WorkspaceDockNode ReplaceLeafId(WorkspaceDockNode node, string current, string replacement)
    {
        WorkspaceDockNode Replace(WorkspaceDockNode value) => value with
        {
            Id = (value.Kind is "Tool" or "Document") && value.Id == current ? replacement : value.Id,
            Children = value.Children?.Select(Replace).ToArray(),
            Hidden = value.Hidden?.Select(Replace).ToArray(),
            Left = value.Left?.Select(Replace).ToArray(),
            Right = value.Right?.Select(Replace).ToArray(),
            Top = value.Top?.Select(Replace).ToArray(),
            Bottom = value.Bottom?.Select(Replace).ToArray(),
            Windows = value.Windows?.Select(window => window with { Layout = Replace(window.Layout) }).ToArray()
        };
        return Replace(node);
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] CreateMinimalGlb()
    {
        var binary = new byte[140];
        WriteFloats(binary, 0, [-1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0]);
        WriteFloats(binary, 48, [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1]);
        WriteFloats(binary, 96, [0, 0, 1, 0, 1, 1, 0, 1]);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(binary.AsSpan(128 + i * 2, 2), new ushort[] { 0, 1, 2, 0, 2, 3 }[i]);
        var json = System.Text.Encoding.UTF8.GetBytes("""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],
             "nodes":[{"name":"Model","mesh":0}],
             "meshes":[{"name":"Quad","primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3}]}],
             "buffers":[{"byteLength":140}],
             "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48},{"buffer":0,"byteOffset":48,"byteLength":48},{"buffer":0,"byteOffset":96,"byteLength":32},{"buffer":0,"byteOffset":128,"byteLength":12}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":4,"type":"VEC3","min":[-1,-1,0],"max":[1,1,0]},{"bufferView":1,"componentType":5126,"count":4,"type":"VEC3"},{"bufferView":2,"componentType":5126,"count":4,"type":"VEC2"},{"bufferView":3,"componentType":5123,"count":6,"type":"SCALAR"}]}
            """);
        var jsonLength = (json.Length + 3) & ~3;
        var binaryLength = (binary.Length + 3) & ~3;
        var result = new byte[12 + 8 + jsonLength + 8 + binaryLength];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0, 4), 0x46546c67);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8, 4), (uint)result.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), 0x4e4f534a);
        json.CopyTo(result.AsSpan(20));
        result.AsSpan(20 + json.Length, jsonLength - json.Length).Fill(0x20);
        var binaryHeader = 20 + jsonLength;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(binaryHeader, 4), (uint)binaryLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(binaryHeader + 4, 4), 0x004e4942);
        binary.CopyTo(result.AsSpan(binaryHeader + 8));
        return result;
    }

    private static void WriteFloats(byte[] destination, int offset, float[] values)
    {
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset + i * sizeof(float), sizeof(float)), BitConverter.SingleToInt32Bits(values[i]));
    }
}
