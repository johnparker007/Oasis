using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class ProjectsHubController : MonoBehaviour
    {
        public HubMenu HubMenu;
        public NewProjectMenu NewProjectMenu;


        private void Awake()
        {
            ShowHubMenu();
        }

        public void ShowHubMenu()
        {
            HubMenu.gameObject.SetActive(true);
            NewProjectMenu.gameObject.SetActive(false);
        }

        public void ShowNewProjectMenu()
        {
            HubMenu.gameObject.SetActive(false);
            NewProjectMenu.gameObject.SetActive(true);
        }
    }
}
