using System.Reflection;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MainWindowViewModelInputDiagnosticsTests
{
    [Fact]
    public void RefreshInputMapDiagnostics_WithDuplicateShortcuts_UpdatesWarningCount()
    {
        var vm = CreateUninitializedViewModel();

        var project = new EditorProject
        {
            Name = "Test",
            ProjectFilePath = "C:/temp/test.oasisproj",
            ProjectDirectory = "C:/temp",
            AssetsDirectory = "C:/temp/Assets",
            MachinesDirectory = "C:/temp/Machines",
            GeneratedDirectory = "C:/temp/Generated",
            FruitMachinePlatform = FruitMachinePlatformType.MPU4
        }.WithInputDefinitions(new[]
        {
            new InputDefinitionModel { Id = "a", Name = "A", Kind = InputDefinitionKind.Button, ButtonNumber = "1", KeyboardShortcut = "Space" },
            new InputDefinitionModel { Id = "b", Name = "B", Kind = InputDefinitionKind.Button, ButtonNumber = "2", KeyboardShortcut = "Space" }
        });

        SetPrivateField(vm, "_loadedProject", project);
        SetPrivateField(vm, "_selectedFruitMachinePlatform", FruitMachinePlatformType.MPU4);
        SetPrivateField(vm, "_inputMapDiagnosticsService", new InputMapDiagnosticsService(new MameInputPortResolver()));
        SetPrivateField(vm, "_outputLog", new OutputLogViewModel());

        InvokePrivateMethod(vm, "RefreshInputMapDiagnostics");

        Assert.True(vm.HasInputMapDiagnostics);
        Assert.Equal(2, vm.InputMapWarningCount);
        Assert.Equal(2, vm.InputMapDiagnostics.Count(d => d.Code == "input.duplicate_shortcut"));
    }

    private static MainWindowViewModel CreateUninitializedViewModel()
    {
        return (MainWindowViewModel)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(MainWindowViewModel));
    }

    private static void SetPrivateField<T>(MainWindowViewModel vm, string name, T value)
    {
        var field = typeof(MainWindowViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, value);
    }

    private static void InvokePrivateMethod(MainWindowViewModel vm, string name)
    {
        var method = typeof(MainWindowViewModel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, null);
    }
}
