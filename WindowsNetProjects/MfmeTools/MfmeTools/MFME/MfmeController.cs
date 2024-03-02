using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.Mfme
{
    public class MfmeController
    {
        public Process Process
        {
            get;
            private set;
        }

        public static Process StartEmulator(string workingDirectory, string filename, string arguments)
        {
            //Process process = new Process();

            //process.StartInfo.WorkingDirectory = Application.dataPath + "\\" + workingDirectory;
            //process.StartInfo.WorkingDirectory = process.StartInfo.WorkingDirectory.Replace("/", "\\");

            //process.StartInfo.FileName = filename;
            //process.StartInfo.Arguments = arguments;

            //process.StartInfo.RedirectStandardInput = true;
            //process.StartInfo.UseShellExecute = false;


            string execPath = Path.Combine(workingDirectory, filename);
            execPath = execPath.Replace("/", "\\");

            var startInfo = new ProcessStartInfo(execPath, arguments);
            startInfo.WorkingDirectory = workingDirectory;

            //if (UserSettingsController.Instance == null
            //     || UserSettingsController.Instance.TestEmulator == EmulatorConfigurationData.EmulatorTypes.MFME)
            if (true) // XXX Hacked back to always be MFME for now
            {
                // reserved for any future MFME specific settings
            }
            else
            {
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true; // <-- this means there's no Lua console window, MAME window still shows though.  Input mapping works, haven't checked readint std output yet
                                                 //startInfo.WindowStyle = ProcessWindowStyle.Hidden; // <-- this Hidden / Minimised has no effect on MAME
            }

            Process process = new Process();
            process.StartInfo = startInfo;

            process.Start();

            return process;
        }
    }
}
