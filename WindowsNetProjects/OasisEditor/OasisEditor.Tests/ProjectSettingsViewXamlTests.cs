using System.Xml.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class ProjectSettingsViewXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void CategoryRootsCollapseUnlessTheirCategoryIsSelected()
    {
        var document = LoadView();
        var contentGrid = document
            .Descendants(Presentation + "Grid")
            .Single(grid => grid.Elements(Presentation + "ScrollViewer").Any(IsGeneralCategoryRoot));
        var categoryRoots = contentGrid.Elements(Presentation + "ScrollViewer").ToArray();

        Assert.Equal(2, categoryRoots.Length);
        AssertCategoryVisibility(categoryRoots.Single(IsGeneralCategoryRoot), "General");
        AssertCategoryVisibility(categoryRoots.Single(IsPlatformSettingsCategoryRoot), "Platform Settings");
    }

    [Fact]
    public void PlatformSettingsUseDedicatedMpu5ViewAndGenericCategory()
    {
        var platformRoot = LoadView()
            .Descendants(Presentation + "ScrollViewer")
            .Single(IsPlatformSettingsCategoryRoot);

        Assert.NotNull(platformRoot.Element(Presentation + "ScrollViewer.Style"));
        Assert.Contains(platformRoot.Descendants(), element => element.Name.LocalName == "Mpu5FabricSettingsView");
        Assert.DoesNotContain("Impact / Fabric", File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Views", "ProjectSettingsView.xaml")));
    }

    [Fact]
    public void FruitMachinePlatformComboBoxIsEnabledAndBoundToPlatformValues()
    {
        var comboBox = LoadView()
            .Descendants(Presentation + "ComboBox")
            .Single(element => element.Attribute("ItemsSource")?.Value == "{Binding FruitMachinePlatformTypes}");

        Assert.False(string.Equals("False", comboBox.Attribute("IsEnabled")?.Value, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("{Binding SelectedFruitMachinePlatform}", comboBox.Attribute("SelectedItem")?.Value);
        Assert.NotEmpty(Enum.GetValues<FruitMachinePlatformType>());
    }

    [Fact]
    public void Mpu5ViewContainsOnlyMpu5RomBindings()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Views", "Mpu5FabricSettingsView.xaml"));
        Assert.Contains("Mpu5ProgramRom1Path", xaml);
        Assert.Contains("Mpu5SoundRom4Path", xaml);
        Assert.DoesNotContain("System6", xaml);
        Assert.Contains("ConfigureReels", xaml);
        Assert.Contains("ConfigureCoins", xaml);
        Assert.Contains("ConfigureMachineOptions", xaml);
    }

    private static XDocument LoadView()
    {
        return XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "ProjectSettingsView.xaml"));
    }

    private static void AssertCategoryVisibility(XElement categoryRoot, string category)
    {
        var style = categoryRoot.Element(Presentation + "ScrollViewer.Style")?.Element(Presentation + "Style");
        Assert.NotNull(style);
        Assert.Contains(
            style.Elements(Presentation + "Setter"),
            setter => setter.Attribute("Property")?.Value == "Visibility"
                && setter.Attribute("Value")?.Value == "Collapsed");

        var triggers = style.Element(Presentation + "Style.Triggers")?.Elements(Presentation + "DataTrigger").ToArray();
        var trigger = Assert.Single(triggers!);
        Assert.Equal("{Binding SelectedProjectSettingsCategory}", trigger.Attribute("Binding")?.Value);
        Assert.Equal(category, trigger.Attribute("Value")?.Value);
        Assert.Contains(
            trigger.Elements(Presentation + "Setter"),
            setter => setter.Attribute("Property")?.Value == "Visibility"
                && setter.Attribute("Value")?.Value == "Visible");
    }

    private static bool IsGeneralCategoryRoot(XElement element)
    {
        return HasHeading(element, "General");
    }

    private static bool IsPlatformSettingsCategoryRoot(XElement element)
    {
        return HasHeading(element, "Platform Settings");
    }

    private static bool HasHeading(XElement element, string heading)
    {
        return element.Descendants(Presentation + "TextBlock")
            .Any(textBlock => textBlock.Attribute("Text")?.Value == heading);
    }
}
