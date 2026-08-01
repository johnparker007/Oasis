using System.Xml.Linq;

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
        AssertCategoryVisibility(categoryRoots.Single(IsImpactFabricCategoryRoot), "Impact / Fabric");
    }

    [Fact]
    public void ImpactFabricVisibilityBelongsToOuterScrollViewer()
    {
        var impactRoot = LoadView()
            .Descendants(Presentation + "ScrollViewer")
            .Single(IsImpactFabricCategoryRoot);

        Assert.NotNull(impactRoot.Element(Presentation + "ScrollViewer.Style"));
        Assert.Null(impactRoot.Element(Presentation + "Grid")?.Element(Presentation + "Grid.Style"));
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

    private static bool IsImpactFabricCategoryRoot(XElement element)
    {
        return HasHeading(element, "Impact / Fabric");
    }

    private static bool HasHeading(XElement element, string heading)
    {
        return element.Descendants(Presentation + "TextBlock")
            .Any(textBlock => textBlock.Attribute("Text")?.Value == heading);
    }
}
