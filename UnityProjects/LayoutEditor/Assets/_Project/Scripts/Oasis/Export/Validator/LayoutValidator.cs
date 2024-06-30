using System.Collections.Generic;

namespace Oasis.Export
{
    public class LayoutValidator
    {
        public void Validate(Dictionary<string, object> layout)
        {
            if (layout == null) 
            {
                throw new ExporterException("Layout may not be null");
            }
            _ = layout ?? throw new ExporterException("Layout is not in expected format");
        }
    }
}
