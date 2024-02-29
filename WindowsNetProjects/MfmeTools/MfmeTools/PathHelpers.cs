using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools
{
    public static class PathHelpers
    {
        public static string MfmeToolsDirectory
        {
            get
            {
                string exeFullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dirFullName = Path.GetDirectoryName(exeFullPath);
                return dirFullName;
            }
        }



    }
}
