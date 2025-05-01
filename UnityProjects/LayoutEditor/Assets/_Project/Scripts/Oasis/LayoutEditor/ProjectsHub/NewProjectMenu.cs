using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class NewProjectMenu : MonoBehaviour
    {
        public Button CreateProjectButton;
        public Button CancelButton;

        private ProjectsHubController _projectsHubController = null;

        private void Awake()
        {
            _projectsHubController = GetComponentInParent<ProjectsHubController>();

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
            Debug.LogError("TODO create project using options");
        }

        private void OnCancelButtonClick()
        {
            _projectsHubController.ShowHubMenu();
        }
    }
}
