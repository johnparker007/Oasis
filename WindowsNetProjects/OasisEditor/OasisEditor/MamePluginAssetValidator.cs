using System.IO;

namespace OasisEditor;

public sealed class MamePluginAssetValidator
{
    private static readonly string[] RequiredRelativePaths =
    [
        "plugin.json",
        "init.lua",
        "system/utility.lua",
        "system/stdin_thread.lua",
        "system/command_processor.lua",
        "system/commands/exit.lua",
        "system/commands/pause.lua",
        "system/commands/resume.lua",
        "system/commands/soft_reset.lua",
        "system/commands/hard_reset.lua",
        "system/commands/throttled.lua",
        "system/commands/state_load.lua",
        "system/commands/state_save.lua",
        "system/commands/state_save_and_exit.lua",
        "system/commands/set_input_value.lua"
    ];

    public IReadOnlyList<string> GetMissingFiles(string pluginRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginRootDirectory))
        {
            return ["<plugin root directory is empty>"];
        }

        var missing = new List<string>();
        foreach (var relativePath in RequiredRelativePaths)
        {
            var absolutePath = Path.Combine(pluginRootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                missing.Add(relativePath);
            }
        }

        return missing;
    }
}
