using System.IO;

namespace OasisEditor.Tests;

public sealed class OutputLogEnhancementTests
{
    [Fact]
    public void DiskWriter_RotatesCurrentLogToPrevious()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var current = Path.Combine(root, "Editor.log");
            File.WriteAllText(current, "old");

            var writer = new OutputLogDiskWriter(root);
            writer.Initialize();

            Assert.True(File.Exists(writer.CurrentLogPath));
            Assert.True(File.Exists(writer.PreviousLogPath));
            Assert.Equal("old", File.ReadAllText(writer.PreviousLogPath));
            Assert.Equal(string.Empty, File.ReadAllText(writer.CurrentLogPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ViewModel_FiltersBySeverityWithoutRemovingSourceEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vm = new OutputLogViewModel(new OutputLogDiskWriter(root));
            vm.AddOutputEntry("info", OutputLogStatus.Info);
            vm.AddOutputEntry("warn", OutputLogStatus.Warning);
            vm.AddOutputEntry("error", OutputLogStatus.Error);

            vm.ShowInfoLogs = false;
            var visible = vm.FilteredEntries.Cast<OutputLogEntry>().ToList();

            Assert.Equal(3, vm.OutputEntries.Count);
            Assert.Equal(2, visible.Count);
            Assert.DoesNotContain(visible, e => e.Status == OutputLogStatus.Info);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
