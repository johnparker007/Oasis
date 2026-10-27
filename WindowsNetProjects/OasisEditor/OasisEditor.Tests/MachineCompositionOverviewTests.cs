using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class MachineCompositionOverviewTests
{
    [Fact]
    public void OverviewShowsActualMachineRelationshipsAndMissingAssetDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisCompositionGraph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var session = new AuthoringSession(root);
            session.CreateProject("Project");
            var projectRoot = Path.GetDirectoryName(session.ProjectFilePath!)!;
            var project = new EditorProject
            {
                Name = "Project", ProjectFilePath = session.ProjectFilePath!, ProjectDirectory = projectRoot,
                AssetsDirectory = Path.Combine(projectRoot, "Assets"), GeneratedDirectory = Path.Combine(projectRoot, "Generated")
            };
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAsset = AssetReference.Project("Assets/Cabinets/Missing/asset.cabinet3d"),
                SurfaceAssignments = [new("screen", "Assets/Faces/Missing/asset.face")],
                PrefabDependencies = [new("Token", AssetReference.Project("Assets/Object3D/Missing/asset.object3d"))]
            };

            var graph = new MachineCompositionOverviewBuilder().Build(machine, project, string.Empty);

            Assert.Contains(graph.Nodes, node => node.Kind == "Machine" && node.Title == "Game");
            Assert.Contains(graph.Edges, edge => edge.FromId == $"machine:{machine.Id}" && edge.ToId == "runtime");
            Assert.Contains(graph.Nodes, node => node.Kind == "Cabinet" && node.Missing && node.ManifestPath is null);
            Assert.Contains(graph.Nodes, node => node.Kind == "Face" && node.Missing && node.ManifestPath is null);
            Assert.Contains(graph.Nodes, node => node.Kind == "Prefab" && node.Missing && node.ManifestPath is null);
            Assert.Contains(graph.Diagnostics, message => message.Contains("Cabinet", StringComparison.Ordinal));
            Assert.Contains(graph.Diagnostics, message => message.Contains("Face", StringComparison.Ordinal));
            Assert.Contains(graph.Diagnostics, message => message.Contains("Prefab", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task MachineWorkspaceCanSwitchBetweenCompositionGraphAndScene()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "OasisCompositionView-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var session = new AuthoringSession(root);
                session.CreateProject("Project");
                using var capture = new WorkspaceCapture(session);
                var view = capture.View;
                view.Machines.OpenMachine(view.Machines.ListMachines().Single().ManifestPath);
                view.ShowMachine();
                view.ShowMachineCompositionGraph();
                Assert.True(view.IsMachineGraphActive);
                Assert.Contains(view.InspectMachineCompositionGraph().Nodes, node => node.Kind == "Machine");
                var state=view.InspectWorkspaceState();Assert.True(state.MachineGraph);
                view.ShowMachineCompositionGraph(false);
                Assert.False(view.IsMachineGraphActive);
                view.ApplyWorkspaceState(state);await view.WaitForSceneAsync();Assert.True(view.IsMachineGraphActive);
                await Task.CompletedTask;
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }
}
