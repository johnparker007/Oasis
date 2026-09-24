using Xunit;
using System.Text.Json;
using OasisEditor.Automation;
using OasisEditor.Features.CabinetEditor.Models;

namespace OasisEditor.Tests;

public sealed class MachineDocumentTests
{
    [Fact]
    public void Schema2_RoundTripsCompositionRuntimeAndInputs()
    {
        var machine = MachineDocument.Create("Machine A") with
        {
            CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d",
            SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
            ReelAssignments = [new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel")],
            Runtime = new MachineEmulationRuntime(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "Assets/ROMs/game.bin" })
        };
        var json = MachineDocumentStorage.Serialize(machine);
        Assert.True(MachineDocumentStorage.TryRead(json, out var result, out var error), error);
        Assert.Equal(machine.Id, result.Id);
        Assert.Equal(FruitMachinePlatformType.MPU5, result.Runtime.Platform);
        Assert.Equal("Assets/ROMs/game.bin", result.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Single(result.SurfaceAssignments);
        Assert.Single(result.ReelAssignments);
    }

    [Fact]
    public void WrongSchema_IsRejectedWithoutCompatibilityFallback()
    {
        var json = MachineDocumentStorage.Serialize(MachineDocument.Create("Machine")).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 0");
        Assert.False(MachineDocumentStorage.TryRead(json, out _, out var error));
        Assert.Contains("only version 2", error);
    }

    [Fact]
    public void TwoMachines_KeepRuntimeSettingsIsolated()
    {
        var a = MachineDocument.Create("A") with { Runtime = new(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "a.bin" }) };
        var b = MachineDocument.Create("B") with { Runtime = new(FruitMachinePlatformType.Epoch, new EpochNativeRomSettings { ProgramRom1Path = "b.bin" }) };
        Assert.Equal("a.bin", a.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Equal("b.bin", b.Runtime.SettingsAs<EpochNativeRomSettings>().ProgramRom1Path);
        Assert.NotEqual(a.Runtime.Platform, b.Runtime.Platform);
    }

    [Fact]
    public void OneDocumentAuthority_PreservesUnsavedCompositionRuntimeAndInputs()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Game")));
        tab.MachineCabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d";
        tab.ExecuteMachineMutation(machine => machine with { Runtime = new(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "game.bin" }) }, "Runtime");
        tab.ExecuteMachineMutation(machine => machine with { InputDefinitions = [new InputDefinitionModel { Id = "start" }] }, "Inputs");
        var result = tab.GetMachineDocument();
        Assert.Equal("Assets/Cabinet3D/Vogue/asset.cabinet3d", result.CabinetAssetPath);
        Assert.Equal("game.bin", result.Runtime.SettingsAs<Mpu5NativeRomSettings>().ProgramRom1Path);
        Assert.Single(result.InputDefinitions);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void EditedDisplayName_IsNotReplacedByPackageTitleWhenSaved()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Package Name/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Original")));
        tab.MachineDisplayName = "Authored Display Name";
        var json = DocumentWorkspaceViewModel.BuildDocumentContent(tab);
        Assert.True(MachineDocumentStorage.TryRead(json, out var reopened, out var error), error);
        Assert.Equal("Authored Display Name", reopened.DisplayName);
        Assert.Equal("Package Name", tab.Document.Title);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "machine-0")]
    [InlineData(2, null)]
    public void StartupSelection_AutomaticallySelectsExactlyOneMachine(int count, string? expected)
    {
        var paths = Enumerable.Range(0, count).Select(index => $"machine-{index}").ToArray();
        Assert.Equal(expected, MachineStartupSelectionPolicy.SelectAutomatic(paths));
    }

    [Fact]
    public void RuntimeProjection_RetainsConcreteSelectedPlatformSettingsAsJson()
    {
        var runtime = new MachineEmulationRuntime(FruitMachinePlatformType.MPU5, new Mpu5NativeRomSettings { ProgramRom1Path = "game.bin" });
        var projected = MachineRuntimeDefinition.From(runtime);
        Assert.False(projected.ExecutionSupportedByPlayer);
        Assert.Equal("MPU5", projected.Platform);
        using var settings = JsonDocument.Parse(projected.PlatformSettingsJson);
        Assert.Equal("game.bin", settings.RootElement.GetProperty("programRom1Path").GetString());
    }

    [Fact]
    public void MachineEditor_DiscoversReelAssetsAndEditsPathAssignments()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineEditor_" + Guid.NewGuid().ToString("N"));
        try
        {
            var assets = Path.Combine(root, "Assets"); var cabinetPath = Path.Combine(assets, "Cabinet3D", "Vogue", "asset.cabinet3d");
            Directory.CreateDirectory(Path.GetDirectoryName(cabinetPath)!);
            File.WriteAllText(cabinetPath, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));
            var project = new EditorProject { Name = "Project", ProjectFilePath = Path.Combine(root, "Project.oasisproj"), ProjectDirectory = root, AssetsDirectory = assets, GeneratedDirectory = Path.Combine(root, "Generated") };
            var reelPath = new ProjectAssetPathService().GetReelManifestPath(project, "Standard");
            Directory.CreateDirectory(Path.GetDirectoryName(reelPath)!);
            File.WriteAllText(reelPath, ReelDocumentStorage.Serialize(ReelDocument.Create("Standard")));
            var facePath = WriteFace(project, "Glass", 0);
            var machine = MachineDocument.Create("Game") with { CabinetAssetPath = "Assets/Cabinet3D/Vogue/asset.cabinet3d", SurfaceAssignments = [new("OasisFace_Glass", facePath)], ReelAssignments = [new(MachineObjectReference.Reel(0), "Assets/Reels/Missing/asset.reel")] };
            var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(Path.Combine(assets, "Machines", "Game", "asset.machine"), "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
            tab.SetProjectAccessor(() => project);
            Assert.Contains(tab.MachineCabinetChoices, choice => choice.DisplayName == "Vogue");
            var reelZero = Assert.Single(tab.MachineReelAssignmentRows.Where(row => row.Reference == MachineObjectReference.Reel(0)));
            Assert.Contains(reelZero.Choices, choice => choice.AssetPath == "Assets/Reels/Standard/asset.reel" && choice.DisplayName == "Standard");
            reelZero.SelectedReelAssetPath = "Assets/Reels/Standard/asset.reel";
            Assert.Equal("Assets/Reels/Standard/asset.reel", Assert.Single(tab.GetMachineDocument().ReelAssignments).ReelAssetPath);
            Assert.True(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SettingsBinding_SwitchingMachinesCreatesIsolatedViewModelModels()
    {
        var machineA = MachineDocument.Create("A") with { Runtime = new(FruitMachinePlatformType.MPU3, new Mpu3ProjectSettings()) };
        var machineBSettings = new Mpu3ProjectSettings(); machineBSettings.ProgramRoms[0].Path = "b.rom";
        var machineB = MachineDocument.Create("B") with { Runtime = new(FruitMachinePlatformType.MPU3, machineBSettings) };
        var boundA = MachineRuntimeSettingsBinding.CreateEditableSnapshot<Mpu3ProjectSettings>(machineA);
        var boundB = MachineRuntimeSettingsBinding.CreateEditableSnapshot<Mpu3ProjectSettings>(machineB);
        var viewModelA = new Mpu3ProjectSettingsViewModel(boundA, _ => { });
        var viewModelB = new Mpu3ProjectSettingsViewModel(boundB, _ => { });
        viewModelA.ProgramRoms[0].Path = "a-edited.rom";
        Assert.Equal("b.rom", viewModelB.ProgramRoms[0].Path);
        Assert.Equal(string.Empty, machineA.Runtime.SettingsAs<Mpu3ProjectSettings>().ProgramRoms[0].Path);
        Assert.Equal("b.rom", machineB.Runtime.SettingsAs<Mpu3ProjectSettings>().ProgramRoms[0].Path);
    }

    [Fact]
    public void CompositionCatalog_LiveRefreshIsStableDeduplicatedAndDoesNotMutateMachine()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineCatalog_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game"));
            using var browser = new AssetBrowserViewModel(() => project, () => { }, () => { }, (_, _) => { }, _ => { }, _ => null, _ => true);
            browser.AssetCatalogChanged += tab.RefreshMachineCompositionChoices;
            Assert.Equal(new[] { "(None)" }, tab.MachineCabinetChoices.Select(choice => choice.DisplayName).ToArray());
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);

            var voguePath = WriteCabinet(project, "Vogue", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            browser.RefreshAssetBrowser();
            Assert.Equal(new[] { "(None)", "Vogue" }, tab.MachineCabinetChoices.Select(choice => choice.DisplayName).ToArray());
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);

            tab.MachineCabinetAssetPath = voguePath;
            Assert.Equal(voguePath, tab.GetMachineDocument().CabinetAssetPath);
            Assert.Equal("Vogue", Assert.Single(tab.MachineCabinetChoices.Where(choice => choice.AssetPath == tab.MachineCabinetAssetPath)).DisplayName);
            Assert.True(tab.CommandService.CanUndo);
            tab.RefreshMachineCompositionChoices(); tab.RefreshMachineCompositionChoices();
            Assert.Equal(new[] { "(None)", "Vogue" }, tab.MachineCabinetChoices.Select(choice => choice.DisplayName).ToArray());
            Assert.Equal(voguePath, tab.MachineCabinetAssetPath);
            Assert.True(tab.CommandService.TryUndo());
            Assert.Null(tab.GetMachineDocument().CabinetAssetPath);
            Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CompositionCatalog_MissingCabinetAndReelRefreshPreserveAuthoredReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineMissing_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var voguePath = WriteCabinet(project, "Vogue", []);
            var facePath = WriteFace(project, "Glass", 0);
            const string missingReel = "Assets/Reels/Missing/asset.reel";
            var machine = MachineDocument.Create("Game") with { CabinetAssetPath = voguePath, SurfaceAssignments = [new("OasisFace_Glass", facePath)], ReelAssignments = [new(MachineObjectReference.Reel(0), missingReel)] };
            var tab = CreateMachineTab(project, machine);
            var reelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));
            Assert.Equal(missingReel, reelRow.SelectedReelAssetPath);
            Assert.Contains(reelRow.Choices, choice => choice.AssetPath == missingReel && choice.DisplayName.StartsWith("Missing:", StringComparison.Ordinal));
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
            tab.RefreshMachineCompositionChoices();
            Assert.Same(reelRow, tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0)));
            Assert.Equal(missingReel, tab.GetMachineDocument().ReelAssignments.Single().ReelAssetPath);
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);

            File.Delete(new ProjectAssetPathService().ResolveProjectRelativePath(project, voguePath));
            tab.RefreshMachineCompositionChoices();
            Assert.Equal(voguePath, tab.GetMachineDocument().CabinetAssetPath);
            Assert.Contains(tab.MachineCabinetChoices, choice => choice.AssetPath == voguePath && choice.DisplayName.StartsWith("Missing:", StringComparison.Ordinal));
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void FaceCatalog_SeesFaceCreatedAfterMachineOpenedWithoutDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineFaces_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game"));
            using var browser = new AssetBrowserViewModel(() => project, () => { }, () => { }, (_, _) => { }, _ => { }, _ => null, _ => true);
            browser.AssetCatalogChanged += tab.RefreshMachineCompositionChoices;
            Assert.Equal("(None)", Assert.Single(tab.MachineFaceChoices).DisplayName);
            var faceDirectory = Path.Combine(project.AssetsDirectory, "Faces", "Top Glass"); Directory.CreateDirectory(faceDirectory);
            File.WriteAllText(Path.Combine(faceDirectory, ProjectAssetPathService.FaceManifestFileName), "{}");
            browser.RefreshAssetBrowser(); browser.RefreshAssetBrowser();
            Assert.Equal(new[] { "(None)", "Top Glass" }, tab.MachineFaceChoices.Select(choice => choice.DisplayName).ToArray());
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ReelCatalog_NewAssetRefreshPreservesSelectionsAndDoesNotMutateMachine()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineReels_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            const string reelPath = "Assets/Reels/Standard/asset.reel";
            var cabinetPath = WriteCabinet(project, "Vogue", []);
            var facePath = WriteFace(project, "Glass", 0, 1);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with { CabinetAssetPath = cabinetPath, SurfaceAssignments = [new("OasisFace_Glass", facePath)], ReelAssignments = [new(MachineObjectReference.Reel(0), reelPath), new(MachineObjectReference.Reel(1), reelPath)] });
            using var browser = new AssetBrowserViewModel(() => project, () => { }, () => { }, (_, _) => { }, _ => { }, _ => null, _ => true);
            browser.AssetCatalogChanged += tab.RefreshMachineCompositionChoices;
            var rows = tab.MachineReelAssignmentRows.ToArray();
            Assert.All(rows, row => Assert.Contains(row.Choices, choice => choice.AssetPath == reelPath && choice.DisplayName.StartsWith("Missing:", StringComparison.Ordinal)));
            var absolute = new ProjectAssetPathService().GetReelManifestPath(project, "Standard");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, ReelDocumentStorage.Serialize(ReelDocument.Create("Standard Reel")));

            browser.RefreshAssetBrowser();

            Assert.All(rows, row =>
            {
                Assert.Same(row, tab.MachineReelAssignmentRows.Single(candidate => candidate.Reference == row.Reference));
                Assert.Equal(reelPath, row.SelectedReelAssetPath);
                Assert.Contains(row.Choices, choice => choice.AssetPath == reelPath && choice.DisplayName == "Standard Reel");
            });
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void AssigningFaceImmediatelyCreatesRequiredReelRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineFaceReels_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", []);
            var facePath = WriteFace(project, "Glass", 0, 1, 2, 3);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with { CabinetAssetPath = cabinetPath });
            Assert.Empty(tab.MachineReelAssignmentRows);

            tab.SetMachineSurfaceAssignment("OasisFace_Glass", facePath);

            Assert.Equal(new[] { "0", "1", "2", "3" }, tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ChangingFaceAssignmentAndUndoRedoUpdateOnlyReelTopology()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineFaceSwitch_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", []);
            var faceA = WriteFace(project, "A", 0, 1);
            var faceB = WriteFace(project, "B", 0, 1, 2, 3);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with { CabinetAssetPath = cabinetPath, SurfaceAssignments = [new("OasisFace_Glass", faceA)] });
            var surfaceRows = tab.MachineSurfaceAssignmentRows.ToArray();
            var reelZero = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));

            tab.SetMachineSurfaceAssignment("OasisFace_Glass", faceB);
            Assert.Equal(new[] { "0", "1", "2", "3" }, tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
            Assert.Same(reelZero, tab.MachineReelAssignmentRows[0]);
            Assert.Equal(surfaceRows, tab.MachineSurfaceAssignmentRows);

            Assert.True(tab.CommandService.TryUndo());
            Assert.Equal(new[] { "0", "1" }, tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
            Assert.Same(reelZero, tab.MachineReelAssignmentRows[0]);
            Assert.True(tab.CommandService.TryRedo());
            Assert.Equal(new[] { "0", "1", "2", "3" }, tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RemovingFacePrunesAssignmentAtomicallyAndUndoRedoRestoresComposition()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineRetainedReel_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", [new("Assets/Reels/Small/asset.reel", "Small", 230, 70)]);
            var facePath = WriteFace(project, "Glass", 3);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_Glass", facePath)],
                ReelAssignments = [new(MachineObjectReference.Reel(3), "Assets/Reels/Small/asset.reel")]
            };
            var tab = CreateMachineTab(project, machine);
            Assert.Single(tab.MachineReelAssignmentRows);

            tab.SetMachineSurfaceAssignment("OasisFace_Glass", null);

            Assert.Empty(tab.GetMachineDocument().ReelAssignments);
            Assert.Empty(tab.MachineReelAssignmentRows);

            Assert.True(tab.CommandService.TryUndo());
            Assert.Equal(facePath, Assert.Single(tab.GetMachineDocument().SurfaceAssignments).FaceAssetPath);
            Assert.Equal("Assets/Reels/Small/asset.reel", Assert.Single(tab.GetMachineDocument().ReelAssignments).ReelAssetPath);
            Assert.Equal(MachineObjectReference.Reel(3), Assert.Single(tab.MachineReelAssignmentRows).Reference);

            Assert.True(tab.CommandService.TryRedo());
            Assert.Empty(tab.GetMachineDocument().SurfaceAssignments);
            Assert.Empty(tab.GetMachineDocument().ReelAssignments);
            Assert.Empty(tab.MachineReelAssignmentRows);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ReassigningSameReelFaceToBothTargetsPrunesStaleMappingsAndPreservesRequiredMapping()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineDuplicateFace_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", [
                new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50),
                new("Assets/Reels/Small/asset.reel", "Small", 180, 40)]);
            var top = WriteFace(project, "TopGlassNew", 3);
            var bottom = WriteFace(project, "BottomGlassNew", 0, 1, 2);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_TopGlass", top), new("OasisFace_BottomGlass", bottom)],
                ReelAssignments =
                [
                    new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel"),
                    new(MachineObjectReference.Reel(1), "Assets/Reels/Standard/asset.reel"),
                    new(MachineObjectReference.Reel(2), "Assets/Reels/Standard/asset.reel"),
                    new(MachineObjectReference.Reel(3), "Assets/Reels/Small/asset.reel")
                ]
            };
            var tab = CreateMachineTab(project, machine);

            tab.SetMachineSurfaceAssignment("OasisFace_BottomGlass", top);

            var retained = Assert.Single(tab.GetMachineDocument().ReelAssignments);
            Assert.Equal(MachineObjectReference.Reel(3), retained.MachineReelReference);
            Assert.Equal("Assets/Reels/Small/asset.reel", retained.ReelAssetPath);
            Assert.Equal(["3"], tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
            Assert.Equal(2, tab.GetMachineDocument().SurfaceAssignments.Count(assignment => assignment.FaceAssetPath == top));

            Assert.True(tab.CommandService.TryUndo());
            Assert.Equal(bottom, tab.GetMachineDocument().SurfaceAssignments.Single(assignment => assignment.TargetId == "OasisFace_BottomGlass").FaceAssetPath);
            Assert.Equal(["0", "1", "2", "3"], tab.GetMachineDocument().ReelAssignments.OrderBy(assignment => assignment.MachineReelReference.Id).Select(assignment => assignment.MachineReelReference.Id).ToArray());
            Assert.Equal(["0", "1", "2", "3"], tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());

            Assert.True(tab.CommandService.TryRedo());
            Assert.Equal(["3"], tab.GetMachineDocument().ReelAssignments.Select(assignment => assignment.MachineReelReference.Id).ToArray());
            Assert.Equal(["3"], tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SavedAssignedFaceRefreshUpdatesRequirementsButUnrelatedFaceDoesNot()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineSavedFace_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", []);
            var assigned = WriteFace(project, "Assigned", 0);
            var unrelated = WriteFace(project, "Unrelated", 7);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with { CabinetAssetPath = cabinetPath, SurfaceAssignments = [new("OasisFace_Glass", assigned)] });
            var reelZero = Assert.Single(tab.MachineReelAssignmentRows);

            WriteFace(project, "Unrelated", 7, 8);
            File.SetLastWriteTimeUtc(new ProjectAssetPathService().ResolveProjectRelativePath(project, unrelated), DateTime.UtcNow.AddSeconds(2));
            tab.RefreshMachineCompositionChoices();
            Assert.Same(reelZero, Assert.Single(tab.MachineReelAssignmentRows));

            WriteFace(project, "Assigned", 0, 1);
            File.SetLastWriteTimeUtc(new ProjectAssetPathService().ResolveProjectRelativePath(project, assigned), DateTime.UtcNow.AddSeconds(3));
            tab.RefreshMachineCompositionChoices();
            Assert.Equal(new[] { "0", "1" }, tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
            Assert.Same(reelZero, tab.MachineReelAssignmentRows[0]);
            Assert.False(tab.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SavedAssignedFaceRefreshHidesStaleAssignmentWithoutDirtyingOrMutatingMachine()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineSavedFaceRemoval_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            var assigned = WriteFace(project, "Assigned", 0, 1);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_Glass", assigned)],
                ReelAssignments =
                [
                    new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel"),
                    new(MachineObjectReference.Reel(1), "Assets/Reels/Standard/asset.reel")
                ]
            };
            var tab = CreateMachineTab(project, machine);

            WriteFace(project, "Assigned", 0);
            File.SetLastWriteTimeUtc(new ProjectAssetPathService().ResolveProjectRelativePath(project, assigned), DateTime.UtcNow.AddSeconds(2));
            tab.RefreshMachineCompositionChoices();

            Assert.Equal(["0"], tab.MachineReelAssignmentRows.Select(row => row.Reference.Id).ToArray());
            Assert.Equal(["0", "1"], tab.GetMachineDocument().ReelAssignments.Select(assignment => assignment.MachineReelReference.Id).ToArray());
            Assert.False(tab.IsDirty);
            Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void FaceAndReelAssignments_DoNotRecreateActiveRows_AndUndoSynchronizesValues()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Game")));
        var faceRow = new MachineSurfaceAssignmentRow(tab, "OasisFace_TopGlass", "Top Glass", [new("(None)", null), new("TopGlass", "Assets/Faces/TopGlass/asset.face")], null);
        var reelRow = new MachineReelAssignmentRow(tab, MachineObjectReference.Reel(0), [new("(None)", null), new("Standard", "Assets/Reels/Standard/asset.reel")], null);
        tab.MachineSurfaceAssignmentRows.Add(faceRow);
        tab.MachineReelAssignmentRows.Add(reelRow);

        faceRow.SelectedAssetPath = "Assets/Faces/TopGlass/asset.face";
        Assert.Same(faceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
        Assert.Equal("Assets/Faces/TopGlass/asset.face", Assert.Single(tab.GetMachineDocument().SurfaceAssignments).FaceAssetPath);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Same(faceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
        Assert.Null(faceRow.SelectedAssetPath);
        Assert.False(tab.CommandService.CanUndo);

        reelRow.SelectedReelAssetPath = "Assets/Reels/Standard/asset.reel";
        Assert.Same(reelRow, Assert.Single(tab.MachineReelAssignmentRows));
        Assert.Equal("Assets/Reels/Standard/asset.reel", Assert.Single(tab.GetMachineDocument().ReelAssignments).ReelAssetPath);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Same(reelRow, Assert.Single(tab.MachineReelAssignmentRows));
        Assert.Null(reelRow.SelectedReelAssetPath);
        Assert.False(tab.CommandService.CanUndo);
    }

    [Fact]
    public void NonCompositionMutations_DoNotRecreateCompositionRows()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Game")));
        var faceRow = new MachineSurfaceAssignmentRow(tab, "OasisFace_TopGlass", "Top Glass", [new("(None)", null)], null);
        var reelRow = new MachineReelAssignmentRow(tab, MachineObjectReference.Reel(0), [new("(None)", null)], null);
        tab.MachineSurfaceAssignmentRows.Add(faceRow);
        tab.MachineReelAssignmentRows.Add(reelRow);

        tab.MachineDisplayName = "Renamed";
        tab.MachinePlatform = FruitMachinePlatformType.MPU5;
        tab.ExecuteMachineMutation(machine => machine with { InputDefinitions = [new InputDefinitionModel { Id = "start" }] }, "Inputs");

        Assert.Same(faceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
        Assert.Same(reelRow, Assert.Single(tab.MachineReelAssignmentRows));
    }

    [Fact]
    public void ExplicitCabinetChange_ClearsSurfacesAndTheirReelAssignments()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineCabinetChange_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetA = WriteCabinet(project, "A", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            var cabinetB = WriteCabinet(project, "B", [new("Assets/Reels/Small/asset.reel", "Small", 180, 40)]);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetA,
                SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel")]
            };
            var tab = CreateMachineTab(project, machine);
            tab.MachineCabinetAssetPath = cabinetB;

            Assert.Empty(tab.GetMachineDocument().SurfaceAssignments);
            Assert.Empty(tab.GetMachineDocument().ReelAssignments);
            Assert.Empty(tab.MachineReelAssignmentRows);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MachineSaveThenAssetRefresh_PreservesVisibleCompositionAndDoesNotAddUndoMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineSaveRefresh_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            var faceA = WriteFace(project, "FaceA", 0);
            var faceB = WriteFace(project, "FaceB", 0);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_TopGlass", faceA)],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel")]
            };
            var tab = CreateMachineTab(project, machine);
            var surfaceRow = Assert.Single(tab.MachineSurfaceAssignmentRows);
            surfaceRow.RefreshChoices([new("Face A", faceA), new("Face B", faceB)]);
            var reelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));
            var cabinetChoice = tab.MachineCabinetChoices.Single(choice => choice.AssetPath == cabinetPath);
            var noneFaceChoice = tab.MachineFaceChoices.Single(choice => choice.AssetPath is null);
            surfaceRow.SelectedAssetPath = faceB;
            var savePath = new ProjectAssetPathService().GetMachineManifestPath(project, "Game");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

            new DocumentSaveService().SaveDocument(tab, savePath).ApplyTo(tab);
            tab.RefreshMachineCompositionChoices(); // the effective callback raised by the scheduled Assets refresh

            Assert.Equal(cabinetPath, tab.MachineCabinetAssetPath);
            Assert.Same(cabinetChoice, tab.MachineCabinetChoices.Single(choice => choice.AssetPath == cabinetPath));
            Assert.Same(noneFaceChoice, tab.MachineFaceChoices.Single(choice => choice.AssetPath is null));
            Assert.Same(surfaceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
            Assert.Equal(faceB, surfaceRow.SelectedAssetPath);
            Assert.Same(reelRow, tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0)));
            Assert.Equal("Assets/Reels/Standard/asset.reel", reelRow.SelectedReelAssetPath);
            Assert.False(tab.IsDirty);
            Assert.True(tab.CommandService.CanUndo);
            Assert.Equal(faceB, Assert.Single(tab.GetMachineDocument().SurfaceAssignments).FaceAssetPath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ChoiceReconciliation_PreservesStableObjectsAndCanRenotifySelectedValue()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateMachineStub("Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Machine")));
        var none = new MachineAssetChoice("(None)", null);
        var selected = new MachineAssetChoice("Face A", "Assets/Faces/A/asset.face");
        var choices = new System.Collections.ObjectModel.ObservableCollection<MachineAssetChoice> { none, selected };
        var row = new MachineSurfaceAssignmentRow(tab, "OasisFace_TopGlass", "Top Glass", choices, selected.AssetPath);
        var notifications = 0;
        row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(row.SelectedAssetPath)) notifications++; };

        row.RefreshChoices([new("(None)", null), new("Face A", selected.AssetPath), new("Face B", "Assets/Faces/B/asset.face")]);
        row.SynchronizeSelectedAssetPath(selected.AssetPath, forceNotification: true);

        Assert.Same(none, row.Choices[0]);
        Assert.Same(selected, row.Choices[1]);
        Assert.Equal(selected.AssetPath, row.SelectedAssetPath);
        Assert.Equal(1, notifications);
        Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
    }

    [Fact]
    public void CabinetContentRefreshWithoutStructuralChange_PreservesReelRowAndSelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisCabinetContentRefresh_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetPath = WriteCabinet(project, "Vogue", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            var facePath = WriteFace(project, "Glass", 0);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_Glass", facePath)],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "Assets/Reels/Standard/asset.reel")]
            });
            var row = tab.MachineReelAssignmentRows.Single(item => item.Reference == MachineObjectReference.Reel(0));
            var selectedChoice = row.Choices.Single(choice => choice.AssetPath == "Assets/Reels/Standard/asset.reel");
            WriteCabinet(project, "Vogue", [new("Assets/Reels/Standard/asset.reel", "Standard", 210, 50)]);
            File.SetLastWriteTimeUtc(new ProjectAssetPathService().ResolveProjectRelativePath(project, cabinetPath), DateTime.UtcNow.AddSeconds(2));

            tab.RefreshMachineCompositionChoices();

            Assert.Same(row, tab.MachineReelAssignmentRows.Single(item => item.Reference == MachineObjectReference.Reel(0)));
            Assert.Same(selectedChoice, row.Choices.Single(choice => choice.AssetPath == "Assets/Reels/Standard/asset.reel"));
            Assert.Equal("Assets/Reels/Standard/asset.reel", row.SelectedReelAssetPath);
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static EditorProject CreateProject(string root)
    {
        var assets = Path.Combine(root, "Assets"); Directory.CreateDirectory(Path.Combine(assets, "Machines")); Directory.CreateDirectory(Path.Combine(root, "Generated"));
        return new EditorProject { Name = "Project", ProjectFilePath = Path.Combine(root, "Project.oasisproj"), ProjectDirectory = root, AssetsDirectory = assets, GeneratedDirectory = Path.Combine(root, "Generated") };
    }

    private static DocumentTabViewModel CreateMachineTab(EditorProject project, MachineDocument machine)
    {
        var path = new ProjectAssetPathService().GetMachineManifestPath(project, "Game");
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile(path, "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(machine));
        tab.SetProjectAccessor(() => project); return tab;
    }

    private static string WriteCabinet(EditorProject project, string name, (string Path, string Name, double Diameter, double Width)[] specifications)
    {
        var path = new ProjectAssetPathService().GetCabinet3DManifestPath(project, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, CabinetDocumentStorage.Serialize(CabinetDocument.FromModelPath("missing.glb")));
        foreach (var specification in specifications)
        {
            var assetName = specification.Path.Contains("Small", StringComparison.OrdinalIgnoreCase) ? "Small" : "Standard";
            var reelPath = new ProjectAssetPathService().GetReelManifestPath(project, assetName);
            Directory.CreateDirectory(Path.GetDirectoryName(reelPath)!);
            File.WriteAllText(reelPath, ReelDocumentStorage.Serialize(ReelDocument.Create(specification.Name) with { DiameterMm = specification.Diameter, WidthMm = specification.Width }));
        }
        return new ProjectAssetPathService().ToProjectRelativePath(project, path);
    }

    private static string WriteFace(EditorProject project, string name, params int[] logicalReels)
    {
        var path = new ProjectAssetPathService().GetFaceManifestPath(project, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var face = new FaceDocumentModel
        {
            Id = Guid.NewGuid().ToString("D"),
            Title = name,
            SourceRegion = new FaceSourceRegionModel { Width = 100, Height = 100 },
            Elements = logicalReels.Select((reel, index) => (FaceElementModel)new FaceReelMount
            {
                ObjectId = $"reel-{reel}", Name = $"Reel {reel}", X = index * 10, Y = 0, Width = 10, Height = 20,
                LinkedMachineObjectReference = MachineObjectReference.Reel(reel)
            }).ToArray()
        };
        File.WriteAllText(path, FaceDocumentStorage.Serialize(face));
        return new ProjectAssetPathService().ToProjectRelativePath(project, path);
    }
}
