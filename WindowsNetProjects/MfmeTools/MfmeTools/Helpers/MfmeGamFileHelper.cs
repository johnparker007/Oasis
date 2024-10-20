using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System;


namespace Oasis.MfmeTools.Helpers
{
    public static class MfmeGamFileHelper
    {
        public static readonly string kKeyLayout = "Layout";
        public static readonly string kKeyPlatform = "System";

        private static string _gamFilePath = null;
        private static List<string> _gamFileLines = null;

        public static string GetFmlFilename(string gamFilePath)
        {
            return GetValue(gamFilePath, kKeyLayout);
        }

        public static string GetPlatformName(string gamFilePath)
        {
            return GetValue(gamFilePath, kKeyPlatform);
        }

        public static string GetValue(string gamFilePath, string key)
        {
            // can't use ReadAllLines, as it doesn't read characters above 127, but 
            // MFME writes characters above 127 (e.g: £ character was encoded as $A3):
            string contents = File.ReadAllText(gamFilePath, System.Text.Encoding.GetEncoding(1252));
            string[] lines = contents.Split('\n');

            foreach (string line in lines)
            
        {
                if (line.StartsWith(key))
                {
                    return line.Substring(key.Length + 1); // +1 for the Space char after the key name in the gam file
                }
            }

            OutputLog.LogError("key '" + key + "' not found in gam file: " + gamFilePath);

            return null;
        }

        public static void PatchLines(string gamFilePath, string key, string patchText, bool preserveKey)
        {
            string[] lines = File.ReadAllLines(gamFilePath);

            for (int lineIndex = 0; lineIndex < lines.Length; ++lineIndex)
            {
                if (lines[lineIndex].StartsWith(key))
                {
                    if(preserveKey)
                    {
                        lines[lineIndex] = key + " " + patchText;
                    }
                    else
                    {
                        lines[lineIndex] = patchText;
                    }
                }
            }

            File.WriteAllLines(gamFilePath, lines);

            OutputLog.Log($"Patched .gam file: [{key}]->[{patchText}]");
        }


        //public static void ReadGamFile(string gamFilePath)
        //{
        //    if (_gamFilePath == gamFilePath)
        //    {
        //        return;
        //    }

        //    _gamFileLines = File.ReadAllLines(gamFilePath).ToList();
        //    _gamFilePath = gamFilePath;
        //}

        //public static MachineConfigurationData.PlatformType Platform
        //{
        //    get
        //    {
        //        return GetPlatform(System);
        //    }
        //}

        public static string SystemValue
        {
            get
            {
                return GetValue("System");
            }
        }

        public static string DIP1Value
        {
            get
            {
                return GetValue("DIP 1");
            }
        }

        public static string DIP2Value
        {
            get
            {
                return GetValue("DIP 2");
            }
        }

        public static string ProtocolValue
        {
            get
            {
                return GetValue("Protocol");
            }
        }


        public static string SetPercentValue
        {
            get
            {
                return GetValue("SetPercent");
            }
        }

        public static string StakeValue
        {
            get
            {
                return GetValue("Stake");
            }
        }

        public static string JackpotValue
        {
            get
            {
                return GetValue("Jackpot");
            }
        }

        public static string PercentageValue
        {
            get
            {
                return GetValue("Percentage");
            }
        }

        public static string LinkTypeValue
        {
            get
            {
                return GetValue("LinkType");
            }
        }

        public static string LoadModeValue
        {
            get
            {
                return GetValue("LoadMode");
            }
        }

        public static bool HiddenValue
        {
            get
            {
                return IsKeyPresent("Hidden");
            }
        }

        public static List<string> SoundValue
        {
            get
            {
                return GetValueList("Sound");
            }
        }

        public static List<string> ROM
        {
            get
            {
                List<string> romList = new List<string>();

                romList.AddRange(GetValueList("ROM"));
                if (IsKeyPresent("VIDROM"))
                {
                    romList.AddRange(GetValueList("VIDROM"));
                }

                return romList;
            }
        }

        public static bool IsKeyPresent(string key)
        {
            string keyLine = _gamFileLines.Find(x => x.StartsWith(key));
            if (keyLine == null || keyLine.Length == 0)
            {
                return false;
            }

            return true;
        }

        public static string GetValue(string key)
        {
            if (!IsKeyPresent(key))
            {
                return null;
            }

            string keyLine = _gamFileLines.Find(x => x.StartsWith(key));
            string value = keyLine.Substring(key.Length + 1); // +1 to skip following space character before the value
            return value;
        }

        public static List<string> GetValueList(string key)
        {
            if (!IsKeyPresent(key))
            {
                return null;
            }

            List<string> keyLines = _gamFileLines.FindAll(x => x.StartsWith(key));
            List<string> values = new List<string>();
            foreach (string keyLine in keyLines)
            {
                values.Add(keyLine.Substring(key.Length + 1)); // +1 to skip following space character before the value
            }

            return values;
        }

