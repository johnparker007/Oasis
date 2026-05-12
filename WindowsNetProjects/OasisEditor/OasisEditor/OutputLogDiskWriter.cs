using System.IO;
using System.Text;

namespace OasisEditor;

public sealed class OutputLogDiskWriter
{
    private readonly string _logDirectory;

    public OutputLogDiskWriter(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public string CurrentLogPath => Path.Combine(_logDirectory, "Editor.log");
    public string PreviousLogPath => Path.Combine(_logDirectory, "Editor-prev.log");

    public void Initialize()
    {
        Directory.CreateDirectory(_logDirectory);
        if (File.Exists(PreviousLogPath))
        {
            File.Delete(PreviousLogPath);
        }

        if (File.Exists(CurrentLogPath))
        {
            File.Move(CurrentLogPath, PreviousLogPath);
        }

        File.WriteAllText(CurrentLogPath, string.Empty);
    }

    public void Append(OutputLogEntry entry)
    {
        var line = entry.ToClipboardLine() + Environment.NewLine;
        File.AppendAllText(CurrentLogPath, line, Encoding.UTF8);
    }
}
