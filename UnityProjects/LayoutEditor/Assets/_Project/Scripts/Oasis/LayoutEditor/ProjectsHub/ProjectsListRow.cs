using Oasis.Projects;
using System.IO;
using UnityEngine;

namespace Oasis.LayoutEditor.ProjectsHub
{
    public class ProjectsListRow : MonoBehaviour
    {
        public TMPro.TMP_Text NameText;
        public TMPro.TMP_Text PathText;

        public void Initialise(ProjectsList.ListItem listItem)
        {
            string name = Path.GetFileName(listItem.Path);
            NameText.text = name;

            string path = listItem.Path;
            path = path.Replace("\\", "\\\\");
            PathText.text = path;
        }
    }
}
