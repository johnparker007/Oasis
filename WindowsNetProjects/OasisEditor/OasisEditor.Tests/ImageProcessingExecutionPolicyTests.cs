using Xunit;

namespace OasisEditor.Tests;

public sealed class ImageProcessingExecutionPolicyTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(2, 1)] [InlineData(3, 2)] [InlineData(4, 3)]
    [InlineData(5, 3)] [InlineData(8, 6)] [InlineData(16, 14)]
    public void Auto_ReservesCapacity(int available, int expected) =>
        Assert.Equal(expected, Resolve(CpuImageProcessingMode.Auto, 99, available));

    [Fact] public void Maximum_UsesAllAvailable() => Assert.Equal(8, Resolve(CpuImageProcessingMode.Maximum, 1, 8));
    [Theory]
    [InlineData(-5, 1)] [InlineData(1, 1)] [InlineData(4, 4)] [InlineData(99, 8)]
    public void Custom_ClampsToAvailableRange(int requested, int expected) =>
        Assert.Equal(expected, Resolve(CpuImageProcessingMode.Custom, requested, 8));

    private static int Resolve(CpuImageProcessingMode mode, int custom, int available) =>
        ImageProcessingExecutionPolicy.Resolve(new ProcessingPreferences { CpuMode = mode, CustomMaximumWorkers = custom }, available).MaxDegreeOfParallelism;
}
