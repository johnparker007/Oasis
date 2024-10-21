using System.Collections.Generic;
using Oasis.Project;
using Oasis.FileOperations;

namespace Oasis.Export
{
    /// <summary>
    /// Class for exporting layouts and projects
    /// </summary>
    public abstract class Exporter
    {
        protected FileSystemWrapper _fileSystemWrapper;
        protected ProjectSettingsValidator _projectSettingsValidator;

        protected LayoutValidator _layoutValidator;

        protected Exporter(FileSystemWrapper fileSystemWrapper, ProjectSettingsValidator projectSettingsValidator, LayoutValidator layoutValidator)
        {
            _fileSystemWrapper = fileSystemWrapper;
            _projectSettingsValidator = projectSettingsValidator;
            _layoutValidator = layoutValidator;
        }
        public abstract void Export(ProjectData projectData, string exportPath);
        protected void Validate(SettingsData projectSettings, Dictionary<string, object> layout, string fileName)
        {
            _projectSettingsValidator.Validate(projectSettings, layout);
            _layoutValidator.Validate(layout);
        }
    }
}