using Oasis.Projects;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class ProjectsListRow : MonoBehaviour
    {
        public TMPro.TMP_Text NameText;
        public TMPro.TMP_Text PathText;
        public Button RowButton;

        private ProjectsListRows _projectsListRows = null;

        private void Awake()
        {
            _projectsListRows = GetComponentInParent<ProjectsListRows>();

            RowButton.onClick.AddListener(OnRowButtonClick);
        }

        private void OnDestroy()
        {
            RowButton.onClick.RemoveListener(OnRowButtonClick);
        }

        public void Initialise(ProjectsList.ListItem listItem)
        {
            string name = Path.GetFileName(listItem.Path);
            NameText.text = name;

            string path = listItem.Path;
            path = path.Replace("\\", "\\\\");
            PathText.text = path;
        }

        public void OnRowButtonClick()
        {
            _projectsListRows.OnRowButtonClick?.Invoke(this);
        }
    }
}
