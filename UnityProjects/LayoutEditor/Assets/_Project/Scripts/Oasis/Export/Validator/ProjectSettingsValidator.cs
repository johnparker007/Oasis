using System.Collections.Generic;
using Oasis.Project;

namespace Oasis.Export
{
    public class ProjectSettingsValidator
    {
        public void Validate(SettingsData projectSettings, Dictionary<string, object> layout)
        {
            if (projectSettings == null) 
            {
                throw new ExporterException("Project settings may not be null");
            }

            if (projectSettings.FruitMachine == null) 
            {
                throw new ExporterException("No Fruit Machine definition provided in project settings");
            }

            // JP the project should be saved if the ROM name is currently empty, as it can be populated later
            //if (projectSettings.Mame.RomName.Trim().Length == 0) {
            //  throw new ExporterException("A ROM name must be provided");
            //}

            if (projectSettings.Mame.RomName == null)
            {
                throw new ExporterException("ROM name cannot be null");
            }
        }
    }
}
