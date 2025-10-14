using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Shared.Extract
{
    [Serializable]
    public class ExtractGamFile
    {
        public Dictionary<string, List<string>> KeyValuePairs = new Dictionary<string, List<string>>();
    }
}
