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
            string projectFolderPath = Path.Combine(LocationInputField.text, ProjectNameInputField.text);

            Editor.Instance.ProjectsController.CreateNewProject(projectFolderPath);

            _projectsHubController.SetNewProjectMenuActive(false);
            _projectsHubController.SetHubMenuActive(false);
        }

        private void OnCancelButtonClick()
        {
            _projectsHubController.SetNewProjectMenuActive(false);
        }
    }
}
