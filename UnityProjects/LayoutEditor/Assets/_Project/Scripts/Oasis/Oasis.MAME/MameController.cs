using MFMEExtract;
using Oasis.Layout;
using Oasis.MFME;
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
            None,

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

        private const string kDataPrefixLampLabel = "lamplabel"; // not sure what this is for now, going to discard, seen on sc4dnd
        private const string kDataPrefixTriac = "triac";
        private const string kDataPrefixText = "text";
        private const string kDataPrefixLamp = "lamp";
        private const string kDataPrefixDigit = "digit";
        private const string kDataPrefixSReel = "sreel"; // legacy
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

        public Process Process
        {
            get;
            private set;
        } = null;


        public string MameExeDirectoryFullPath
        {
            get
            {
                int mameVersion = Preferences.kDefaultMameVersion;

                if (Editor.Instance != null && Editor.Instance.Preferences != null)
                {
                    mameVersion = Editor.Instance.Preferences.MameVersion;
                }

                string versionFolder = $"mame{mameVersion.ToString("D4")}";
                return Path.Combine(Application.persistentDataPath, "Downloads", "MAME", versionFolder);
            }
        }

        private void Update()
        {
        }

        private void OnDestroy()
        {
            // TODO this stuff will want to be in the StopMame() flow etc
            if(Process != null)
            {
                Process.OutputDataReceived -= OnOutputDataReceived;

                Process.CancelOutputRead();

                Process.Kill();
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

            string arguments = Editor.Instance.Project.Settings.Mame.RomName + additionalArgs;
//xxx hack
//string arguments = Editor.Instance.Project.Settings.Mame.RomName;

            Process = StartProcess(MameExeDirectoryFullPath, kMameExeFilename, arguments);

            if(ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 0;
            }
        }

        public void ExitMame()
        {
            string pluginCommand = "exit";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);

            if (ForceVsyncOffWhenRunning)
            {
                QualitySettings.vSyncCount = 1;
            }
        }

        public void SoftReset()
        {
            string pluginCommand = "soft_reset";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void HardReset()
        {
            string pluginCommand = "hard_reset";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void Pause()
        {
            string pluginCommand = "pause";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void Resume()
        {
            string pluginCommand = "resume";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        } 

        public void SetThrottled(bool throttled)
        {
            string pluginCommand =
                "throttled"
                + " "
                + throttled;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateLoad()
        {
            string pluginCommand =
                "state_load"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateSave()
        {
            string pluginCommand =
                "state_save"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        public void StateSaveAndExit()
        {
            string pluginCommand =
                "state_save_and_exit"
                + " "
                + kDefaultSaveStateFilename;

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        // XXX TEMP to test
        public void SnapshotPixels()
        {
            string pluginCommand = "snapshot_pixels";

            UnityEngine.Debug.Log("Sending: " + pluginCommand);

            Process.StandardInput.WriteLine(pluginCommand);
        }

        // TOIMPROVE - this class will need breaking up into input/output/commands etc
        public void SetButtonState(int buttonNumber, bool state)
        {
            // video games will prob have an option to send 'standard' inputs, like P1 Joystick Up, P2 Fire 1 etc...
            string tag = MameInputPortHelper.GetMamePortTag(buttonNumber, Editor.Instance.Project.Settings.FruitMachine.Platform);
            string mask = MameInputPortHelper.GetMAMEPortInputMaskName(buttonNumber);

            SetPortValue(tag, mask, state);
        }

        // JP TOIMPROVE: this probably doesn't want to live in here long term
        public static PlatformType GetPlatformFromMfmeSystem(string mfmeSystem)
        {
            switch (mfmeSystem)
            {
                case "MPU3":
                    return PlatformType.MPU3;
                case "MPU4":
                    return PlatformType.MPU4;
                case "IMPACT":
                    return PlatformType.Impact;
                case "M1AB":
                    return PlatformType.M1AB;
                case "MPS2":
                    return PlatformType.MPS2;
                case "SYS1":
                    return PlatformType.AceSys1;
                case "SCORPION2":
                    return PlatformType.Scorpion2;
                //case "DOTMATRIX":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "SYSTEM80":
                    return PlatformType.Sys80;
                case "SYS5":
                    return PlatformType.Sys5;
                case "MPU4VIDEO":
                    return PlatformType.MPU4Video;
                case "SCORPION1":
                    return PlatformType.Scorpion1;
                case "SYS85":
                    return PlatformType.SYS85;
                case "spACE":
                    return PlatformType.AceSPACE;
                case "PROCONN":
                    return PlatformType.Proconn;
                case "SCORPION4":
                    return PlatformType.Scorpion4;
                case "MACH2000E":
                    return PlatformType.Mach2000E;
                case "MPU5":
                    return PlatformType.MPU5;
                case "SRU":
                    return PlatformType.SRU;
                case "EPOCH":
                    return PlatformType.Epoch;
                //case "MACH2000S":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "MACH2000A":
                    return PlatformType.Mach2000A;
                case "MMM":
                    return PlatformType.MMM;
                case "MPU2":
                    return PlatformType.MPU2;
                case "IGTS+":
                    return PlatformType.IGTSPlus;
                case "SCORPION5":
                    return PlatformType.Scorpion5;
                case "ADDER5":
                    return PlatformType.Adder5;
                case "SYS83":
                    return PlatformType.SYS83;
                case "IGTS2000":
                    return PlatformType.IGTS2000;
                //case "IGTVFD":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "ACEVIDEO":
                    return PlatformType.ACEVideo;
                case "MPU4PLASMA":
                    return PlatformType.MPU4Plasma;
                case "ELECTROCOIN":
                    return PlatformType.Electrocoin;
                //case "ECOINSOUND":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "COINMASTER":
                    return PlatformType.Coinmaster;
                case "ASTRASYSA1":
                    return PlatformType.AstraA1;
                case "PLUTO5":
                    return PlatformType.Pluto5;
                case "PHOENIX":
                    return PlatformType.Phoenix;
                case "BLACKBOX":
                    return PlatformType.BLACKBOX;
                case "ELECTRO":
                    return PlatformType.Electro;
                case "M1VIDEO":
                    return PlatformType.M1Video;
                //case "M1REEL":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "INDER":
                    return PlatformType.INDER;
                case "PCLMAXI":
                    return PlatformType.PCLMAXI;
                //case "MAYGAYDOTMATRIX":
                //    Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
                //    Debug.Break();
                //    return PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
                case "PHOENIX2":
                    return PlatformType.Phoenix2;
                //case "Unknown":
                //    return PlatformType.MFMEGamUnknown;

                default:
                    UnityEngine.Debug.LogError("mfmeSystem not set up for: " + mfmeSystem);
                    return PlatformType.Electro;
            }
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

            Process.StandardInput.WriteLine(pluginCommand);
        }

        private Process StartProcess(string workingDirectory, string filename, string arguments)
        {
            if(DebugOutputMameCommandLine)
            {
                UnityEngine.Debug.LogError("Working dir: " + workingDirectory);
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
            //if (lineData.Substring(0, kDataPrefixSReel.Length) == kDataPrefixSReel)
            //{
            //    // ignore/discard
            //}
            //else if (lineData.Substring(0, kDataPrefixTriac.Length) == kDataPrefixTriac)
            //{
            //    // ignore/discard
            //}
            //else if (lineData.Substring(0, kDataPrefixText.Length) == kDataPrefixText)
            //{
            //    // ignore/discard
            //}
            //else if (lineData.Substring(0, kDataPrefixLampLabel.Length) == kDataPrefixLampLabel) // needs to be before "Lamp"!
            //{
            //    // ignore/discard
            //}
            //else 
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

// XXX hack to force cull brightness while working out issue with sc4 vfd compared to mpu4
VfdDuty[0] = 31;
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