        //private static MachineConfigurationData.PlatformType GetPlatform(string mfmeGamSystemString)
        //{
        //    switch (mfmeGamSystemString)
        //    {
        //        case "MPU3":
        //            return MachineConfigurationData.PlatformType.MPU3;
        //        case "MPU4":
        //            return MachineConfigurationData.PlatformType.MPU4;
        //        case "IMPACT":
        //            return MachineConfigurationData.PlatformType.Impact;
        //        case "M1AB":
        //            return MachineConfigurationData.PlatformType.M1AB;
        //        case "MPS2":
        //            return MachineConfigurationData.PlatformType.MPS2;
        //        case "SYS1":
        //            return MachineConfigurationData.PlatformType.AceSys1;
        //        case "SCORPION2":
        //            return MachineConfigurationData.PlatformType.Scorpion2;
        //        case "DOTMATRIX":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "SYSTEM80":
        //            return MachineConfigurationData.PlatformType.Sys80;
        //        case "SYS5":
        //            return MachineConfigurationData.PlatformType.Sys5;
        //        case "MPU4VIDEO":
        //            return MachineConfigurationData.PlatformType.MPU4Video;
        //        case "SCORPION1":
        //            return MachineConfigurationData.PlatformType.Scorpion1;
        //        case "SYS85":
        //            return MachineConfigurationData.PlatformType.SYS85;
        //        case "spACE":
        //            return MachineConfigurationData.PlatformType.AceSPACE;
        //        case "PROCONN":
        //            return MachineConfigurationData.PlatformType.Proconn;
        //        case "SCORPION4":
        //            return MachineConfigurationData.PlatformType.Scorpion4;
        //        case "MACH2000E":
        //            return MachineConfigurationData.PlatformType.Mach2000E;
        //        case "MPU5":
        //            return MachineConfigurationData.PlatformType.MPU5;
        //        case "SRU":
        //            return MachineConfigurationData.PlatformType.SRU;
        //        case "EPOCH":
        //            return MachineConfigurationData.PlatformType.Epoch;
        //        case "MACH2000S":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "MACH2000A":
        //            return MachineConfigurationData.PlatformType.Mach2000A;
        //        case "MMM":
        //            return MachineConfigurationData.PlatformType.MMM;
        //        case "MPU2":
        //            return MachineConfigurationData.PlatformType.MPU2;
        //        case "IGTS+":
        //            return MachineConfigurationData.PlatformType.IGTSPlus;
        //        case "SCORPION5":
        //            return MachineConfigurationData.PlatformType.Scorpion5;
        //        case "ADDER5":
        //            return MachineConfigurationData.PlatformType.Adder5;
        //        case "SYS83":
        //            return MachineConfigurationData.PlatformType.SYS83;
        //        case "IGTS2000":
        //            return MachineConfigurationData.PlatformType.IGTS2000;
        //        case "IGTVFD":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "ACEVIDEO":
        //            return MachineConfigurationData.PlatformType.ACEVideo;
        //        case "MPU4PLASMA":
        //            return MachineConfigurationData.PlatformType.MPU4Plasma;
        //        case "ELECTROCOIN":
        //            return MachineConfigurationData.PlatformType.Electrocoin;
        //        case "ECOINSOUND":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "COINMASTER":
        //            return MachineConfigurationData.PlatformType.Coinmaster;
        //        case "ASTRASYSA1":
        //            return MachineConfigurationData.PlatformType.AstraA1;
        //        case "PLUTO5":
        //            return MachineConfigurationData.PlatformType.Pluto5;
        //        case "PHOENIX":
        //            return MachineConfigurationData.PlatformType.Phoenix;
        //        case "BLACKBOX":
        //            return MachineConfigurationData.PlatformType.BLACKBOX;
        //        case "ELECTRO":
        //            return MachineConfigurationData.PlatformType.Electro;
        //        case "M1VIDEO":
        //            return MachineConfigurationData.PlatformType.M1Video;
        //        case "M1REEL":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "INDER":
        //            return MachineConfigurationData.PlatformType.INDER;
        //        case "PCLMAXI":
        //            return MachineConfigurationData.PlatformType.PCLMAXI;
        //        case "MAYGAYDOTMATRIX":
        //            Debug.LogError("No mapping!    static _gamFilePath: " + _gamFilePath);
        //            Debug.Break();
        //            return MachineConfigurationData.PlatformType.MFMEGamNotSetUp; // This is wrong, unsure of some mappings
        //        case "PHOENIX2":
        //            return MachineConfigurationData.PlatformType.Phoenix2;
        //        case "Unknown":
        //            return MachineConfigurationData.PlatformType.MFMEGamUnknown;

        //        default:
        //            Debug.LogError("mfmeGamPlatformString not set up for: " + mfmeGamSystemString
        //                + "    static _gamFilePath: " + _gamFilePath);
        //            return MachineConfigurationData.PlatformType.Electro;
        //    }

        //}


    }

}
