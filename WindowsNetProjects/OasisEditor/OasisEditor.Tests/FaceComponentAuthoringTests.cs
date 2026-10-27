using OasisEditor;
using OasisEditor.FacePipeline;
using Xunit;

namespace OasisEditor.Avalonia.Tests;

public sealed class FaceComponentAuthoringTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "OasisFaceComponentTests", Guid.NewGuid().ToString("N"));

    public FaceComponentAuthoringTests() => Directory.CreateDirectory(_workspace);

    [Fact]
    public void CreatesAndEditsButtonAndDisplaysWithNativeMetadataAndHistory()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Components");
        var session = new FaceAuthoringSession(project);
        var path = session.CreateFace("MachineFace");
        var buttonId = session.AddButton("Start", 12, 20, 70, 36, "start");
        var sevenId = session.AddSevenSegmentDisplay("Credits", 100, 20, displayIndex: 2, digitCount: 4);
        var alphaId = session.AddAlphaDisplay("Message", 100, 90, displayIndex: 1, digitCount: 12);
        Assert.Equal(alphaId, session.SelectedId);
        Assert.Equal(alphaId, session.Elements.Last().Id);

        Assert.True(session.UpdateFaceComponent(buttonId, new FaceComponentUpdateOptions { InputId = "coin_in", Name = "Coin" }));
        Assert.True(session.UpdateFaceComponent(sevenId, new FaceComponentUpdateOptions
        {
            OnColorHex = "#FFAA00", OffColorHex = "#101010", ShowDecimalPoint = true, DigitCount = 5
        }));
        Assert.True(session.UpdateFaceComponent(alphaId, new FaceComponentUpdateOptions
        {
            SegmentDisplayType = "alphanumeric", ShowCommaTail = true, IsReversed = true, ShowDecimalPoint = true
        }));
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.False(Assert.IsType<FaceAlphaDisplayElement>(session.Document!.Elements.Single(item => item.ObjectId == alphaId)).IsReversed);
        session.Redo();
        session.Save();

        var reopened = new FaceAuthoringSession(project);
        reopened.OpenFace(path);
        var button = Assert.IsType<FaceButtonElement>(reopened.Document!.Elements.Single(item => item.ObjectId == buttonId));
        var seven = Assert.IsType<FaceSevenSegmentDisplayElement>(reopened.Document.Elements.Single(item => item.ObjectId == sevenId));
        var alpha = Assert.IsType<FaceAlphaDisplayElement>(reopened.Document.Elements.Single(item => item.ObjectId == alphaId));
        Assert.Equal(MachineInputReference.FromInputId("coin_in"), button.LinkedInputReference);
        Assert.Equal("Coin", button.Name);
        Assert.Equal(MachineObjectReference.SevenSegmentDisplay(2), seven.LinkedMachineObjectReference);
        Assert.Equal(5, seven.DigitCount);
        Assert.Equal("#FFAA00", seven.OnColorHex);
        Assert.True(seven.ShowDecimalPoint);
        Assert.Equal(MachineObjectReference.AlphaDisplay(1), alpha.LinkedMachineObjectReference);
        Assert.Equal("alphanumeric", alpha.SegmentDisplayType);
        Assert.True(alpha.ShowCommaTail);
        Assert.True(alpha.IsReversed);
        Assert.Equal(12, alpha.DigitCount);
    }

    [Fact]
    public void ComponentEditEnforcesKindsValidationLocksAndExplicitClears()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Validation");
        var session = new FaceAuthoringSession(project);
        session.CreateFace("ValidationFace");
        var button = session.AddButton("Button", 0, 0);
        var seven = session.AddSevenSegmentDisplay("Display", 10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.AddAlphaDisplay("Invalid", 0, 0, digitCount: 0));
        Assert.Throws<ArgumentException>(() => session.AddButton("Bad ID", 0, 0, inputId: "bad id"));
        Assert.Throws<ArgumentException>(() => session.UpdateFaceComponent(seven,
            new FaceComponentUpdateOptions { LogicalReference = MachineObjectReference.AlphaDisplay(1) }));
        Assert.Throws<ArgumentException>(() => session.UpdateFaceComponent(button,
            new FaceComponentUpdateOptions { OnColorHex = "not-a-colour" }));

        session.UpdateFaceComponent(seven, new FaceComponentUpdateOptions { OnColorHex = "#FFFFFF", Locked = true });
        Assert.Throws<InvalidOperationException>(() => session.UpdateFaceComponent(seven, new FaceComponentUpdateOptions { X = 20 }));
        Assert.Throws<ArgumentException>(() => session.UpdateFaceComponent(seven,
            new FaceComponentUpdateOptions { OnColorHex = "#000000", ClearOnColorHex = true }));
        Assert.True(session.UpdateFaceComponent(seven, new FaceComponentUpdateOptions { ClearOnColorHex = true }));
        Assert.Null(Assert.IsType<FaceSevenSegmentDisplayElement>(session.Document!.Elements.Single(item => item.ObjectId == seven)).OnColorHex);
    }

    [Fact]
    public void GenerationSettingsAreValidatedUndoableAndPersisted()
    {
        var project = new AuthoringSession(_workspace);
        project.CreateProject("Generation");
        var session = new FaceAuthoringSession(project);
        var root = Path.GetDirectoryName(project.ProjectFilePath)!;
        var package = Path.Combine(root, "Assets", "Faces", "GenerationFace");
        Directory.CreateDirectory(package);
        var path = Path.Combine(package, ProjectAssetPathService.FaceManifestFileName);
        File.WriteAllText(path, FaceDocumentStorage.Serialize(new FaceDocumentModel
        {
            Id = "generation-face",
            Title = "GenerationFace",
            BuildState = FaceBuildStateFactory.CreateGeneratedState(artwork: true, mask: true, trays: true,
                runtimeAssetsCurrent: true, runtimeAssetsConfigured: true)
        }));
        session.OpenFace(path);
        var settings = new FaceGenerationSettingsModel
        {
            MaskExtractionThreshold = 77,
            TrayBoundsInflationPercent = 18,
            TrayBoundsPaddingPixels = 4.5,
            ClampTrayBoundsToLampWindow = true,
            PostWarpSharpeningEnabled = false,
            PostWarpSharpeningAmount = .8,
            PostWarpSharpeningRadiusPixels = 1.4,
            PostWarpSharpeningThreshold = 31
        };
        Assert.True(session.UpdateGenerationSettings(settings));
        Assert.False(session.UpdateGenerationSettings(settings));
        var stale = session.Document!.BuildState;
        Assert.Equal(FaceBuildStatus.Current, stale.Get(FaceGeneratedProduct.ArtworkCorrectionInput).Status);
        Assert.Equal(FaceBuildStatus.Stale, stale.Get(FaceGeneratedProduct.BaseArtwork).Status);
        Assert.Equal(FaceBuildStatus.Stale, stale.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, stale.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal(FaceBuildStatus.Stale, stale.Get(FaceGeneratedProduct.Trays).Status);
        Assert.Equal(FaceBuildStatus.Stale, stale.Get(FaceGeneratedProduct.RuntimeAssets).Status);
        session.Undo();
        Assert.Equal(FaceGenerationSettingsModel.Default.MaskExtractionThreshold, session.Document!.GenerationSettings.MaskExtractionThreshold);
        session.Redo();
        session.Save();

        var reopened = new FaceAuthoringSession(project);
        reopened.OpenFace(path);
        Assert.Equal(settings.MaskExtractionThreshold, reopened.Document!.GenerationSettings.MaskExtractionThreshold);
        Assert.Equal(settings.TrayBoundsInflationPercent, reopened.Document.GenerationSettings.TrayBoundsInflationPercent);
        Assert.Equal(settings.TrayBoundsPaddingPixels, reopened.Document.GenerationSettings.TrayBoundsPaddingPixels);
        Assert.Equal(settings.ClampTrayBoundsToLampWindow, reopened.Document.GenerationSettings.ClampTrayBoundsToLampWindow);
        Assert.Equal(settings.PostWarpSharpeningEnabled, reopened.Document.GenerationSettings.PostWarpSharpeningEnabled);
        Assert.Equal(settings.PostWarpSharpeningAmount, reopened.Document.GenerationSettings.PostWarpSharpeningAmount);
        Assert.Equal(settings.PostWarpSharpeningRadiusPixels, reopened.Document.GenerationSettings.PostWarpSharpeningRadiusPixels);
        Assert.Equal(settings.PostWarpSharpeningThreshold, reopened.Document.GenerationSettings.PostWarpSharpeningThreshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => reopened.UpdateGenerationSettings(new FaceGenerationSettingsModel { TrayBoundsPaddingPixels = -1 }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true);
    }
}
