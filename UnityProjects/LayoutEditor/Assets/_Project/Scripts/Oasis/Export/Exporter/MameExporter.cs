using System.Collections.Generic;
using System.Xml;
using Oasis.Project;
using Oasis.FileOperations;

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
            //TODO: Implement the export of LAY files.
        }   
    }
}
