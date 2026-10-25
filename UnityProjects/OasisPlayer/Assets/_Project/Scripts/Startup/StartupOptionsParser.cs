using System;
using System.Collections.Generic;

namespace OasisPlayer.Startup
{
    public static class StartupOptionsParser
    {
        public static bool TryParse(IReadOnlyList<string> args, out StartupOptions options, out string error)
        {
            options = new StartupOptions(StartupMode.MachinePreview, string.Empty, false, 1280, 800);
            error = string.Empty;
            var mode = StartupMode.MachinePreview;
            var modeSet = false;
            var build = string.Empty;
            bool? fullscreen = null;
            var width = 1280;
            var height = 800;
            var interaction = "";
            var arcade = "";
            var fabric = ""; var amber = "";

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    // Unity also receives these process arguments. Accept only the engine
                    // switches used by the background standalone smoke check.
                    case "-batchmode":
                    case "-nographics":
                        break;
                    case "-logFile":
                        if (!ReadValue(args, ref i, arg, out _, out error)) return false;
                        break;
                    case "--mode":
                        if (!ReadValue(args, ref i, arg, out var rawMode, out error)) return false;
                        if (rawMode == "machine-preview")
                        {
                            mode = StartupMode.MachinePreview;
                            modeSet = true;
                        }
                        else if (rawMode == "arcade")
                        {
                            mode = StartupMode.Arcade;
                            modeSet = true;
                        }
                        else
                        {
                            error = $"Unknown startup mode '{rawMode}'. Supported modes: machine-preview, arcade.";
                            return false;
                        }
                        break;
                    case "--fabric-runtime":
                        if (!ReadValue(args, ref i, arg, out fabric, out error)) return false;
                        break;
                    case "--amber-impact":
                        if (!ReadValue(args, ref i, arg, out amber, out error)) return false;
                        break;
                    case "--arcade":
                        if (!ReadValue(args, ref i, arg, out arcade, out error)) return false;
                        break;
                    case "--build":
                        if (!ReadValue(args, ref i, arg, out build, out error)) return false;
                        break;
                    case "--interaction":
                        if (!ReadValue(args, ref i, arg, out interaction, out error)) return false;
                        if (interaction != "buttons" && interaction != "pool")
                        { error = "--interaction must be buttons or pool."; return false; }
                        break;
                    case "--windowed":
                        fullscreen = false;
                        break;
                    case "--fullscreen":
                        fullscreen = true;
                        break;
                    case "--width":
                        if (!ReadPositiveInt(args, ref i, arg, out width, out error)) return false;
                        break;
                    case "--height":
                        if (!ReadPositiveInt(args, ref i, arg, out height, out error)) return false;
                        break;
                    default:
                        error = $"Unknown startup argument '{arg}'.";
                        return false;
                }
            }

            if (!modeSet)
            {
                error = "Missing required --mode machine-preview|arcade argument.";
                return false;
            }

            if (mode == StartupMode.MachinePreview && string.IsNullOrWhiteSpace(build))
            {
                error = "Machine preview mode requires --build <directory>.";
                return false;
            }

            if (mode == StartupMode.Arcade && (string.IsNullOrWhiteSpace(arcade) || !string.IsNullOrEmpty(build) || !string.IsNullOrEmpty(interaction)))
            { error = "Arcade mode requires --arcade <manifest> and uses each Machine's authored profile; do not pass --build or --interaction."; return false; }
            if (mode == StartupMode.MachinePreview && !string.IsNullOrEmpty(arcade))
            { error = "--arcade requires --mode arcade."; return false; }
            options = new StartupOptions(mode, build, fullscreen ?? false, width, height, interaction, arcade, fabric, amber);
            return true;
        }

        private static bool ReadValue(IReadOnlyList<string> args, ref int index, string option, out string value, out string error)
        {
            value = string.Empty;
            error = string.Empty;
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"{option} requires a value.";
                return false;
            }

            value = args[++index];
            return true;
        }

        private static bool ReadPositiveInt(IReadOnlyList<string> args, ref int index, string option, out int value, out string error)
        {
            value = 0;
            if (!ReadValue(args, ref index, option, out var raw, out error)) return false;
            if (!int.TryParse(raw, out value) || value <= 0)
            {
                error = $"{option} requires a positive integer value.";
                return false;
            }

            return true;
        }
    }
}
