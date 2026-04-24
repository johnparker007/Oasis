using System.Globalization;
using System.Text;

namespace OasisEditor;

internal static class CrashDiagnostics
{
    private static readonly object SyncRoot = new();

    public static string LogPath
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OasisEditor",
                "Logs");
            Directory.CreateDirectory(root);
            return Path.Combine(root, $"crash-{DateTime.UtcNow:yyyyMMdd}.log");
        }
    }

    public static void Log(string source, Exception exception, bool isTerminating)
    {
        var builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"Timestamp (UTC): {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Source: {source}");
        builder.AppendLine($"IsTerminating: {isTerminating}");
        builder.AppendLine($"Thread: {Environment.CurrentManagedThreadId}");
        builder.AppendLine($"Runtime: {Environment.Version}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine("Exception:");
        builder.AppendLine(exception.ToString());

        lock (SyncRoot)
        {
            File.AppendAllText(LogPath, builder.ToString());
        }
    }
}
