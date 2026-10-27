using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OasisEditor;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[CollectionDefinition("Avalonia headless UI", DisableParallelization = true)]
public sealed class AvaloniaHeadlessUiCollection { }

[Collection("Avalonia headless UI")]
public sealed class MachineAuthoringSessionTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "OasisMachineAuthoring", Guid.NewGuid().ToString("N"));

    public MachineAuthoringSessionTests() => Directory.CreateDirectory(workspace);

    [Fact]
    public void FullCurrentMachineMetadataRoundTripsThroughNativeSession()
    {
        var projectFile = CreateProject();
        var machine = OpenMachine(projectFile);
        var impact = new System6NativeRomSettings
        {
            ProgramRom1Path = "Roms/program.bin", SoundRom2Path = "Roms/sound.bin", FlashSwitch = true,
            CoinCommunicationStyle = AmberCoinCommunicationStyle.Parallel, CoinPulseCycles = 123456,
            ReelOptos = [new System6ReelOptoSettings { ReelIndex = 0, Steps = 144, OptoStart = 3, OptoEnd = 8 }],
            Coins = [new System6CoinSettings { Name = "Token", Num = 0, Enabled = true, CoinValue = 25, CounterOut = 9 }]
        };
        var linkedVisualId = Guid.NewGuid();
        machine.UpdateDocument(current => current with
        {
            DisplayName = "Rich Impact Machine",
            CabinetAsset = AssetReference.Project("Assets/Cabinet3D/Cabinet/asset.cabinet3d"),
            SurfaceAssignments = [new MachineSurfaceAssignment("main", "Assets/Faces/Main/asset.face")],
            ReelAssignments = [new MachineReelAssignment(MachineObjectReference.Reel(2), AssetReference.Library("Reels/Classic/asset.reel"))],
            PrefabDependencies = [new MachinePrefabDeclaration("ball", AssetReference.Project("Assets/Object3D/Ball/asset.object3d"))],
            Anchors = [new MachineAnchor("player", "Player", new MachineVector3(1, 2, 3), new MachineVector3(0, 90, 0))],
            GeometrySurfaces = [new MachineGeometrySurface("table", new MachineVector3(0, 1, 0), MachineVector3.Zero, 2, 4)],
            Interaction = new MachineInteractionDefinition("Pool", "ball", "table"),
            InputDefinitions = [new InputDefinitionModel { Id = "start", Name = "Start", Kind = InputDefinitionKind.Button, ButtonNumber = "3", KeyboardShortcut = "Enter", LinkedVisualElementId = linkedVisualId }],
            Runtime = new EmulationRuntimeDefinition(FruitMachinePlatformType.Impact, impact)
        });
        machine.Save();

        var loaded = OpenMachine(projectFile);
        var saved = Assert.IsType<EmulationRuntimeDefinition>(loaded.Document!.Runtime);
        var settings = Assert.IsType<System6NativeRomSettings>(saved.PlatformSettings);
        Assert.Equal("Roms/program.bin", settings.ProgramRom1Path);
        Assert.Equal("Roms/sound.bin", settings.SoundRom2Path);
        Assert.True(settings.FlashSwitch);
        Assert.Equal(AmberCoinCommunicationStyle.Parallel, settings.CoinCommunicationStyle);
        Assert.Equal(123456u, settings.CoinPulseCycles);
        Assert.Equal(144, Assert.Single(settings.ReelOptos).Steps);
        Assert.Equal(25, Assert.Single(settings.Coins).CoinValue);
        Assert.Equal("main", Assert.Single(loaded.Document.SurfaceAssignments).TargetId);
        Assert.Equal(AssetReferenceScope.Library, Assert.Single(loaded.Document.ReelAssignments).ReelAsset.Scope);
        Assert.Equal("ball", Assert.Single(loaded.Document.PrefabDependencies).Alias);
        Assert.Equal(new MachineVector3(1, 2, 3), Assert.Single(loaded.Document.Anchors).Position);
        Assert.Equal("table", Assert.Single(loaded.Document.GeometrySurfaces).Id);
        Assert.Equal("Pool", loaded.Document.Interaction.Kind);
        Assert.Equal(linkedVisualId, loaded.Document.InputDefinitions.Single().LinkedVisualElementId);
    }

    [Fact]
    public void InvalidEditsAreAtomicNoOpsPreserveRedoAndSaveDetectsExternalChanges()
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        var original = session.Document!;
        Assert.False(session.UpdateDocument(current => current));
        session.SetDisplayName("First");
        session.SetDisplayName("Second");
        session.Undo();
        Assert.True(session.CanRedo);
        Assert.False(session.SetDisplayName("First"));
        Assert.True(session.CanRedo);

        Assert.Throws<InvalidOperationException>(() => session.UpdateDocument(current => current with { Anchors = [new MachineAnchor("bad id", "Broken", MachineVector3.Zero, MachineVector3.Zero)] }));
        Assert.Equal("First", session.Document!.DisplayName);
        Assert.True(session.IsDirty);
        session.Redo(); session.Save();
        Assert.False(session.IsDirty);
        session.SetDisplayName("Local change");
        File.AppendAllText(session.MachinePath!, " ");
        Assert.Throws<IOException>(session.Save);
        Assert.Equal("Local change", session.Document!.DisplayName);
        Assert.Equal(original.Id, session.Document.Id);
    }

    [Fact]
    public void MutableSettingsAndListsAreClonedAcrossSnapshotsAndHistory()
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        session.SetRuntime(EmulationRuntimeDefinition.Create(FruitMachinePlatformType.Impact));
        session.Save();

        var exposedSnapshot = session.Document!;
        Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)exposedSnapshot.Runtime).PlatformSettings).ProgramRom1Path = "outside-history.bin";
        exposedSnapshot.InputDefinitions.Add(new InputDefinitionModel { Id = "outside", Name = "Outside", Kind = InputDefinitionKind.Button });
        Assert.Empty(session.Document!.InputDefinitions);
        Assert.Equal(string.Empty, Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)session.Document.Runtime).PlatformSettings).ProgramRom1Path);
        Assert.False(session.IsDirty);

        session.UpdateDocument(current =>
        {
            Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)current.Runtime).PlatformSettings).ProgramRom1Path = "authored.bin";
            return current;
        });
        Assert.Equal("authored.bin", Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)session.Document!.Runtime).PlatformSettings).ProgramRom1Path);
        session.Undo();
        Assert.Equal(string.Empty, Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)session.Document!.Runtime).PlatformSettings).ProgramRom1Path);
        session.Redo();
        Assert.Equal("authored.bin", Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)session.Document!.Runtime).PlatformSettings).ProgramRom1Path);
    }

    [Fact]
    public void OasisScriptIsValidatedBeforeHistoryAndPersistedWithConflictProtection()
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        session.SetOasisRuntime("Script Machine", "on machine.started { }");
        Assert.True(session.IsDirty);
        Assert.Throws<InvalidDataException>(() => session.SetOasisBehaviorSource("on machine.started { let = ; }"));
        Assert.Equal("on machine.started { }", session.BehaviorSource);
        session.Save();
        var scriptPath = Path.Combine(Path.GetDirectoryName(session.MachinePath!)!, "behavior.oasis");
        Assert.Equal("on machine.started { }", File.ReadAllText(scriptPath));
        Assert.False(session.IsDirty);

        var reopened = OpenMachine(projectFile);
        Assert.Equal("on machine.started { }", reopened.BehaviorSource);
        reopened.SetOasisBehaviorSource("state ready=false;");
        File.WriteAllText(scriptPath, "external edit");
        Assert.Throws<IOException>(reopened.Save);
        Assert.Equal("state ready=false;", reopened.BehaviorSource);
    }

    [Theory]
    [InlineData(FruitMachinePlatformType.None)]
    [InlineData(FruitMachinePlatformType.Impact)]
    [InlineData(FruitMachinePlatformType.MPU5)]
    [InlineData(FruitMachinePlatformType.Epoch)]
    [InlineData(FruitMachinePlatformType.MPU3)]
    [InlineData(FruitMachinePlatformType.MaygayM1)]
    [InlineData(FruitMachinePlatformType.Scorpion4)]
    public void EverySupportedPlatformRetainsPopulatedNativeSettings(FruitMachinePlatformType platform)
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        var runtime = EmulationRuntimeDefinition.Create(platform);
        Assert.True(SetStringSetting(runtime.PlatformSettings, $"Roms/{platform}.bin"));
        var expectedSettings = JsonSerializer.Serialize(runtime.PlatformSettings);
        session.UpdateDocument(current => current with { Runtime = runtime });
        session.Save();

        var reloaded = Assert.IsType<EmulationRuntimeDefinition>(OpenMachine(projectFile).Document!.Runtime);
        Assert.Equal(platform, reloaded.Platform);
        Assert.Equal(runtime.PlatformSettings.GetType(), reloaded.PlatformSettings.GetType());
        Assert.Equal(expectedSettings, JsonSerializer.Serialize(reloaded.PlatformSettings));
    }

    [Fact]
    public void FmlInputsRequireIntentionalApplyAndPersistAsOneUndoableMachineEdit()
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        var recovered = new[]
        {
            new InputDefinitionModel { Id = "coin-in", Name = "Coin 1", Kind = InputDefinitionKind.Coin, ButtonNumber = "4", CoinInput = true, CoinChannel = 1, CoinValue = 10, Inverted = true, RawMfmeShortcut = "MFME:COIN1", KeyboardShortcut = "5", Notes = "verified mapping" }
        };
        Assert.True(session.ApplyRecoveredInputs(recovered));
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Empty(session.Document!.InputDefinitions);
        Assert.True(session.CanRedo);
        Assert.False(session.ApplyRecoveredInputs([]));
        Assert.True(session.CanRedo);
        session.Redo(); session.Save();

        var input = Assert.Single(OpenMachine(projectFile).Document!.InputDefinitions);
        Assert.Equal("coin-in", input.Id);
        Assert.Equal(1, input.CoinChannel);
        Assert.Equal(10, input.CoinValue);
        Assert.Equal("MFME:COIN1", input.RawMfmeShortcut);
        Assert.Equal("verified mapping", input.Notes);
    }

    [Fact]
    public void InvalidRuntimeAndEscapingAssetOrScriptPathsAreRejectedWithoutMutation()
    {
        var projectFile = CreateProject();
        var session = OpenMachine(projectFile);
        var before = session.Document!;
        Assert.Throws<InvalidOperationException>(() => session.UpdateDocument(current => current with { Runtime = new EmulationRuntimeDefinition(FruitMachinePlatformType.Impact, new Mpu5NativeRomSettings()), Behavior = MachineBehaviorDefinition.OasisScript() }));
        Assert.Throws<InvalidOperationException>(() => session.SetOasisBehaviorSource("state active=true;"));
        Assert.Throws<InvalidOperationException>(() => session.UpdateDocument(current => current with { Runtime = new OasisRuntimeDefinition(), Behavior = new MachineBehaviorDefinition("OasisScript", "../behavior.oasis") }));
        Assert.Throws<InvalidDataException>(() => session.ResolveAsset("../../outside"));
        Assert.Equal(MachineDocumentStorage.Serialize(before), MachineDocumentStorage.Serialize(session.Document!));
        Assert.False(session.IsDirty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void HeadlessMachineInspectorShowsTypedSettingsAndCommitsThemThroughHistory()
    {
        HeadlessTestHost.Run(() =>
        {
        var projectFile = CreateProject();
        var session = new AuthoringSession(workspace); session.OpenProject(projectFile);
        var faces = new FaceAuthoringSession(session);
        using var capture = new WorkspaceCapture(session, faces);
        capture.View.OpenAsset(new AssetSummary("Machine", "Machine", "Assets/Machines/Machine/asset.machine"));
        var image = capture.Capture(width: 1280, height: 800, theme: "Dark");
        Assert.True(image.Length > 5000);
        Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "PLATFORM SETTINGS");
        Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Program Rom 1 Path");
        Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "FACE SURFACE ASSIGNMENTS");
        Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "INPUT DEFINITIONS (0)");
        var rom = capture.View.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "MachineSetting_ProgramRom1Path");
        rom.Text = "Roms/program.bin";
        capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Apply emulation settings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var runtime = Assert.IsType<EmulationRuntimeDefinition>(capture.View.Machines.Document!.Runtime);
        Assert.True(Assert.IsType<System6NativeRomSettings>(runtime.PlatformSettings).ProgramRom1Path == "Roms/program.bin", string.Join(" | ", capture.View.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        Assert.True(capture.View.Machines.CanUndo);
        capture.View.Machines.Undo();
        Assert.Equal(string.Empty, Assert.IsType<System6NativeRomSettings>(((EmulationRuntimeDefinition)capture.View.Machines.Document!.Runtime).PlatformSettings).ProgramRom1Path);
        });
    }

    [Fact]
    public void InvalidProjectDoesNotReplaceSharedProjectBinding()
    {
        var projectFile = CreateProject();
        var shared = new AuthoringSession(workspace); shared.OpenProject(projectFile);
        var machines = new MachineAuthoringSession(shared); machines.OpenProject(projectFile);
        var invalidFolder = Path.Combine(workspace, "Invalid"); Directory.CreateDirectory(invalidFolder);
        var invalidProject = Path.Combine(invalidFolder, "Invalid.oasisproj");
        File.WriteAllText(invalidProject, "{\"name\":\"Invalid\",\"version\":9,\"layout\":{\"assets\":\"Elsewhere\",\"generated\":\"Generated\"}}");
        Assert.Throws<InvalidDataException>(() => machines.OpenProject(invalidProject));
        Assert.Equal(Path.GetFullPath(projectFile), Path.GetFullPath(shared.ProjectFilePath!));
        Assert.True(machines.IsProjectCurrent);
        Assert.Single(machines.ListMachines());
    }

    [Fact]
    public void SwitchingSharedProjectKeepsDirtyMachineButBlocksStaleEditsAndSave() => AssertStaleBinding(closeProject: false);

    [Fact]
    public void ClosingSharedProjectKeepsDirtyMachineButBlocksStaleEditsAndSave() => AssertStaleBinding(closeProject: true);

    [Fact]
    public void MachineAssignmentCreationUsesExistingRegisteredAssetsAndExplainsMissingAssets()
    {
        HeadlessTestHost.Run(() =>
        {
            var projectFile = CreateProject();
            var shared = new AuthoringSession(workspace); shared.OpenProject(projectFile);
            var faces = new FaceAuthoringSession(shared);
            using var capture = new WorkspaceCapture(shared, faces);
            var machineAsset = new AssetSummary("Machine", "Machine", "Assets/Machines/Machine/asset.machine");
            capture.View.OpenAsset(machineAsset); capture.Capture();
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add Face assignment")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Contains(capture.View.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("No registered Faces asset", StringComparison.Ordinal) == true);
            faces.CreateFace("AssignedFace");
            var objectDirectory = Path.Combine(Path.GetDirectoryName(projectFile)!, "Assets", "Object3D", "Existing"); Directory.CreateDirectory(objectDirectory);
            File.WriteAllText(Path.Combine(objectDirectory, "asset.object3d"), "{}");
            capture.View.OpenAsset(machineAsset); capture.Capture();
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add Face assignment")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var face = Assert.Single(capture.View.Machines.Document!.SurfaceAssignments);
            Assert.Equal("Assets/Faces/AssignedFace/asset.face", face.FaceAssetPath);
            capture.Capture();
            capture.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add Prefab declaration")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var prefab = Assert.Single(capture.View.Machines.Document!.PrefabDependencies);
            Assert.Equal("Assets/Object3D/Existing/asset.object3d", prefab.PrefabAsset.Path);
        });
    }

    private void AssertStaleBinding(bool closeProject)
    {
        var projectFile = CreateProject();
        var shared = new AuthoringSession(workspace); shared.OpenProject(projectFile);
        var machines = new MachineAuthoringSession(shared); machines.OpenProject(projectFile);
        machines.OpenMachine(machines.ListMachines().Single().ManifestPath);
        machines.SetDisplayName("First local edit"); machines.SetDisplayName("Second local edit"); machines.Undo();
        if (closeProject) shared.CloseProject();
        else
        {
            var otherDirectory = new ProjectScaffolder().CreateProject("Other", workspace);
            shared.OpenProject(Path.Combine(otherDirectory, "Other.oasisproj"));
        }
        Assert.False(machines.IsProjectCurrent);
        Assert.True(machines.IsDirty);
        Assert.Equal("First local edit", machines.Document!.DisplayName);
        Assert.Throws<InvalidOperationException>(machines.Undo);
        Assert.Throws<InvalidOperationException>(machines.Redo);
        Assert.Throws<InvalidOperationException>(machines.Save);
        Assert.Equal("First local edit", machines.Document!.DisplayName);
        Assert.True(machines.IsDirty);
    }

    private string CreateProject() => new ProjectScaffolder().CreateProject("Machine", workspace) is var directory
        ? Path.Combine(directory, "Machine.oasisproj") : throw new InvalidOperationException();

    private MachineAuthoringSession OpenMachine(string projectFile)
    {
        var project = new AuthoringSession(workspace); project.OpenProject(projectFile);
        var session = new MachineAuthoringSession(project); session.OpenProject(projectFile);
        var path = session.ListMachines().Single().ManifestPath;
        session.OpenMachine(path);
        return session;
    }

    private static bool SetStringSetting(object target, string value)
    {
        foreach (var property in target.GetType().GetProperties().Where(property => property.CanWrite && property.GetIndexParameters().Length == 0))
        {
            if (property.PropertyType == typeof(string)) { property.SetValue(target, value); return true; }
            if (property.GetValue(target) is System.Collections.IList list)
                foreach (var item in list) if (item is not null && SetStringSetting(item, value)) return true;
        }
        return false;
    }

    public void Dispose() { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
}
