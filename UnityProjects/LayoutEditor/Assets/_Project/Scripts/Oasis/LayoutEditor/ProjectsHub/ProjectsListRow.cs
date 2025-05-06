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
        public Button RemoveButton;

        private ProjectsListRows _projectsListRows = null;

        private void Awake()
        {
            _projectsListRows = GetComponentInParent<ProjectsListRows>();

            RowButton.onClick.AddListener(OnRowButtonClick);
            RemoveButton.onClick.AddListener(OnRemoveButtonClick);
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

        public void OnRemoveButtonClick()
        {
            _projectsListRows.OnRemoveButtonClick?.Invoke(this);
        }
    }
}
