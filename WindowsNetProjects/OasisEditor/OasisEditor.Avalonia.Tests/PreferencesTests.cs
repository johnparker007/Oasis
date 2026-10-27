using OasisEditor;
using OasisEditor.Avalonia;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class PreferencesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OasisAvaloniaPreferences", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PreferencesStorePersistsProcessingAndPlayerValuesSeparately()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "prefs.json");
        var store = new AvaloniaEditorPreferencesStore(path);
        store.Save(new AvaloniaEditorPreferences
        {
            Processing = new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Custom, CustomMaximumWorkers = Environment.ProcessorCount + 10 },
            Player = new AvaloniaPlayerPreferences
            {
                ExecutablePath = "  C:\\Oasis\\OasisPlayer.exe  ",
                Fullscreen = true,
                PreviewWidth = 1600,
                PreviewHeight = 900
            }
        });

        var saved = store.Load();
        Assert.Equal(CpuImageProcessingMode.Custom, saved.Processing.CpuMode);
        Assert.Equal(Math.Max(1, Environment.ProcessorCount), saved.Processing.CustomMaximumWorkers);
        Assert.Equal("C:\\Oasis\\OasisPlayer.exe", saved.Player.ExecutablePath);
        Assert.True(saved.Player.Fullscreen);
        Assert.Equal(1600, saved.Player.PreviewWidth);
        Assert.Equal(900, saved.Player.PreviewHeight);
    }

    [Fact]
    public void PreferencesRejectInvalidPlayerDimensionsAndDefaultToAutoAndWindowedPreview()
    {
        var defaults = new AvaloniaEditorPreferences();
        Assert.Equal(CpuImageProcessingMode.Auto, defaults.Processing.CpuMode);
        Assert.False(defaults.Player.Fullscreen);
        Assert.Equal(OasisPlayerLaunchService.DefaultPreviewWidth, defaults.Player.PreviewWidth);
        Assert.Equal(OasisPlayerLaunchService.DefaultPreviewHeight, defaults.Player.PreviewHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => (defaults with
        { Player = defaults.Player with { PreviewWidth = 0 } }).Normalize());
    }

    [Fact]
    public async Task HeadlessPreferencesRequireExplicitWorkspaceStoreAndRejectExternalOrMalformedFiles()
    {
        await HeadlessTestHost.RunAsync(async () =>
        {
            Directory.CreateDirectory(_root);
            var session = new AuthoringSession(_root);
            session.CreateProject("Preferences");
            using var capture = new WorkspaceCapture(session);
            var view = capture.View;
            var path = Path.Combine(_root, "preferences.json");
            view.ConfigurePreferencesPersistence(path);
            view.UpdatePreferences(new AvaloniaEditorPreferences
            {
                Processing = new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Maximum },
                Player = new AvaloniaPlayerPreferences { ExecutablePath = "C:\\Oasis\\OasisPlayer.exe", Fullscreen = true, PreviewWidth = 1440, PreviewHeight = 900 }
            });
            Assert.Equal("C:\\Oasis\\OasisPlayer.exe", new AvaloniaEditorPreferencesStore(path).Load().Player.ExecutablePath);
            Assert.Throws<InvalidDataException>(() => view.ConfigurePreferencesPersistence(Path.Combine(_root, "..", "outside.json")));

            File.WriteAllText(path, "{malformed");
            Assert.Throws<IOException>(() => view.UpdatePreferences(new AvaloniaEditorPreferences
            { Processing = new ProcessingPreferences { CpuMode = CpuImageProcessingMode.Auto } }));
            Assert.Throws<InvalidDataException>(() => new AvaloniaEditorPreferencesStore(path).Load());
            await Task.CompletedTask;
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
