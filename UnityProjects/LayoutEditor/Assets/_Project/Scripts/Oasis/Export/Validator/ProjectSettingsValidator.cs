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
          if (projectSettings.FruitMachine == null) {
            throw new ExporterException("No Fruit Machine definition provided in project settings");
          }
          if (projectSettings.Mame.RomName.Trim().Length == 0) {
            throw new ExporterException("A ROM name must be provided");
          }
      }
  }
}
