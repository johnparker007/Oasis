namespace OasisEditor.NativeIntegrationTests;

internal static class NativePrerequisites
{
    internal static string? GetSkipReason(IEnumerable<string> prerequisites)
    {
        if (!OperatingSystem.IsWindows())
            return "Fabric native integration tests require Windows.";

        foreach (var prerequisite in prerequisites)
        {
            var value = Environment.GetEnvironmentVariable(prerequisite);
            if (string.IsNullOrWhiteSpace(value))
                return $"{prerequisite} is not configured.";
            var path = Path.GetFullPath(value);
            var exists = prerequisite.EndsWith("_DIRECTORY", StringComparison.Ordinal)
                ? Directory.Exists(path)
                : File.Exists(path);
            if (!exists)
                return $"{prerequisite} does not point to an existing path: {path}";
        }
        return null;
    }

    internal static string RequireFile(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"{variable} was not configured despite the native-test prerequisite check.");
        return Path.GetFullPath(path);
    }

    internal static string RequireDirectory(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"{variable} was not configured despite the native-test prerequisite check.");
        return Path.GetFullPath(path);
    }
}
