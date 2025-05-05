using Oasis.Projects;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class ProjectsListRows : MonoBehaviour
    {
        public ProjectsListRow ProjectsListRowPrefab;

        public UnityEvent<ProjectsListRow> OnRowButtonClick = null;

        private List<ProjectsListRow> _rows = new List<ProjectsListRow>();

        public void Rebuild(List<ProjectsList.ListItem> listItems)
        {
            DeleteCurrentRows();

            foreach (ProjectsList.ListItem listItem in listItems)
            {
                AddRow(listItem);
            }
        }

        private void DeleteCurrentRows()
        {
            foreach (ProjectsListRow row in _rows)
            {
                Destroy(row);
            }

            _rows.Clear();
        }

        private void AddRow(ProjectsList.ListItem listItem)
        {
            ProjectsListRow row = Instantiate(ProjectsListRowPrefab, transform);

            row.Initialise(listItem);

            _rows.Add(row);
        }
    }
}
