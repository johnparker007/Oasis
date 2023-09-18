using MFMEExtract;
using Oasis.Layout;
using Oasis.MFME;
using Oasis.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.MAME
{
    public class MameController : MonoBehaviour
    {
        private const string kTEMPHardcodedMameExeDirectoryPath = "Emulators\\MAME\\mame0258";
        private const string kTEMPHardcodedRomName = "j6popoli";

        private const string kMameExeFilename = "mame.exe";

        // Outputs component state changes to console, may be better way of pulling these out of MAME
        private const string kArgsOutputConsole = "-output console";
        private const string kArgsOutputNetwork = "-output network";

        // Enable Lua console (may be better way of doing this with a custom Mame lua plugin)
        private const string kArgsLuaConsole = "-console";

        // Use the Oasis plugin
        private const string kArgsPluginOasis = "-plugin oasis";

        // This '-video none' option means we don't need to actually skip the 'this game is not working' screens
        // and go straight into the emulation.  Also don't need -window or -skip_gameinfo (?) either
        private const string kArgsVideoNone = "-video none -seconds_to_run 999999999";
        private const string kArgsForTestingWithVideo = "-window";

        private const string kArgsSkipGameInfo = "-skip_gameinfo";

        // "-state A";  this works, loads the required state as part of startup, very fast/clean

        // -autosave no good, only works when save states explicitally supported, will need to 
        // 'manually' save state on exit via sending Lua command.  Then can specify '-state 1' etc
        // to load with a saved state


        // Some sample MAME output data until implemented:
        //digit2 = 23387  (JP prob 1 bit per segment, support up to 16 segs including dot?
        //digit3 = 16191
        //digit5 = 14392
        //STATLED = 1
        //reel1 = 94  JP Not sure whether should be using reel1 or sreel1 value, I think one of them is legacy for the old reel system in the MAME internal layouts
        //sreel1 = 64170
        //vfdduty0 = 27   (JP 0-31 I believe)
        //vfd13 = 41164  (JP prob 1 bit per segment, support up to 16 segs including dot?  or is it for char set?

        private const string kDataPrefixLamp = "lamp";
        private const string kDataPrefixDigit = "digit";
        private const string kDataPrefixReel = "reel";
        private const string kDataPrefixVfdDuty0 = "vfdduty0";
        private const string kDataPrefixVfd = "vfd";


        public bool ArgsOutputConsole;
        public bool ArgsOutputNetwork;

        public bool ArgsLuaConsole;
        public bool ArgsPluginOasis;
        public bool ArgsVideoNone;
        public bool ArgsSkipGameInfo;

        public bool ProcessCreateNoWindow;

        [Tooltip("By forcing vsync off, we remove a frame of latency, so lamps etc will light up one frame earlier" +
            ", essentially removing a frame of input lag, by removing a frame of Unity render lag behind the emulator." +
            " In some recorded footage, Unity was actually *ahead* of the MAME internal layout rendering by 1 frame " +
            "with vsync disabled!")]
        public bool ForceVsyncOffWhenRunning;

        public bool DebugOutputStdOut;

        public bool DebugOutputMameCommandLine;

        public int[] LampValues
        {
            get;
            private set;
        } = new int[1024]; // TEMP test, no idea how large this needs to be wrt all techs!

        public int[] ReelValues
        {
            get;
            private set;
        } = new int[16]; // TEMP test, no idea how large this needs to be wrt all techs!

        public UnityEvent OnImportComplete = new UnityEvent();

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
            if(_process != null)
            {
                _process.OutputDataReceived -= OnOutputDataReceived;

                _process.CancelOutputRead();

                _process.Kill();
            }
        }

        public void StartMame()
        {
            string additionalArgs = "";

            // can't be both of these:
            if(ArgsOutputConsole)
            {
                additionalArgs += " " + kArgsOutputConsole;
            }
            else if (ArgsOutputNetwork)
            {
                additionalArgs += " " + kArgsOutputNetwork;
            }

            if (ArgsLuaConsole)
            {
                additionalArgs += " " + kArgsLuaConsole;
            }

            if (ArgsPluginOasis)
            {
                additionalArgs += " " + kArgsPluginOasis;
            }

            if (ArgsSkipGameInfo)
            {
                additionalArgs += " " + kArgsSkipGameInfo;
            }

            if (ArgsVideoNone)
            {
                additionalArgs += " " + kArgsVideoNone;
            }
            else
            {
                additionalArgs += " " + kArgsForTestingWithVideo;
            }

            string arguments = kTEMPHardcodedRomName + additionalArgs;
            _process = StartProcess(MameExeDirectoryFullPath, kMameExeFilename, arguments);

            if(ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 0;
            }
        }

        public void StopMame()
        {
            // TODO - stopping the emulation will prob be done with Lua?


            if (ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 1;
            }
        }

        public void ResetMame()
        {

        }

        public void PauseMame()
        {

        }

        public async void TESTUnpause()
        {
            await _process.StandardInput.WriteLineAsync("resume\r\n");
        } 

        // TOIMPROVE - this class will need breaking up into input/output/commands etc
        public void SetButtonState(int buttonNumber, bool state)
        {
            // TODO just hardcoded lookup of JPM Impact port/tag for testing for the mo, will
            // need to be dynamic from currently selected Platform for these fruit machine raw inputs
            // (video games will prob have an option to send 'standard' inputs, like P1 Joystick Up, P2 Fire 1 etc...)

            string tag = MameInputPortHelper.GetMamePortTagImpact(buttonNumber);
            string mask = MameInputPortHelper.GetMAMEPortInputMaskName(buttonNumber);

            SetPortValue(tag, mask, state);
        }

        private void SetPortValue(string tag, string mask, bool keyDown)
        {
            string inputValue = keyDown ? "1" : "0";

            string pluginCommand =
                "set_input_value"
                + " "
                + tag
                + " "
                + mask
                + " "
                + inputValue
                ;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        private Process StartProcess(string workingDirectory, string filename, string arguments)
        {
            if(DebugOutputMameCommandLine)
            {
                UnityEngine.Debug.LogError(filename + " " + arguments);
            }

            string execPath = Path.Combine(workingDirectory, filename);
            execPath = execPath.Replace("/", "\\");

            ProcessStartInfo startInfo = new ProcessStartInfo(execPath, arguments);
            startInfo.WorkingDirectory = workingDirectory;

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = ProcessCreateNoWindow; 

            Process process = new Process();
            process.StartInfo = startInfo;

            process.OutputDataReceived += OnOutputDataReceived;

            process.Start();

            process.BeginOutputReadLine();

            return process;
        }

        private void ProcessLine(string lineData)
        {
            if(DebugOutputStdOut)
            {
                UnityEngine.Debug.LogError(lineData);
            }

            // TODO very crude for now, will be able to be optimised with dictionaries etc
            if (lineData.Substring(0, kDataPrefixLamp.Length) == kDataPrefixLamp)
            {
                ProcessLineLamp(lineData);
            }
            else if (lineData.Substring(0, kDataPrefixReel.Length) == kDataPrefixReel)
            {
                ProcessLineReel(lineData);
            }
        }

        // TOIMPROVE - can make a generic function since this component number/value text extraction is copy/paste
        private void ProcessLineLamp(string lineData)
        {
            int lampNumberStartIndex = kDataPrefixLamp.Length;
            int lampNumberEndIndex = lineData.IndexOf(' ');
            string lampNumberString = lineData.Substring(lampNumberStartIndex, lampNumberEndIndex - lampNumberStartIndex);
            int lampNumber = int.Parse(lampNumberString);

            int lampValueStartIndex = lineData.LastIndexOf(' ');
            string lampValueString = lineData.Substring(lampValueStartIndex, lineData.Length - lampValueStartIndex);
            int lampValue = int.Parse(lampValueString);

            //UnityEngine.Debug.LogError("lampNumber " + lampNumber + "   lampValue " + lampValue);

            LampValues[lampNumber] = lampValue;
        }

        private void ProcessLineReel(string lineData)
        {
            int reelNumberStartIndex = kDataPrefixReel.Length;
            int reelNumberEndIndex = lineData.IndexOf(' ');
            string reelNumberString = lineData.Substring(reelNumberStartIndex, reelNumberEndIndex - reelNumberStartIndex);
            int reelNumber = int.Parse(reelNumberString);

            int reelValueStartIndex = lineData.LastIndexOf(' ');
            string reelValueString = lineData.Substring(reelValueStartIndex, lineData.Length - reelValueStartIndex);
            int reelValue = int.Parse(reelValueString);

            //UnityEngine.Debug.LogError("reelNumber " + reelNumber + "   reelValue " + reelValue);

            ReelValues[reelNumber] = reelValue;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            //UnityEngine.Debug.LogError(dataReceivedEventArgs.Data);

            ProcessLine(dataReceivedEventArgs.Data);
        }

    }

}

