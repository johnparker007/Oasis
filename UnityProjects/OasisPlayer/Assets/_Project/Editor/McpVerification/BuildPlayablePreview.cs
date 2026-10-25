using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace OasisPlayer.Editor.Verification
{
    public static class BuildPlayablePreview
    {
        public static void Build()
        {
            var output = Environment.GetEnvironmentVariable("OASIS_PLAYABLE_BUILD_OUTPUT");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)) throw new InvalidOperationException("Explicit absolute OASIS_PLAYABLE_BUILD_OUTPUT required.");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Bootstrap.unity", "Assets/_Project/Scenes/MachinePreview.unity" },
                locationPathName = Path.Combine(output, "OasisPlayer.exe"), target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player build failed: " + report.summary.result);
        }
    }
}
