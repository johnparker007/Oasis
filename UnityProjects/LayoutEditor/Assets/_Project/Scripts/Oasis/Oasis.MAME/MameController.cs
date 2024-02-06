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
        // placeholder, may do something different to this for Oasis
        public enum PlatformType
        {
            Scorpion1,
            Scorpion2,
            Scorpion4,
            Scorpion5,
            MPU3,
            MPU4,
            MPU5,
            Impact,
            M1AB,
            // remaining ones from MFME:
            MPU2,
            MPU4Video,
            MPU4Plasma,
            BLACKBOX,
            SYS83,
            SYS85,
            Adder5,
            Proconn,
            AceSys1,
            AceSPACE,
            MMM,
            Epoch,
            SRU,
            Sys80,
            MPS2,
            Sys5,
            Mach2000E,
            Mach2000A,
            IGTSPlus,
            IGTS2000,
            ACEVideo,
            Electrocoin,
            Coinmaster,
            AstraA1,
            Pluto5,
            Phoenix,
            Electro, // probably never need this one
            M1Video,
            INDER,
            PCLMAXI,
            Phoenix2,
            // new ones from MAME:
            MPU1,
        }


        private const string kTEMPHardcodedMameExeDirectoryPath = "Emulators\\MAME\\mame0258";

        private const string kDefaultSaveStateFilename = "oasis_save_state";

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

        private const string kArgsStateLoad = "-state";


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
        private const string kDataPrefixVfdDuty = "vfdduty";
        private const string kDataPrefixVfdBlank = "vfdblank";
        private const string kDataPrefixVfd = "vfd";

        private const string kDataScreenPixelBytesStart = "pixel_data_start";


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

        [Tooltip("Temp for testing")]
        public string DebugMameRomName;

        [Tooltip("Temp for testing")]
        public PlatformType DebugPlatformType;

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

        public int[] VfdValues
        {
            get;
            private set;
        } = new int[16]; // TEMP test, no idea how large this needs to be wrt all techs!

        public int[] VfdDuty
        {
            get;
            private set;
        } = new int[4]; // TEMP test, no idea how large this needs to be wrt all techs!

        public int[] DigitValues
        {
            get;
            private set;
        } = new int[64]; // TEMP test, no idea how large this needs to be wrt all techs!

        public UnityEvent OnImportComplete = new UnityEvent();

        private Process _process = null;
        //private bool _nextStdOutLineIsPixelData = false;


        public string MameExeDirectoryFullPath
        {
            get
            {
                return Path.Combine(DataPathHelper.DynamicRootPath, kTEMPHardcodedMameExeDirectoryPath);
            }
        }

        // XXX TEMP initial hack test:
        private void Update()
        {
           // SnapshotPixels();
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

        public void StartMame(bool loadState)
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

            if(loadState)
            {
                additionalArgs += " " + kArgsStateLoad + " " + kDefaultSaveStateFilename;
            }

            string arguments = DebugMameRomName + additionalArgs;
            _process = StartProcess(MameExeDirectoryFullPath, kMameExeFilename, arguments);

            if(ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 0;
            }
        }

        public void ExitMame()
        {
            string pluginCommand = "exit";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);

            if (ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 1;
            }
        }

        public void SoftReset()
        {
            string pluginCommand = "soft_reset";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void HardReset()
        {
            string pluginCommand = "hard_reset";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void Pause()
        {
            string pluginCommand = "pause";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void Resume()
        {
            string pluginCommand = "resume";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        } 

        public void SetThrottled(bool throttled)
        {
            string pluginCommand =
                "throttled"
                + " "
                + throttled;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateLoad()
        {
            string pluginCommand =
                "state_load"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateSave()
        {
            string pluginCommand =
                "state_save"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateSaveAndExit()
        {
            string pluginCommand =
                "state_save_and_exit"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        // XXX TEMP to test
        public void SnapshotPixels()
        {
            string pluginCommand = "snapshot_pixels";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            _process.StandardInput.WriteLine(pluginCommand);
        }

        // TOIMPROVE - this class will need breaking up into input/output/commands etc
        public void SetButtonState(int buttonNumber, bool state)
        {
            // video games will prob have an option to send 'standard' inputs, like P1 Joystick Up, P2 Fire 1 etc...
            string tag = MameInputPortHelper.GetMamePortTag(buttonNumber, DebugPlatformType);
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

            //if (lineData == kDataScreenPixelBytesStart)
            //{
            //    _nextStdOutLineIsPixelData = true;
            //}
            //else if (_nextStdOutLineIsPixelData)
            //{
            //    ProcessLinePixelData(lineData);
            //    _nextStdOutLineIsPixelData = false;
            //}
            //else
            
            // TODO very crude for now, will be able to be optimised with dictionaries etc
            if (lineData.Substring(0, kDataPrefixLamp.Length) == kDataPrefixLamp)
            {
                ProcessLineLamp(lineData);
            }
            else if (lineData.Substring(0, kDataPrefixReel.Length) == kDataPrefixReel)
            {
                ProcessLineReel(lineData);
            }
            else if (lineData.Substring(0, kDataPrefixVfd.Length) == kDataPrefixVfd)
            {
                ProcessLineVfd(lineData);
            }
            else if (lineData.Substring(0, kDataPrefixDigit.Length) == kDataPrefixDigit)
            {
                ProcessLineDigit(lineData);
            }




        }

        //private void ProcessLinePixelData(string lineData)
        //{
        //    // TOIMPROVE - optimise:  we will get the screen x/y resolution at process start.
        //    // Then, we can create the fixed length byte array at start, and keep repopulating it, allocing
        //    // ~60 times a frame is very bad!  Just to get working initially...
        //    //byte[] bytes = new byte[lineData.Length * sizeof(char)];
        //    //System.Buffer.BlockCopy(lineData.ToCharArray(), 0, bytes, 0, bytes.Length);

        //    int here = 0;

        //}

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

        private void ProcessLineVfd(string lineData)
        {
            // Examples:
            // vfdduty0 = 29 ; So this applies too all the sub elements of vfd0, range is 0-31
            // vfd2 = 66555 ; In MAME these are the individual characters on the vfd, should be 1 bit per segment
            // vfdblank15 = -1 ;  need to parse these
            int vfdValueStartIndex = lineData.LastIndexOf(' ');
            string vfdValueString = lineData.Substring(vfdValueStartIndex, lineData.Length - vfdValueStartIndex);
            int vfdValue = int.Parse(vfdValueString);

            if (lineData.Substring(0, kDataPrefixVfdDuty.Length) == kDataPrefixVfdDuty)
            {
                // TOIMPROVE just hard checking for vfd0 for now, some machines may use vfd1,2 etc
                VfdDuty[0] = vfdValue;

                //UnityEngine.Debug.LogError("JP Vfd duty: " + VfdDuty[0]);
            }
            else if(lineData.Substring(0, kDataPrefixVfdBlank.Length) == kDataPrefixVfdBlank)
            {
                // TODO process these commands to blank Vfd elements (can prob just set associated vfd element number to 0)
            }
            else
            {
                string vfdNumberString = lineData.Substring(kDataPrefixVfd.Length, 2);
                int vfdNumber = int.Parse(vfdNumberString);
                VfdValues[vfdNumber] = vfdValue;

                //UnityEngine.Debug.LogError("JP Vfd" + vfdNumber + " = " + VfdValues[vfdNumber]);
            }
        }

        private void ProcessLineDigit(string lineData)
        {
            int digitValueStartIndex = lineData.LastIndexOf(' ');
            string digitValueString = lineData.Substring(digitValueStartIndex, lineData.Length - digitValueStartIndex);
            int digitValue = int.Parse(digitValueString);

            // sets -1 as some kind of init/blank at startup on Andy Capp.  Otherwise seems to be 1 bit per segment
            if(digitValue == -1)
            {
                //UnityEngine.Debug.LogError("JP Digit reset");
                digitValue = 0;
            }

            string digitNumberString = lineData.Substring(kDataPrefixDigit.Length, 2);
            int digitNumber = int.Parse(digitNumberString);
            DigitValues[digitNumber] = digitValue;

            //UnityEngine.Debug.LogError("JP Digit" + digitNumber + " = " + DigitValues[digitNumber]);
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs dataReceivedEventArgs)
        {
            //UnityEngine.Debug.LogError(dataReceivedEventArgs.Data);

            //UnityEngine.Debug.LogError("Len: " + dataReceivedEventArgs.Data.Length);

            ProcessLine(dataReceivedEventArgs.Data);
        }

    }

}

