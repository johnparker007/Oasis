using System.IO;

namespace OasisEditor;

public static class MameRuntimePaths
{
    public static string EnsureManagedRuntimeRootDirectory()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OasisEditor",
            "MAME");
        Directory.CreateDirectory(root);
        return root;
    }

    public static string ResolveBundledLuaPluginSourcePath()
    {
        var appBaseDirectory = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(appBaseDirectory, "Assets", "MAME", "plugins", "oasis"));
        return Directory.Exists(candidate) ? candidate : string.Empty;
    }
}
