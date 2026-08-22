using OasisEditor;
using Xunit;

namespace OasisEditor.Tests;

public sealed class FaceBuildServiceTests
{
    [Fact]
    public void ArtworkCorrection_InvalidatesArtworkAndRuntimeOnly()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, true, true, true);
        new FaceBuildService().Invalidate(state, FaceBuildInput.ArtworkCorrection);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.RuntimeLighting).Status);
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.Trays).Status);
    }

    [Fact]
    public void Build_InvokesOnlyStaleConfiguredProducts()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, true, false);
        state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Stale;
        var calls = new List<FaceGeneratedProduct>();
        var result = new FaceBuildService().Build(state, Builders(calls));
        Assert.Equal([FaceGeneratedProduct.ArtworkOutput], result.Built);
        Assert.DoesNotContain(FaceGeneratedProduct.Trays, calls);
    }

    [Fact]
    public void Rebuild_InvokesEveryConfiguredProduct()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, true, true, true);
        var calls = new List<FaceGeneratedProduct>();
        var result = new FaceBuildService().Build(state, Builders(calls), force: true);
        Assert.True(result.Succeeded);
        Assert.Equal(4, calls.Count);
    }

    [Fact]
    public void Failure_RetainsErrorAndSkipsDependentProducts()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(false, true, true, true);
        state.Get(FaceGeneratedProduct.LampMask).Status = FaceBuildStatus.Stale;
        state.Get(FaceGeneratedProduct.Trays).Status = FaceBuildStatus.Stale;
        var builders = Builders([]).ToDictionary(pair => pair.Key, pair => pair.Value);
        builders[FaceGeneratedProduct.LampMask] = () => new(FaceGeneratedProduct.LampMask, false, "mask unavailable");
        var result = new FaceBuildService().Build(state, builders);
        Assert.False(result.Succeeded);
        Assert.Equal(FaceBuildStatus.Error, state.Get(FaceGeneratedProduct.LampMask).Status);
        Assert.Equal("mask unavailable", state.Get(FaceGeneratedProduct.LampMask).ErrorMessage);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.Trays).Status);
    }

    [Fact]
    public void RelevantChange_RecoversErrorToStale_ThenBuildToCurrent()
    {
        var state = FaceBuildStateFactory.CreateGeneratedState(true, false, false, false);
        state.Get(FaceGeneratedProduct.ArtworkOutput).Status = FaceBuildStatus.Error;
        state.Get(FaceGeneratedProduct.ArtworkOutput).ErrorMessage = "old failure";
        var service = new FaceBuildService();
        service.Invalidate(state, FaceBuildInput.ArtworkCorrection);
        Assert.Equal(FaceBuildStatus.Stale, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
        Assert.Null(state.Get(FaceGeneratedProduct.ArtworkOutput).ErrorMessage);
        service.Build(state, Builders([]));
        Assert.Equal(FaceBuildStatus.Current, state.Get(FaceGeneratedProduct.ArtworkOutput).Status);
    }

    [Fact]
    public void NotConfigured_IsSkippedWithoutFailure()
    {
        var result = new FaceBuildService().Build(new FaceBuildStateModel(), new Dictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>>());
        Assert.True(result.Succeeded);
        Assert.Empty(result.Built);
    }

    private static IReadOnlyDictionary<FaceGeneratedProduct, Func<FaceBuildNodeResult>> Builders(IList<FaceGeneratedProduct> calls) =>
        Enum.GetValues<FaceGeneratedProduct>().ToDictionary(product => product, product => (Func<FaceBuildNodeResult>)(() =>
        {
            calls.Add(product);
            return new FaceBuildNodeResult(product, true);
        }));
}
