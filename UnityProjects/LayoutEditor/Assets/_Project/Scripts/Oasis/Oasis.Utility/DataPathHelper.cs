using System.IO;
using UnityEngine;

namespace Oasis.Utility
{
    public static class DataPathHelper
    {
        public static string ProjectRootPath
        {
            get
            {
                return Path.GetDirectoryName(Application.dataPath);
            }
        }

        public static string MAMERootPath
        {
            get
            {
                // This is temporary, until the proper system is in that manages MAME binaries, savestates, dynamic run folder, etc
                return Path.Combine(ProjectRootPath, "Emulators\\MAME\\mame0267");
            }
        }

        public static string MAMEROMSPath
        {
            get
            {
                return Path.Combine(MAMERootPath, "roms");
            }
        }

        public static string MAMESourcePath
        {
            get
            {
                return Path.Combine(ProjectRootPath, "MameSource\\barcrest");
            }
        }
    }
}
