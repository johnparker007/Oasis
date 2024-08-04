using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.Helpers
{
    public static class FontSmoothingHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        const uint SPI_GETFONTSMOOTHING = 74;
        const uint SPI_SETFONTSMOOTHING = 75;
        const uint SPI_UPDATEINI = 0x1;
        const UInt32 SPIF_UPDATEINIFILE = 0x1;

        public static Boolean GetFontSmoothing()
        {
            bool iResult;
            int pv = 0;
            /* Call to systemparametersinfo to get the font smoothing value. */
            iResult = SystemParametersInfo(SPI_GETFONTSMOOTHING, 0, ref pv, 0);
            if (pv > 0)
            {
                //pv > 0 means font smoothing is on.
                return true;
            }
            else
            {
                //pv == 0 means font smoothing is off.
                return false;
            }
        }

        // TODO check if by SPIF_UPDATEINIFILE we can do some temporary form that autoreverts on Windows
        // reboot in case of crash in MfmeTools during extraction.
        public static void SetFontSmoothing(bool enabled)
        {
            uint uiParam = (uint)(enabled ? 1 : 0);

            bool iResult;
            int pv = 0;
            /* Call to systemparametersinfo to set the font smoothing value. */
            iResult = SystemParametersInfo(SPI_SETFONTSMOOTHING, uiParam, ref pv, SPIF_UPDATEINIFILE);
        }
    }
}
