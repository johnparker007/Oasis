using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Oasis.MfmeTools.Shared.Mfme
{
    public static class ExeHelper
    {
        private static SHA256 _sha256 = SHA256.Create();

        public static string MFMEExePath
        {
            get
            {
                return Path.Combine(MFMERootPath, MFMEExeFilename);
            }
        }

        public static string MFMERootPath
        {
            get
            {
                return Path.Combine(PathHelpers.MfmeToolsDirectory, "MFME");
            }
        }

        public static string MFMEExeFilename
        {
            get
            {
                return "MFME.exe";
            }
        }

        public static bool IsLatestMFMEExePresent(byte[] validHash)
        {
            return File.Exists(MFMEExePath)
                && IsLatestExe(MFMEExePath, validHash);
        }

        public static bool IsLatestExe(string filePath, byte[] validHash)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            byte[] hash = GetHashSha256(filePath);

            return hash.SequenceEqual(validHash);
        }

        public static byte[] GetHashSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                return _sha256.ComputeHash(stream);
            }
        }
    }
}
