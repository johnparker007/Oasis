using Oasis.Export;
using Oasis.FileOperations;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class NewProjectMenu : MonoBehaviour
    {
        public TMPro.TMP_InputField ProjectNameInputField;
        public TMPro.TMP_InputField LocationInputField;
        public Button CreateProjectButton;
        public Button CancelButton;

        private ProjectsHubController _projectsHubController = null;

        private void Awake()
        {
            _projectsHubController = GetComponentInParent<ProjectsHubController>();

            ProjectNameInputField.text = "New Project";
            LocationInputField.text = Editor.Instance.Preferences.ProjectsFolder;

            CreateProjectButton.onClick.AddListener(OnCreateProjectButtonClick);
            CancelButton.onClick.AddListener(OnCancelButtonClick);
        }

        private void OnDestroy()
        {
            CreateProjectButton.onClick.RemoveListener(OnCreateProjectButtonClick);
            CancelButton.onClick.RemoveListener(OnCancelButtonClick);
        }

        private void OnCreateProjectButtonClick()
        {
            // create new empty current project and layout and settings
            Editor.Instance.Project = new ProjectData();
            Editor.Instance.Project.Layout = new LayoutObject();
            Editor.Instance.Project.Settings.Mame.RomName = "";


            // save the project
            string projectFolderPath = Path.Combine(
                LocationInputField.text, ProjectNameInputField.text);

            CreateProjectFolder(projectFolderPath);

            OasisExporter exporter = new OasisExporter(
                new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());

            exporter.Export(
                Editor.Instance.Project, 
                string.Format("{0}\\project.json", projectFolderPath));
        }

        private void OnCancelButtonClick()
        {
            _projectsHubController.ShowHubMenu();
        }

        private void CreateProjectFolder(string projectFolderPath)
        {
            Directory.CreateDirectory(projectFolderPath);
        }
    }
}
