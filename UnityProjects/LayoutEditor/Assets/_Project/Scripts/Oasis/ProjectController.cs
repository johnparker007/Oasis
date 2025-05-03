using Oasis.Export;
using Oasis.FileOperations;
using System.IO;
using UnityEngine;

namespace Oasis
{
    public class ProjectController : MonoBehaviour
    {
        public const string kProjectJsonFilename = "project.json";

        public string ProjectRootPath
        {
            get;
            set;
        }

        public bool CreateNewProject(string rootPath)
        {
            // TODO prob want to add some exception handling and return false for failed save

            // create new empty current project and layout and settings
            Editor.Instance.Project = new ProjectData();
            Editor.Instance.Project.Layout = new LayoutObject();
            Editor.Instance.Project.Settings.Mame.RomName = "";

            // create project directory
            Directory.CreateDirectory(rootPath);

            Editor.Instance.ProjectController.ProjectRootPath = rootPath;

            SaveProject();

            return true;
        }

        public bool SaveProject()
        {
            // TODO prob want to add some exception handling and return false for failed save

            string projectJsonPath = Path.Combine(ProjectRootPath, kProjectJsonFilename);

            OasisExporter exporter = new OasisExporter(new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());
            exporter.Export(Editor.Instance.Project, projectJsonPath);

            return true;
        }
    }
}
