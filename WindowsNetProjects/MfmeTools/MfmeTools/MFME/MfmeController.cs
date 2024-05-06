using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Mfme
{
    public class MfmeController
    {
        public static readonly string kMameDllFilename = "MfmeDll.dll";
        public static readonly string kDllInjectorExeFilename = "DllInject.exe";

        public static Process LaunchMFMEExeWithLayout()
        {
            MfmeOasisCustomRegistry.Initialise();

            string commandLineArguments = Path.Combine(LayoutCopier.kLayoutsDirectoryName, LayoutCopier.kLayoutGamFilename);

            return StartProcess(ExeHelper.MFMERootPath, ExeHelper.MFMEExeFilename, commandLineArguments);
        }

        public static Process LaunchMFMEDllInjector(Process mfmeExeProcess)
        {
            string commandLineArguments =
                mfmeExeProcess.Id + " \"" + Path.Combine(ExeHelper.MFMERootPath, kMameDllFilename) + "\"";

            return StartProcess(ExeHelper.MFMERootPath, kDllInjectorExeFilename, commandLineArguments);
        }

        public static Process StartProcess(string workingDirectory, string filename, string arguments)
        {
            string execPath = Path.Combine(workingDirectory, filename);
            execPath = execPath.Replace("/", "\\");

            var startInfo = new ProcessStartInfo(execPath, arguments);
            startInfo.WorkingDirectory = workingDirectory;

            Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();

            return process;
        }

        public static void KillMFMEProcessIfNotExited(Process mfmeProcess)
        {
            if (mfmeProcess != null && !mfmeProcess.HasExited)
            {
                mfmeProcess.Kill();
            }
        }

    }
}
