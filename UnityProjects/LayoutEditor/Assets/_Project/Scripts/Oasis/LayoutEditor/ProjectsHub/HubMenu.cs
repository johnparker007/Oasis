using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class HubMenu : MonoBehaviour
    {
        public Button NewProjectButton;
        public Button AddProjectButton;

        private ProjectsHubController _projectsHubController = null;

        private void Awake()
        {
            _projectsHubController = GetComponentInParent<ProjectsHubController>();

            NewProjectButton.onClick.AddListener(OnNewProjectButtonClick);
            AddProjectButton.onClick.AddListener(OnAddProjectButtonClick);
        }

        private void OnDestroy()
        {
            NewProjectButton.onClick.RemoveListener(OnNewProjectButtonClick);
            AddProjectButton.onClick.RemoveListener(OnAddProjectButtonClick);
        }

        private void OnNewProjectButtonClick()
        {
            _projectsHubController.SetNewProjectMenuActive(true);
        }

        private void OnAddProjectButtonClick()
        {
            Debug.LogError("TODO OnAddProjectButtonClick");

            //Editor.Instance.Preferences.ProjectsFolder
        }
    }
}
