using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools
{
    public class Extractor
    {
        public struct Options
        {
            public string SourceLayoutPath;
            public bool UseCachedLampImages;
            public bool UseCachedReelImages;
            public bool ScrapeLamps5To8;
            public bool ScrapeLamps9To12;
        }

        public void StartExtraction(Options options)
        {
            OutputLog.Log("Starting Extraction");
            OutputLog.Log("Extraction source layout: " + options.SourceLayoutPath);

            Program.LayoutCopier.CopyToMfmeTools(options.SourceLayoutPath);
            OutputLog.Log("Copied source layout to MFME Tools");

        }
    }
}
