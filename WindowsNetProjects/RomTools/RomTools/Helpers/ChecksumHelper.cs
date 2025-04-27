using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Oasis.RomTools.Helpers
{
    public static class ChecksumHelper
    {
        public static ulong GetTotalSum(byte[] dataBytes)
        {
            ulong totalSum = 0;
            foreach(byte dataByte in dataBytes)
            {
                totalSum += dataByte;
            }

            return totalSum;
        }
    }


}
