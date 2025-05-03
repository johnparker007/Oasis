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
            SetHubMenuActive(true);
            SetNewProjectMenuActive(false);
        }

        public void SetHubMenuActive(bool active)
        {
            HubMenu.gameObject.SetActive(active);
        }

        public void SetNewProjectMenuActive(bool active)
        {
            NewProjectMenu.gameObject.SetActive(active);
        }

    }
}
