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
            ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")],
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

            var voguePath = WriteCabinet(project, "Vogue", [new("standard", "Standard", 210, 50)]);
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
            var voguePath = WriteCabinet(project, "Vogue", [new("standard", "Standard", 210, 50)]);
            var machine = MachineDocument.Create("Game") with { CabinetAssetPath = voguePath, ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")] };
            var tab = CreateMachineTab(project, machine);
            var reelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));
            Assert.Equal("standard", reelRow.SelectedReelAssetPath);
            Assert.False(tab.IsDirty); Assert.False(tab.CommandService.CanUndo);
            tab.RefreshMachineCompositionChoices();
            Assert.Same(reelRow, tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0)));
            Assert.Equal("standard", tab.GetMachineDocument().ReelAssignments.Single().ReelAssetPath);
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
    public void FaceAndReelAssignments_DoNotRecreateActiveRows_AndUndoSynchronizesValues()
    {
        var tab = new DocumentTabViewModel(EditorDocument.CreateFromFile("C:/Project/Assets/Machines/Game/asset.machine", "Machine"), machineDocumentJson: MachineDocumentStorage.Serialize(MachineDocument.Create("Game")));
        var faceRow = new MachineSurfaceAssignmentRow(tab, "OasisFace_TopGlass", "Top Glass", [new("(None)", null), new("TopGlass", "Assets/Faces/TopGlass/asset.face")], null);
        var reelRow = new MachineReelAssignmentRow(tab, MachineObjectReference.Reel(0), [new("(None)", null), new("Standard", "standard")], null);
        tab.MachineSurfaceAssignmentRows.Add(faceRow);
        tab.MachineReelAssignmentRows.Add(reelRow);

        faceRow.SelectedAssetPath = "Assets/Faces/TopGlass/asset.face";
        Assert.Same(faceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
        Assert.Equal("Assets/Faces/TopGlass/asset.face", Assert.Single(tab.GetMachineDocument().SurfaceAssignments).FaceAssetPath);
        Assert.True(tab.CommandService.TryUndo());
        Assert.Same(faceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
        Assert.Null(faceRow.SelectedAssetPath);
        Assert.False(tab.CommandService.CanUndo);

        reelRow.SelectedReelAssetPath = "standard";
        Assert.Same(reelRow, Assert.Single(tab.MachineReelAssignmentRows));
        Assert.Equal("standard", Assert.Single(tab.GetMachineDocument().ReelAssignments).ReelAssetPath);
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
    public void ExplicitCabinetChange_ClearsAssignmentsAndRebuildsDependentReelRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisMachineCabinetChange_" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = CreateProject(root);
            var cabinetA = WriteCabinet(project, "A", [new("standard", "Standard", 210, 50)]);
            var cabinetB = WriteCabinet(project, "B", [new("small", "Small", 180, 40)]);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetA,
                SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/Top/asset.face")],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")]
            };
            var tab = CreateMachineTab(project, machine);
            var oldReelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));

            tab.MachineCabinetAssetPath = cabinetB;

            Assert.Empty(tab.GetMachineDocument().SurfaceAssignments);
            Assert.Empty(tab.GetMachineDocument().ReelAssignments);
            var newReelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));
            Assert.NotSame(oldReelRow, newReelRow);
            Assert.Contains(newReelRow.Choices, choice => choice.AssetPath == "small" && choice.DisplayName == "Small");
            Assert.DoesNotContain(newReelRow.Choices, choice => choice.AssetPath == "standard");
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
            var cabinetPath = WriteCabinet(project, "Vogue", [new("standard", "Standard", 210, 50)]);
            var machine = MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                SurfaceAssignments = [new("OasisFace_TopGlass", "Assets/Faces/FaceA/asset.face")],
                ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")]
            };
            var tab = CreateMachineTab(project, machine);
            var surfaceRow = Assert.Single(tab.MachineSurfaceAssignmentRows);
            surfaceRow.RefreshChoices([new("Face A", "Assets/Faces/FaceA/asset.face"), new("Face B", "Assets/Faces/FaceB/asset.face")]);
            var reelRow = tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0));
            var cabinetChoice = tab.MachineCabinetChoices.Single(choice => choice.AssetPath == cabinetPath);
            var noneFaceChoice = tab.MachineFaceChoices.Single(choice => choice.AssetPath is null);
            surfaceRow.SelectedAssetPath = "Assets/Faces/FaceB/asset.face";
            var savePath = new ProjectAssetPathService().GetMachineManifestPath(project, "Game");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

            new DocumentSaveService().SaveDocument(tab, savePath).ApplyTo(tab);
            tab.RefreshMachineCompositionChoices(); // the effective callback raised by the scheduled Assets refresh

            Assert.Equal(cabinetPath, tab.MachineCabinetAssetPath);
            Assert.Same(cabinetChoice, tab.MachineCabinetChoices.Single(choice => choice.AssetPath == cabinetPath));
            Assert.Same(noneFaceChoice, tab.MachineFaceChoices.Single(choice => choice.AssetPath is null));
            Assert.Same(surfaceRow, Assert.Single(tab.MachineSurfaceAssignmentRows));
            Assert.Equal("Assets/Faces/FaceB/asset.face", surfaceRow.SelectedAssetPath);
            Assert.Same(reelRow, tab.MachineReelAssignmentRows.Single(row => row.Reference == MachineObjectReference.Reel(0)));
            Assert.Equal("standard", reelRow.SelectedReelAssetPath);
            Assert.False(tab.IsDirty);
            Assert.True(tab.CommandService.CanUndo);
            Assert.Equal("Assets/Faces/FaceB/asset.face", Assert.Single(tab.GetMachineDocument().SurfaceAssignments).FaceAssetPath);
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
            var cabinetPath = WriteCabinet(project, "Vogue", [new("standard", "Standard", 210, 50)]);
            var tab = CreateMachineTab(project, MachineDocument.Create("Game") with
            {
                CabinetAssetPath = cabinetPath,
                ReelAssignments = [new(MachineObjectReference.Reel(0), "standard")]
            });
            var row = tab.MachineReelAssignmentRows.Single(item => item.Reference == MachineObjectReference.Reel(0));
            var selectedChoice = row.Choices.Single(choice => choice.AssetPath == "standard");
            WriteCabinet(project, "Vogue", [new("standard", "Standard", 210, 50)]);
            File.SetLastWriteTimeUtc(new ProjectAssetPathService().ResolveProjectRelativePath(project, cabinetPath), DateTime.UtcNow.AddSeconds(2));

            tab.RefreshMachineCompositionChoices();

            Assert.Same(row, tab.MachineReelAssignmentRows.Single(item => item.Reference == MachineObjectReference.Reel(0)));
            Assert.Same(selectedChoice, row.Choices.Single(choice => choice.AssetPath == "standard"));
            Assert.Equal("standard", row.SelectedReelAssetPath);
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

}
