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
            Console.WriteLine("Start Extraction()");
            Console.WriteLine("Extraction source layout: " + options.SourceLayoutPath);

        }
    }
}
