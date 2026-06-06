using System.Reflection;
using Xunit;

namespace OasisEditor.Tests;

public sealed class MainWindowViewModelMameStdoutTests
{
    [Fact]
    public void TryClassifyMameStdoutSegment_DebugDisabled_SuppressesPluginDiagnostics()
    {
        var logged = TryClassify("@Oasis plugin: ### command line received: set_input_value J9_2 1 1", debugOutputStdOut: false, out _);

        Assert.False(logged);
    }

    [Fact]
    public void TryClassifyMameStdoutSegment_DebugDisabled_StillLogsErrorsAsWarnings()
    {
        var logged = TryClassify("@ERROR failed to process command", debugOutputStdOut: false, out var status);

        Assert.True(logged);
        Assert.Equal(OutputLogStatus.Warning, status);
    }

    [Fact]
    public void TryClassifyMameStdoutSegment_DebugEnabled_LogsPluginDiagnosticsAsInfo()
    {
        var logged = TryClassify("@Oasis plugin: ### command line received: set_input_value J9_2 1 1", debugOutputStdOut: true, out var status);

        Assert.True(logged);
        Assert.Equal(OutputLogStatus.Info, status);
    }

    private static bool TryClassify(string segment, bool debugOutputStdOut, out OutputLogStatus status)
    {
        var method = typeof(MainWindowViewModel).GetMethod("TryClassifyMameStdoutSegment", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] parameters = [segment, debugOutputStdOut, null];
        var result = (bool)method!.Invoke(null, parameters)!;
        status = Assert.IsType<OutputLogStatus>(parameters[2]);
        return result;
    }
}
