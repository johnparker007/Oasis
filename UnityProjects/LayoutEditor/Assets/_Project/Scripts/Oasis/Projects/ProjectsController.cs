using Oasis.Export;
using Oasis.FileOperations;
using System.IO;
using UnityEngine;

namespace Oasis.Projects
{
    public class ProjectsController : MonoBehaviour
    {
        public const string kProjectJsonFilename = "project.json";
        public const string kAssetsFolderName = "Assets";
         
        public string ProjectRootPath
        {
            get;
            set;
        }

        public string ProjectAssetsPath
        {
            get { return Path.Combine(ProjectRootPath, kAssetsFolderName); }
        }

        public ProjectsList ProjectsList
        {
            get;
            private set;
        }


        private void Awake()
        {
            ProjectsList = GetComponent<ProjectsList>();
        }

        private void Start()
        {
            
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
            Directory.CreateDirectory(Path.Combine(rootPath, kAssetsFolderName));

            ProjectRootPath = rootPath;

            SaveProject();

            ProjectsList.AddListItem(new ProjectsList.ListItem()
            {
                Path = rootPath,
                LastModifiedTime = 0 // TODO
            });

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

        public bool LoadProject(string path)
        {
            // TODO prob want to add some exception handling and return false for failed save

            ProjectRootPath = path;

            Directory.CreateDirectory(ProjectAssetsPath);

            string projectJsonPath = Path.Combine(ProjectRootPath, kProjectJsonFilename);

            Import.Importer importer = new Import.Importer();
            Editor.Instance.Project = importer.Import(projectJsonPath);

            return true;
        }
    }
}
