using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.Mfme
{
    public static class MfmeOasisCustomRegistry
    {
        public static readonly string kSoftwareKey = "SOFTWARE";
        public static readonly string kCJWKey = "CJW";
        public static readonly string kMfmeOasisKey = "MFME (Oasis - MFME Tools)";

        public static void Initialise()
        {
            CreateRootMFMEOasisKeyAndValues();
        }

        private static void CreateRootMFMEOasisKeyAndValues()
        {
            // TOIMPROVE - check - SOFTWARE may not need to also be opened writable
            RegistryKey cjwRootKey = Registry.CurrentUser.OpenSubKey(kSoftwareKey, true).OpenSubKey(kCJWKey, true);

            if (cjwRootKey == null)
            {
                OutputLog.LogError("Can't create Oasis MFME registry - couldn't find MFME enclosing key (" + kSoftwareKey + "/" + kCJWKey + ")");
                return;
            }

            RegistryKey mfmeOasisKey = cjwRootKey.CreateSubKey(kMfmeOasisKey);

            mfmeOasisKey.SetValue("AboutBoxShown", "1");
            mfmeOasisKey.SetValue("AdditionalFolders", "");
            mfmeOasisKey.SetValue("AddToGameDB", "0");
            mfmeOasisKey.SetValue("BmpToJpg", "1");
            mfmeOasisKey.SetValue("ButtonEffects", "1");
            mfmeOasisKey.SetValue("ClickProperties", "0");
            mfmeOasisKey.SetValue("CoinNoteEffects", "1");
            mfmeOasisKey.SetValue("CurrentVersion", "2002");
            mfmeOasisKey.SetValue("DefaultAudio", "0");
            mfmeOasisKey.SetValue("DefaultMonitor", "0");
            mfmeOasisKey.SetValue("DisableScreenSaver", "1");
            mfmeOasisKey.SetValue("DisplayBrightness", "7");
            mfmeOasisKey.SetValue("DisplayClock", "1");
            mfmeOasisKey.SetValue("DragMove", "1");
            mfmeOasisKey.SetValue("DragOffscreen", "0");
            mfmeOasisKey.SetValue("DragReels", "0");
            mfmeOasisKey.SetValue("EffectsVolume", "127");
            mfmeOasisKey.SetValue("EscQuitsProgram", "0"); // potentially may want to look into this, though current system does seem to be working fine
            mfmeOasisKey.SetValue("HideCursor", "0");
            mfmeOasisKey.SetValue("IgnoreLayoutBackgrounds", "0");
            mfmeOasisKey.SetValue("IgnoreLayoutSizePosition", "0"); // may want to look into this
            mfmeOasisKey.SetValue("IgnoreLocalEffects", "0");
            mfmeOasisKey.SetValue("LayoutSeed", "0");
            mfmeOasisKey.SetValue("LimitDrag", "0");
            mfmeOasisKey.SetValue("LoadCropped", "0");
            mfmeOasisKey.SetValue("LongTermMeters", "1");
            mfmeOasisKey.SetValue("MagnifierEnabled", "0");
            mfmeOasisKey.SetValue("MagnifierScale2", "10");
            mfmeOasisKey.SetValue("ManagerColumn0", "0");
            mfmeOasisKey.SetValue("ManagerColumn1", "1");
            mfmeOasisKey.SetValue("ManagerColumn10", "10");
            mfmeOasisKey.SetValue("ManagerColumn11", "11");
            mfmeOasisKey.SetValue("ManagerColumn12", "12");
            mfmeOasisKey.SetValue("ManagerColumn13", "13");
            mfmeOasisKey.SetValue("ManagerColumn14", "14");
            mfmeOasisKey.SetValue("ManagerColumn2", "2");
            mfmeOasisKey.SetValue("ManagerColumn3", "3");
            mfmeOasisKey.SetValue("ManagerColumn4", "4");
            mfmeOasisKey.SetValue("ManagerColumn5", "5");
            mfmeOasisKey.SetValue("ManagerColumn6", "6");
            mfmeOasisKey.SetValue("ManagerColumn7", "7");
            mfmeOasisKey.SetValue("ManagerColumn8", "8");
            mfmeOasisKey.SetValue("ManagerColumn9", "9");

            mfmeOasisKey.SetValue("ManagerHeight", "600");
            mfmeOasisKey.SetValue("ManagerLeft", "78");
            mfmeOasisKey.SetValue("ManagerS1", "310");
            mfmeOasisKey.SetValue("ManagerS2", "118");
            mfmeOasisKey.SetValue("ManagerSortedColumnV19", "0");
            mfmeOasisKey.SetValue("ManagerSortedDirection", "1");
            mfmeOasisKey.SetValue("ManagerTop", "78");
            mfmeOasisKey.SetValue("ManagerWidth", "800");
            mfmeOasisKey.SetValue("MeterPanelOff", "0");
            mfmeOasisKey.SetValue("MeterTriacEffects", "1");
            mfmeOasisKey.SetValue("Muted", "0");
            mfmeOasisKey.SetValue("PathName", "");
            mfmeOasisKey.SetValue("RandomBackDrops", "0");
            mfmeOasisKey.SetValue("RandomTiles", "0");
            mfmeOasisKey.SetValue("ReelBounce", "0");
            mfmeOasisKey.SetValue("ReelEffects", "1");
            mfmeOasisKey.SetValue("ScreenMode", "0");
            mfmeOasisKey.SetValue("ShowGrid", "0");
            mfmeOasisKey.SetValue("SlideShowTimeout", "1");
            mfmeOasisKey.SetValue("SnapShotReminders", "0");
            mfmeOasisKey.SetValue("SnapToGrid", "0");
            mfmeOasisKey.SetValue("StartInManager", "0");
            mfmeOasisKey.SetValue("TrackBallResolution", "1");
            mfmeOasisKey.SetValue("UnpackBlendedLamps", "0");
            mfmeOasisKey.SetValue("UseFileNames", "0");
            mfmeOasisKey.SetValue("UseWholeDesktop", "0");
            mfmeOasisKey.SetValue("VTP", "0");
            mfmeOasisKey.SetValue("XGrid", "5");
            mfmeOasisKey.SetValue("YGrid", "5");

            mfmeOasisKey.Close();

            OutputLog.Log("Oasis MFME registry initialised.");
        }

    }

}
