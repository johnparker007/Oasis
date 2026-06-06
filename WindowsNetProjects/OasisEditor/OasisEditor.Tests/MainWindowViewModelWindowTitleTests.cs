using System.Reflection;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MainWindowViewModelWindowTitleTests
{
    [Fact]
    public void WindowTitle_WithLoadedProject_IncludesProjectName()
    {
        var vm = CreateUninitializedViewModel();
        SetPrivateField(vm, "_loadedProject", new EditorProject
        {
            Name = "Andy Capp",
            ProjectFilePath = "C:/temp/Andy Capp.oasisproj",
            ProjectDirectory = "C:/temp",
            AssetsDirectory = "C:/temp/Assets",
            MachinesDirectory = "C:/temp/Machines",
            GeneratedDirectory = "C:/temp/Generated"
        });

        Assert.Equal("Andy Capp - Oasis Editor", vm.WindowTitle);
    }

    [Fact]
    public void WindowTitle_WithoutLoadedProject_UsesEditorTitle()
    {
        var vm = CreateUninitializedViewModel();

        Assert.Equal("Oasis Editor", vm.WindowTitle);
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
}
