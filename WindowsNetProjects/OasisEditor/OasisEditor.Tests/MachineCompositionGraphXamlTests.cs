using System.Xml.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MachineCompositionGraphXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MachineTabsUseExplicitOasisWorkspaceStyles()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "DocumentEditorView.xaml"));
        var machineTabs = document.Descendants(Presentation + "TabControl").Single();
        var style = machineTabs.Element(Presentation + "TabControl.Style")?.Element(Presentation + "Style");
        Assert.Equal("{StaticResource OasisWorkspaceTabControlStyle}", style?.Attribute("BasedOn")?.Value);
        Assert.All(machineTabs.Elements(Presentation + "TabItem"), item =>
            Assert.Equal("{StaticResource OasisWorkspaceTabItemStyle}", item.Attribute("Style")?.Value));
    }

    [Fact]
    public void OverviewDiagnosticsUsesOasisStyleAndSharedStylesUseSemanticResources()
    {
        var overview = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Views", "MachineCompositionGraphView.xaml"));
        Assert.Equal("{StaticResource OasisDiagnosticsExpanderStyle}",
            overview.Descendants(Presentation + "Expander").Single().Attribute("Style")?.Value);

        var app = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "App.xaml"));
        var required = new[] { "OasisWorkspaceTabControlStyle", "OasisWorkspaceTabItemStyle", "OasisDiagnosticsExpanderStyle", "OasisGraphEdgeLabelBadgeStyle" };
        foreach (var key in required)
        {
            var style = app.Descendants(Presentation + "Style").Single(x => x.Attribute(Xaml + "Key")?.Value == key);
            Assert.Contains("DynamicResource", style.ToString(SaveOptions.DisableFormatting));
        }
    }
}
