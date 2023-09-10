using System.IO;
using UnityEngine;

namespace Oasis.Utility
{
    public static class DataPathHelper
    {
        public static string DynamicRootPath
        {
            get
            {
                if (Application.isEditor)
                {
                    return EditorRootPath;
                }
                else
                {
                    return BuildRootPath;
                }
            }
        }

        public static string EditorRootPath
        {
            get
            {
                // TOIMPROVE - this will differ for different developer PCs, should pull out to 
                // some kind of config file/scriptable object
                return "C:\\projects\\GitRepos\\Oasis\\UnityProjectDataDirs\\LayoutEditor";
            }
        }

        public static string BuildRootPath
        {
            get
            {
                return Application.dataPath;
            }
        }
    }
}
