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
                return System.IO.Path.GetDirectoryName(Application.dataPath);
            }
        }

        public static string MAMERootPath
        {
            get
            {
                return Path.Combine(DataPathHelper.ProjectRootPath, "Emulators\\MAME\\mame0258");
            }
        }

        public static string MAMEROMSPath
        {
            get
            {
                return Path.Combine(DataPathHelper.MAMERootPath, "roms");
            }
        }

        public static string MAMESourcePath
        {
            get
            {
                return Path.Combine(DataPathHelper.ProjectRootPath, "MameSource\\barcrest");
            }
        }
    }
}
