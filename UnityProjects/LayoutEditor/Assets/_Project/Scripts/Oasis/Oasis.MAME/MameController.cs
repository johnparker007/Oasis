using MFMEExtract;
using Oasis.Layout;
using Oasis.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.MAME
{
    // TODO this prob doesn't need to be a monobehavioru after all,
    // can be same pattern as the ExtractIMporter, standrad C# class
    public class MameController : MonoBehaviour
    {
        private const string kTEMPHardcodedMameExeDirectoryPath = "Emulators\\MAME\\mame0258";
        private const string kTEMPHardcodedRomName = "j6popoli";

        private const string kMameExeFilename = "mame.exe";

        // This '-video none' option means we don't need to actually skip the 'this game is not working' screens
        // and go straight into the emulation.  Also don't need -window or -skip_gameinfo either
        private const string kAdditionalArgs = "-output console -video none -seconds_to_run 999999999";

        private const string kLampDataPrefix = "lamp";


        // new test approach to deal with callback blocking
        public int[] LampValues
        {
            get;
            private set;
        } = new int[1024]; // TEMP test, no idea how large this needs to be!


        public UnityEvent OnImportComplete = new UnityEvent();
        public UnityEvent<int, int> OnLampChanged = new UnityEvent<int, int>();

        private Process _process = null;


        public string MameExeDirectoryFullPath
        {
            get
            {
                return Path.Combine(DataPathHelper.DynamicRootPath, kTEMPHardcodedMameExeDirectoryPath);
            }
        }

        private void OnDestroy()
        {
            // TODO this stuff will want to be in the StopMame() flow etc

            _process.OutputDataReceived -= OnOutputDataReceived;

            _process.CancelOutputRead();

            _process.Kill();
        }

        public void StartMame()
        {
            string arguments = kTEMPHardcodedRomName + " " + kAdditionalArgs;
            _process = StartProcess(MameExeDirectoryFullPath, kMameExeFilename, arguments);
        }

        public void StopMame()
        {
            // TODO - this will prob be done with Lua?
        }

        public void ResetMame()
        {

        }

        public void PauseMame()
        {

        }

        private Process StartProcess(string workingDirectory, string filename, string arguments)
        {
            string execPath = Path.Combine(workingDirectory, filename);
            execPath = execPath.Replace("/", "\\");

            ProcessStartInfo startInfo = new ProcessStartInfo(execPath, arguments);
            startInfo.WorkingDirectory = workingDirectory;

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true; // <-- this means there's no Lua console window, MAME window still shows though.  Input mapping works, haven't checked readint std output yet
            //startInfo.WindowStyle = ProcessWindowStyle.Hidden; // <-- this Hidden / Minimised has no effect on MAME

            Process process = new Process();
            process.StartInfo = startInfo;

            process.OutputDataReceived += OnOutputDataReceived;

            process.Start();

            process.BeginOutputReadLine();

            return process;
        }

        private void ProcessLine(string lineData)
        {
            // TODO very crude for now, will be able to be optimised with dictionaries etc
            if(lineData.Substring(0, kLampDataPrefix.Length) == kLampDataPrefix)
            {
                ProcessLineLamp(lineData);
            }
        }

        private void ProcessLineLamp(string lineData)
        {
            int lampNumberStartIndex = kLampDataPrefix.Length;
            int lampNumberEndIndex = lineData.IndexOf(' ');
            string lampNumberString = lineData.Substring(lampNumberStartIndex, lampNumberEndIndex - lampNumberStartIndex);
            int lampNumber = int.Parse(lampNumberString);

            int lampValueStartIndex = lineData.LastIndexOf(' ');
            string lampValueString = lineData.Substring(lampValueStartIndex, lineData.Length - lampValueStartIndex);
            int lampValue = int.Parse(lampValueString);

            UnityEngine.Debug.LogError("lampNumber " + lampNumber + "   lampValue " + lampValue);

            //OnLampChanged?.Invoke(lampNumber, lampValue);

            LampValues[lampNumber] = lampValue;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            //UnityEngine.Debug.LogError(dataReceivedEventArgs.Data);

            ProcessLine(dataReceivedEventArgs.Data);
        }


    }

}

