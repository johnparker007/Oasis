using System;
using System.Collections.Generic;

namespace OasisPlayer.Startup;

public static class StartupOptionsParser
{
    public static bool TryParse(IReadOnlyList<string> args, out StartupOptions options, out string error)
    {
        options = new StartupOptions(StartupMode.MachinePreview, string.Empty, false, 1280, 800);
        error = string.Empty;
        var mode = StartupMode.MachinePreview; var modeSet = false; string build = string.Empty; bool? fullscreen = null; int width = 1280; int height = 800;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--mode":
                    if (!ReadValue(args, ref i, arg, out var rawMode, out error)) return false;
                    if (rawMode == "machine-preview") { mode = StartupMode.MachinePreview; modeSet = true; }
                    else if (rawMode == "arcade") { error = "Arcade mode is recognised but is not implemented in Phase 1."; return false; }
                    else { error = $"Unknown startup mode '{rawMode}'. Supported mode: machine-preview."; return false; }
                    break;
                case "--build":
                    if (!ReadValue(args, ref i, arg, out build, out error)) return false; break;
                case "--windowed": fullscreen = false; break;
                case "--fullscreen": fullscreen = true; break;
                case "--width": if (!ReadPositiveInt(args, ref i, arg, out width, out error)) return false; break;
                case "--height": if (!ReadPositiveInt(args, ref i, arg, out height, out error)) return false; break;
                default: error = $"Unknown startup argument '{arg}'."; return false;
            }
        }
        if (!modeSet) { error = "Missing required --mode machine-preview argument."; return false; }
        if (mode == StartupMode.MachinePreview && string.IsNullOrWhiteSpace(build)) { error = "Machine preview mode requires --build <directory>."; return false; }
        options = new StartupOptions(mode, build, fullscreen ?? false, width, height); return true;
    }
    private static bool ReadValue(IReadOnlyList<string> args, ref int index, string option, out string value, out string error) { value = string.Empty; error = string.Empty; if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal)) { error = $"{option} requires a value."; return false; } value = args[++index]; return true; }
    private static bool ReadPositiveInt(IReadOnlyList<string> args, ref int index, string option, out int value, out string error) { value = 0; if (!ReadValue(args, ref index, option, out var raw, out error)) return false; if (!int.TryParse(raw, out value) || value <= 0) { error = $"{option} requires a positive integer value."; return false; } return true; }
}
