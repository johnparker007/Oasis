using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Mame
{
    public static class MameRomIdentifier
    {
        public static string IdentifyRom(List<string> romPaths)
        {
            foreach(string romPath in romPaths)
            {
                string mameRomName = IdentifyRom(romPath);
                if(mameRomName != null) // TODO: && mameGameList.Contains(mameRomName)
                {
                    return mameRomName;
                }
            }

            return null;
        }

        private static string IdentifyRom(string romPath)
        {
            string mameRomName = "m4andycp";
            return mameRomName;
        }
    }
}
