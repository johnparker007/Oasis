using Oasis.Export;
using Oasis.FileOperations;
using System;
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

            ProjectRootPath = NormalizeProjectPath(rootPath);

            SaveProject();

            ProjectsList.AddListItem(new ProjectsList.ListItem()
            {
                Path = ProjectRootPath,
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

            ProjectRootPath = NormalizeProjectPath(path);

            Directory.CreateDirectory(ProjectAssetsPath);

            string projectJsonPath = Path.Combine(ProjectRootPath, kProjectJsonFilename);

            Import.Importer importer = new Import.Importer();
            Editor.Instance.Project = importer.Import(projectJsonPath);

            return true;
        }

        public bool TryAddExistingProject(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return false;
            }

            string normalisedPath;

            try
            {
                normalisedPath = NormalizeProjectPath(projectRootPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to normalise project path '{projectRootPath}': {exception.Message}");
                return false;
            }

            if (!Directory.Exists(normalisedPath))
            {
                Debug.LogWarning($"Project directory does not exist at path '{normalisedPath}'.");
                return false;
            }

            string projectJsonPath = Path.Combine(normalisedPath, kProjectJsonFilename);

            if (!File.Exists(projectJsonPath))
            {
                Debug.LogWarning($"Unable to find '{kProjectJsonFilename}' in '{normalisedPath}'.");
                return false;
            }

            if (IsProjectInList(normalisedPath))
            {
                return true;
            }

            ulong lastModifiedTime = 0;

            try
            {
                lastModifiedTime = (ulong)File.GetLastWriteTimeUtc(projectJsonPath).ToFileTimeUtc();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to read modified time for '{projectJsonPath}': {exception.Message}");
            }

            ProjectsList.AddListItem(new ProjectsList.ListItem()
            {
                Path = normalisedPath,
                LastModifiedTime = lastModifiedTime
            });

            return true;
        }

        private string NormalizeProjectPath(string projectPath)
        {
            string fullPath = Path.GetFullPath(projectPath);
            string rootPath = Path.GetPathRoot(fullPath);

            if (!string.IsNullOrEmpty(fullPath) && !string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return fullPath;
        }

        private bool IsProjectInList(string projectPath)
        {
            foreach (ProjectsList.ListItem listItem in ProjectsList.ListItems)
            {
                string existingPath;

                try
                {
                    existingPath = NormalizeProjectPath(listItem.Path);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to normalise stored project path '{listItem.Path}': {exception.Message}");
                    continue;
                }

                if (string.Equals(existingPath, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
