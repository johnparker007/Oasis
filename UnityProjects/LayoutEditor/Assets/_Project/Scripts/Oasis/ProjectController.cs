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
