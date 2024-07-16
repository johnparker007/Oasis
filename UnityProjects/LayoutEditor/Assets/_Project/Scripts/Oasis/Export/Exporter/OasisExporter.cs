using System.Collections.Generic;
using Newtonsoft.Json;
using Oasis.Project;
using Oasis.Common;

namespace Oasis.Export 
{
    /// <summary>
    /// Class for exporting Oasis projects
    /// </summary>
    public class OasisExporter : Exporter
    {
        public OasisExporter(FileSystemWrapper fileSystemWrapper, ProjectSettingsValidator projectSettingsValidator, LayoutValidator layoutValidator) : base(fileSystemWrapper, projectSettingsValidator, layoutValidator) {}

        /// <summary>
        /// Saves an Oasis Project
        /// </summary>
        /// <param name="projectData"></param>
        /// <param name="exportPath"></param>
        public override void Export(ProjectData projectData, string exportPath)
        {
            SettingsData projectSettings = projectData.Settings;
            Dictionary<string, object> layout = projectData.Layout.GetRepresentation();
            // The call to base.Export(...) will perform validation on project settings and layout.
            base.Validate(projectSettings, layout, exportPath);
            // For now, add a section to the JSON we are exporting to represent the project settings
            // We might change this later
            layout.Add("api_version", Constants.OASIS_PROJECT_FILE_API_VERSION);
            layout.Add("project_settings", new Dictionary<string, object>
            {
                { "ROM_Name", projectSettings.Mame.RomName },
                { "FruitMachine_Platform", projectSettings.FruitMachine.Platform.ToString() }
            });
            string jsonLayout = GetJSONLayout(layout);
            _fileSystemWrapper.WriteAllText(exportPath, jsonLayout);
        }

        private string GetJSONLayout(Dictionary<string, object> layout)
        {
            try
            {
                return JsonConvert.SerializeObject(layout, Formatting.Indented);
            }
            catch (JsonException e)
            {
                throw new ExporterException("Unable to convert Oasis layout data into JSON", e);
            }
        }
    }
}
