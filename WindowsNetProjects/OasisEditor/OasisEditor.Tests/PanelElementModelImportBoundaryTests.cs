using System;
using System.Linq;
using Xunit;

namespace OasisEditor.Tests;

public sealed class PanelElementModelImportBoundaryTests
{
    [Fact]
    public void PanelElementModel_DoesNotExposeMfmeSpecificPropertyNames()
    {
        var properties = typeof(PanelElementModel).GetProperties();

        Assert.DoesNotContain(properties, property =>
            property.Name.Contains("Mfme", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("ExtractComponent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PanelElementImportSourceModel_UsesGenericProvenanceFields()
    {
        var properties = typeof(PanelElementImportSourceModel)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(new[] { "Format", "Reference" }, properties);
    }
}
