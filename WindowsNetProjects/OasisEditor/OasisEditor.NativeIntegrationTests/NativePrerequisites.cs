using Xunit.Sdk;

namespace OasisEditor.NativeIntegrationTests;

internal static class NativePrerequisites
{
    internal static string RequireFile(string variable)
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("Fabric native integration tests require Windows.");
        var path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path))
            throw SkipException.ForSkip($"{variable} is not configured.");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            throw SkipException.ForSkip($"{variable} does not point to an existing file: {path}");
        return path;
    }

    internal static string RequireDirectory(string variable)
    {
        if (!OperatingSystem.IsWindows())
            throw SkipException.ForSkip("Fabric native integration tests require Windows.");
        var path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path))
            throw SkipException.ForSkip($"{variable} is not configured.");
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path))
            throw SkipException.ForSkip($"{variable} does not point to an existing directory: {path}");
        return path;
    }
}
