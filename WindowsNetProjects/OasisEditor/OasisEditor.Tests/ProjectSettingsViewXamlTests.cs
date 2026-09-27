using System.Xml.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ProjectSettingsViewXamlTests
{
    [Fact]
    public void ProjectSettingsContainsOnlyProjectScopedInformation()
    {
        var xaml = Read("ProjectSettingsView.xaml");
        Assert.Contains("Project name", xaml);
        Assert.Contains("Project file", xaml);
        Assert.Contains("Assets folder", xaml);
        Assert.DoesNotContain("Fruit machine platform", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Platform Settings", xaml);
        Assert.DoesNotContain("SelectedFruitMachinePlatform", xaml);
        Assert.DoesNotContain("FabricSettingsView", xaml);
        Assert.DoesNotContain("Program ROM", xaml);
        Assert.DoesNotContain("System6", xaml);
    }

    [Fact]
    public void MachineDetailsHostsDocumentScopedRuntimeEditorAndEverySupportedPlatformView()
    {
        var xaml = Read("DocumentEditorView.xaml");
        Assert.Contains("MachineRuntimeSettings", xaml);
        Assert.Contains("Runtime Settings", xaml);
        Assert.Contains("MachineRuntimeKind", xaml);
        Assert.Contains("MachinePlatforms", xaml);
        Assert.Contains("ImpactRuntimeSettingsView", xaml);
        Assert.Contains("Mpu5FabricSettingsView", xaml);
        Assert.Contains("EpochFabricSettingsView", xaml);
        Assert.Contains("Mpu3FabricSettingsView", xaml);
        Assert.Contains("M1FabricSettingsView", xaml);
        Assert.Contains("Scorpion4FabricSettingsView", xaml);
        Assert.Contains("No emulation platform is configured", xaml);
        Assert.Equal(EmulationRuntimePlatforms.Supported, new[] { FruitMachinePlatformType.None, FruitMachinePlatformType.Impact, FruitMachinePlatformType.MPU5, FruitMachinePlatformType.Epoch, FruitMachinePlatformType.MPU3, FruitMachinePlatformType.MaygayM1, FruitMachinePlatformType.Scorpion4 });
    }

    [Fact]
    public void ImpactRuntimeEditorRetainsLogicalSectionsAndMachineTerminology()
    {
        var xaml = Read("ImpactRuntimeSettingsView.xaml");
        Assert.Contains("RuntimeSettingsTabs", xaml);
        Assert.Contains("ROMS", xaml);
        Assert.Contains("Stake/Prize", xaml);
        Assert.Contains("Reels", xaml);
        Assert.Contains("Coins", xaml);
        Assert.DoesNotContain("ProjectSettingsTabs", xaml);
    }

    [Fact]
    public void Mpu5ViewContainsOnlyMpu5RomBindings()
    {
        var xaml = Read("Mpu5FabricSettingsView.xaml");
        Assert.Contains("Mpu5ProgramRom1Path", xaml);
        Assert.Contains("Mpu5SoundRom4Path", xaml);
        Assert.DoesNotContain("System6", xaml);
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Views", name));
}
