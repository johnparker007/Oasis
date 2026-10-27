using Avalonia.Controls;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

[Collection("Avalonia headless UI")]
public sealed class OutputPaneTests
{
    [Fact]
    public void PaneFiltersAndCopiesVisibleOrSelectedRows()
    {
        HeadlessTestHost.Run(() =>
        {
            using var service = new OutputLogService();
            var pane = new OutputPaneControl(service);
            var layout = Assert.IsType<Grid>(pane.Content);
            var toolbar = Assert.IsAssignableFrom<Panel>(layout.Children[0]);
            var search = Assert.Single(toolbar.Children.OfType<TextBox>());
            var filters = Assert.Single(toolbar.Children.OfType<StackPanel>());
            service.Add("opened project", OutputSeverity.Info);
            service.Add("asset warning", OutputSeverity.Warning);
            service.Add("save failed", OutputSeverity.Error);
            service.ShowInfo = false;
            service.SearchText = "asset";

            Assert.Single(service.FilteredEntries);
            Assert.Equal("asset", search.Text);
            Assert.False(filters.Children.OfType<CheckBox>().Single(check => Equals(check.Content, "Info")).IsChecked);
            Assert.True(filters.Children.OfType<CheckBox>().Single(check => Equals(check.Content, "Warnings")).IsChecked);
            Assert.Contains("asset warning", pane.BuildCopyTextForSelection());
            Assert.DoesNotContain("save failed", pane.BuildCopyTextForSelection());
            var selected = service.Entries.Where(entry => entry.Severity == OutputSeverity.Error);
            Assert.Contains("save failed", pane.BuildCopyTextForSelection(selected));
            pane.Dispose();
        });
    }

    [Fact]
    public void DiskLoggingRequiresExplicitPathAndAppendsWithoutRotatingExistingContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "OasisOutput_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "chosen.log");
            File.WriteAllText(path, "existing line" + Environment.NewLine);
            using (var optIn = new OutputLogService(path)) optIn.Add("new line", OutputSeverity.Error);
            using var defaultService = new OutputLogService();
            Assert.Null(defaultService.DiskLogPath);
            var lines = File.ReadAllLines(path);
            Assert.Equal("existing line", lines[0]);
            Assert.Contains("[Error] new line", lines[1]);
            Assert.False(File.Exists(Path.Combine(root, "Editor.log")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ServiceDeduplicatesConsecutiveStatusAndBoundsRetainedEntries()
    {
        HeadlessTestHost.Run(() =>
        {
            using var service = new OutputLogService();
            service.Add("Ready");
            service.Add("Ready");
            Assert.Single(service.Entries);
            for (var i = 0; i < OutputLogService.MaximumEntries + 1; i++) service.Add("Message " + i);
            Assert.Equal(OutputLogService.MaximumEntries, service.Entries.Count);
            Assert.Equal("Message " + OutputLogService.MaximumEntries, service.Entries[^1].Message);
        });
    }
}
