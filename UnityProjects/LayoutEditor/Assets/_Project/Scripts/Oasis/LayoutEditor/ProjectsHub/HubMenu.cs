using Oasis.Projects;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class HubMenu : MonoBehaviour
    {
        public Button NewProjectButton;
        public Button AddProjectButton;
        public ProjectsListRows ProjectsListRows;


        private ProjectsHubController _projectsHubController = null;

        private bool _rebuildRequired = false;

        public List<ProjectsList.ListItem> ListItems
        {
            get
            {
                if(Editor.Instance.ProjectsController.ProjectsList == null)
                {
                    return null;
                }

                return Editor.Instance.ProjectsController.ProjectsList.ListItems;
            }
        }

        private void Awake()
        {
            _projectsHubController = GetComponentInParent<ProjectsHubController>();

            NewProjectButton.onClick.AddListener(OnNewProjectButtonClick);
            AddProjectButton.onClick.AddListener(OnAddProjectButtonClick);

            ProjectsListRows.OnRowButtonClick.AddListener(OnRowButtonClick);
            ProjectsListRows.OnRemoveButtonClick.AddListener(OnRemoveButtonClick);
        }

        private void Start()
        {
            Editor.Instance.ProjectsController.ProjectsList.OnListModified.AddListener(OnListModified);

            _rebuildRequired = true;
        }

        private void Update()
        {
            if(_rebuildRequired && ListItems != null)
            {
                ProjectsListRows.Rebuild(ListItems);
                _rebuildRequired = false;
            }
        }

        private void OnDestroy()
        {
            NewProjectButton.onClick.RemoveListener(OnNewProjectButtonClick);
            AddProjectButton.onClick.RemoveListener(OnAddProjectButtonClick);

            ProjectsListRows.OnRowButtonClick.RemoveListener(OnRowButtonClick);
            ProjectsListRows.OnRemoveButtonClick.RemoveListener(OnRemoveButtonClick);

            Editor.Instance.ProjectsController.ProjectsList.OnListModified.RemoveListener(OnListModified);
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

        private void OnListModified()
        {
            _rebuildRequired = true;
        }

        private void OnRowButtonClick(ProjectsListRow row)
        {
            Editor.Instance.ProjectsController.LoadProject(row.PathText.text);
            _projectsHubController.SetHubMenuActive(false);
        }

        private void OnRemoveButtonClick(ProjectsListRow row)
        {
            string textLookup = row.PathText.text.Replace("\\\\", "\\");
            Editor.Instance.ProjectsController.ProjectsList.RemoveListItem(textLookup);
        }
    }
}
