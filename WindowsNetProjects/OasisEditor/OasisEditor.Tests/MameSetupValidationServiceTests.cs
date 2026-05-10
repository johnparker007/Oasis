using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MameSetupValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_SelectedVersionMissingFromManagedInstall_ReportsIssue()
    {
        var installRoot = Path.Combine(Path.GetTempPath(), $"oasis-mame-test-{Guid.NewGuid():N}");
        var pluginSource = Path.Combine(installRoot, "plugins");
        Directory.CreateDirectory(pluginSource);
        File.WriteAllText(Path.Combine(pluginSource, "init.lua"), "-- test");
        File.WriteAllText(Path.Combine(pluginSource, "plugin.json"), "{}");

        var executablePath = Path.Combine(installRoot, "mame0281", "mame.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        File.WriteAllText(executablePath, string.Empty);

        var sut = new MameSetupValidationService(
            new MamePluginAssetValidator(),
            new StubMameVersionCatalogService("0287"));

        try
        {
            var state = await sut.ValidateAsync(
                new MameSetupValidationRequest(
                    executablePath,
                    installRoot,
                    pluginSource,
                    "0287",
                    "https://github.com/mamedev/mame/releases"),
                CancellationToken.None);

            Assert.Equal(MameSetupPhase.NeedsAttention, state.Phase);
            Assert.Contains(state.Issues, issue => issue.Contains("0287", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(installRoot))
            {
                Directory.Delete(installRoot, true);
            }
        }
    }

    private sealed class StubMameVersionCatalogService(string latestVersion) : IMameVersionCatalogService
    {
        public Task<MameVersionCatalog> GetCatalogAsync(string releaseSource, CancellationToken cancellationToken)
            => Task.FromResult(new MameVersionCatalog([latestVersion], latestVersion, DateTimeOffset.UtcNow, false, releaseSource));

        public Task<MameVersionCatalog> GetLatestVersionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new MameVersionCatalog([latestVersion], latestVersion, DateTimeOffset.UtcNow, false, "stub"));
    }
}
