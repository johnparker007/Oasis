using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oasis.Utility
{
    public static class TempPathHelper 
    {
        private const string kTempPathOasisRootDirectoryName = "Oasis";

        public static DirectoryInfo GetRootDirectoryInfo()
        {
            string tempPath = Path.GetTempPath();
            string tempOasisPath = Path.Combine(tempPath, kTempPathOasisRootDirectoryName);

            DirectoryInfo directoryInfo = new DirectoryInfo(tempOasisPath);
            if (!directoryInfo.Exists)
            {
                try
                {
                    directoryInfo.Create();
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Failed to create directory: {0}", exception.ToString());
                }
            }

            return directoryInfo;
        }
    }
}
