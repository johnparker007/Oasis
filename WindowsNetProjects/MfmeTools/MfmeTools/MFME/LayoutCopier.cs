using Oasis.MfmeTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Shared.Mfme
{
    public class LayoutCopier
    {
        public static readonly string kLayoutsDirectoryName = "Layouts";
        public static readonly string kLayoutGamFilename = "MFME_Layout.gam";
        //public static readonly string kLayoutFmlFilename = "MFME_Layout.fml";

        public static string LayoutsDirectoryPath
        {
            get
            {
                return Path.Combine(ExeHelper.MFMERootPath, kLayoutsDirectoryName);
            }
        }

        public void CopyToMfmeTools(string sourceGamFilePath)
        {
            FileHelper.ForceDeleteDirectory(LayoutsDirectoryPath, true);

            string targetGamFilePath = Path.Combine(LayoutsDirectoryPath, kLayoutGamFilename);
            File.Copy(sourceGamFilePath, targetGamFilePath);

            string sourceFmlFilename = MfmeGamFileHelper.GetFmlFilename(sourceGamFilePath);
            OutputLog.Log("Linked .fml source filename: " + sourceFmlFilename);

            string sourceLayoutDirectoryPath = Path.GetDirectoryName(sourceGamFilePath);
            string sourceFmlPath = Path.Combine(sourceLayoutDirectoryPath, sourceFmlFilename);

            string targetFmlFilePath = Path.Combine(LayoutsDirectoryPath, sourceFmlFilename);
            File.Copy(sourceFmlPath, targetFmlFilePath);

            PatchGamFile(targetGamFilePath);
        }

        private void PatchGamFile(string gamFilePath)
        {
            MfmeGamFileHelper.PatchLines(gamFilePath, "Sound", "");
            MfmeGamFileHelper.PatchLines(gamFilePath, "ROM", "");
        }
    }
}
