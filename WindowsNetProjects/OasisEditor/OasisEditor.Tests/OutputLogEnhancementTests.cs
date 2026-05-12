using System.Diagnostics;
using System.IO;
using Xunit;

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

    [Fact]
    public void ViewModel_CopySelection_ExcludesHiddenFilteredRows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vm = new OutputLogViewModel(new OutputLogDiskWriter(root));
            vm.AddOutputEntry("info", OutputLogStatus.Info);
            vm.AddOutputEntry("warn", OutputLogStatus.Warning);
            vm.AddOutputEntry("error", OutputLogStatus.Error);
            vm.ShowWarningLogs = false;

            vm.UpdateSelectedEntries(vm.OutputEntries);
            var text = vm.BuildClipboardTextForSelection();

            Assert.Contains("[Info] info", text);
            Assert.Contains("[Error] error", text);
            Assert.DoesNotContain("[Warning] warn", text);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ViewModel_CopySelectionHeader_UsesSingularForSingleItem()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vm = new OutputLogViewModel(new OutputLogDiskWriter(root));
            vm.AddOutputEntry("info", OutputLogStatus.Info);
            vm.UpdateSelectedEntries(vm.OutputEntries.Take(1));
            Assert.Equal("Copy Row", vm.CopySelectionHeader);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    [Fact]
    public void ViewModel_OpenLog_UsesShellLauncher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var launcher = new FakeOutputLogShellLauncher();
            var vm = new OutputLogViewModel(new OutputLogDiskWriter(root), launcher);
            var opened = vm.TryOpenCurrentLog(out var failureReason);

            Assert.True(opened);
            Assert.Null(failureReason);
            Assert.NotNull(launcher.LastStartInfo);
            Assert.Equal(vm.CurrentLogPath, launcher.LastStartInfo!.FileName);
            Assert.True(launcher.LastStartInfo.UseShellExecute);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ViewModel_ShowInExplorer_UsesShellLauncher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oasis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var launcher = new FakeOutputLogShellLauncher();
            var vm = new OutputLogViewModel(new OutputLogDiskWriter(root), launcher);
            var opened = vm.TryShowLogInExplorer(out var failureReason);

            Assert.True(opened);
            Assert.Null(failureReason);
            Assert.NotNull(launcher.LastStartInfo);
            Assert.Equal("explorer.exe", launcher.LastStartInfo!.FileName);
            Assert.Contains(vm.LogDirectoryPath, launcher.LastStartInfo.Arguments);
            Assert.True(launcher.LastStartInfo.UseShellExecute);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class FakeOutputLogShellLauncher : IOutputLogShellLauncher
    {
        public ProcessStartInfo? LastStartInfo { get; private set; }

        public bool TryLaunch(ProcessStartInfo startInfo, out string? failureReason)
        {
            LastStartInfo = startInfo;
            failureReason = null;
            return true;
        }
    }

}
