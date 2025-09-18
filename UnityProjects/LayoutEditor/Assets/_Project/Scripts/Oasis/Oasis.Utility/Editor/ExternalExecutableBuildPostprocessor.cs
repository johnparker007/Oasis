#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Oasis.Utility.Editor
{
    public class ExternalExecutableBuildPostprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows &&
                report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            if (!ExternalExecutableUtility.HasEditorExecutables())
            {
                UnityEngine.Debug.LogWarning(
                    $"No external executables were found in '{ExternalExecutableUtility.GetEditorExecutableDirectory()}'.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows &&
                report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            string buildOutputPath = report.summary.outputPath;
            string buildDirectory = Path.GetDirectoryName(buildOutputPath);
            if (string.IsNullOrEmpty(buildDirectory))
            {
                return;
            }

            ExternalExecutableUtility.CopyExecutablesForBuild(buildDirectory);
        }
    }
}
#endif
