using System.Text.Json;
using Xunit;

namespace OasisEditor.Tests;

public sealed class Mpu3ProjectSettingsViewModelTests
{
    [Fact]
    public void ProgramRomEditsUpdateCorrectBackingEntriesAndInvokeSave()
    {
        var model = new Mpu3ProjectSettings();
        var saves = 0;
        var viewModel = new Mpu3ProjectSettingsViewModel(model, updated => { Assert.Same(model, updated); saves++; });

        for (var slot = 0; slot < 4; slot++)
        {
            viewModel.ProgramRoms[slot].Path = $"rom-{slot}.bin";
            Assert.Equal(slot, model.ProgramRoms[slot].Slot);
            Assert.Equal($"rom-{slot}.bin", model.ProgramRoms[slot].Path);
        }

        Assert.Equal(4, saves);
    }

    [Fact]
    public void ReelEditsUpdateCorrectBackingReelAndInvokeSave()
    {
        var model = new Mpu3ProjectSettings();
        var saves = 0;
        var viewModel = new Mpu3ProjectSettingsViewModel(model, _ => saves++);

        viewModel.Reels[2].Steps = 128;
        viewModel.Reels[2].OptoStart = 7;
        viewModel.Reels[2].OptoEnd = 11;
        viewModel.Reels[2].OptoInvert = true;

        Assert.Equal(2, model.Reels[2].ReelIndex);
        Assert.Equal(128, model.Reels[2].Steps);
        Assert.Equal(7, model.Reels[2].OptoStart);
        Assert.Equal(11, model.Reels[2].OptoEnd);
        Assert.True(model.Reels[2].OptoInvert);
        Assert.Equal(4, saves);
    }

    [Fact]
    public void EveryDipUpdatesItsIndexedBooleanAndInvokesSave()
    {
        var model = new Mpu3ProjectSettings();
        var saves = 0;
        var viewModel = new Mpu3ProjectSettingsViewModel(model, _ => saves++);

        for (var index = 0; index < 16; index++)
        {
            Assert.Equal(index, viewModel.Dips[index].Index);
            viewModel.Dips[index].IsEnabled = true;
            Assert.True(model.Dips[index]);
        }

        Assert.Equal(16, saves);
    }

    [Fact]
    public void ViewModelEditsSurviveCurrentSchemaSerializationRoundTrip()
    {
        var model = new Mpu3ProjectSettings();
        var viewModel = new Mpu3ProjectSettingsViewModel(model, _ => { });
        viewModel.ProgramRoms[3].Path = "fourth.rom";
        viewModel.Dips[15].IsEnabled = true;

        var restored = JsonSerializer.Deserialize<Mpu3ProjectSettings>(JsonSerializer.Serialize(model))!;
        Assert.Equal("fourth.rom", restored.ProgramRoms[3].Path);
        Assert.True(restored.Dips[15]);
        Assert.Equal(6, EditorProject.CurrentSchemaVersion);
        Assert.Null(typeof(Mpu3ProgramRomSettingsViewModel).GetProperty("LoadAddress"));
    }

    [Fact]
    public void NewViewModelTargetsOnlyItsSuppliedProjectModel()
    {
        var first = new Mpu3ProjectSettings();
        var second = new Mpu3ProjectSettings();
        var firstViewModel = new Mpu3ProjectSettingsViewModel(first, _ => { });
        var secondViewModel = new Mpu3ProjectSettingsViewModel(second, _ => { });

        firstViewModel.ProgramRoms[0].Path = "first.rom";
        secondViewModel.ProgramRoms[0].Path = "second.rom";

        Assert.Equal("first.rom", first.ProgramRoms[0].Path);
        Assert.Equal("second.rom", second.ProgramRoms[0].Path);
        Assert.NotSame(firstViewModel.ProgramRoms[0], secondViewModel.ProgramRoms[0]);
    }
}
