using System.Collections.Generic;
using System.Text;
using System.Xml;
using Oasis.Layout;
using Oasis.Project;

namespace Oasis.Export 
{
    /// <summary>
    /// Class for exporting Oasis projects as MAME lay files.
    /// </summary>
    public class MameExporter : Exporter
    {
        public MameExporter(FileSystemWrapper fileSystemWrapper, ProjectSettingsValidator projectSettingsValidator, LayoutValidator layoutValidator) : base(fileSystemWrapper, projectSettingsValidator, layoutValidator) {}

        /// <summary>
        /// Exports a MAME layout generated from an Oasis Project
        /// </summary>
        /// <param name="projectData"></param>
        /// <param name="exportPath"></param>
        public override void Export(ProjectData projectData, string exportPath)
        {
            base.Validate(projectData.Settings, projectData.Layout.GetRepresentation(), exportPath);
            XmlTextWriter writer = new XmlTextWriter(exportPath, Encoding.UTF8);
            writer.WriteStartDocument();
            writer.WriteStartElement("mamelayout");
            writer.WriteAttributeString("version", "2");

            // Get the representation and process it to populate the document
            foreach(KeyValuePair<string, object> currentEntry in projectData.Layout.GetRepresentation())
            {
                if (currentEntry.Key == "views")
                {
                    processViews((Dictionary<string, object>)currentEntry.Value);
                }
            }
            writer.WriteEndElement();
            writer.Close();
        }

        protected void processViews(Dictionary<string, object> views)
        {

        }
    }
}
