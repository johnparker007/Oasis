using Oasis.MfmeTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Mfme
{
    public class LayoutCopier
    {
        public static readonly string kLayoutsDirectoryName = "Layouts";
        public static readonly string kLayoutGamFilename = "MFME_Layout.gam";
        public static readonly string kLayoutFmlFilename = "MFME_Layout.fml";

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

            string originalSourceFmlFilename = MfmeGamFileHelper.GetFmlFilename(sourceGamFilePath);

            string sourceLayoutDirectoryPath = Path.GetDirectoryName(sourceGamFilePath);
            string sourceFmlPath = Path.Combine(sourceLayoutDirectoryPath, originalSourceFmlFilename);

            string targetFmlFilePath = Path.Combine(LayoutsDirectoryPath, kLayoutFmlFilename);
            File.Copy(sourceFmlPath, targetFmlFilePath);

            PatchGamFile(targetGamFilePath);
        }

        private void PatchGamFile(string gamFilePath)
        {
            // MFME has a bug so it can't load .fml contains '£' etc
            MfmeGamFileHelper.PatchLines(gamFilePath, "Layout", kLayoutFmlFilename, true);

            // blanking the ROMs skips all the startup popups, and stops game running
            MfmeGamFileHelper.PatchLines(gamFilePath, "Sound", "", false);
            MfmeGamFileHelper.PatchLines(gamFilePath, "ROM", "", false);
        }
    }
}
