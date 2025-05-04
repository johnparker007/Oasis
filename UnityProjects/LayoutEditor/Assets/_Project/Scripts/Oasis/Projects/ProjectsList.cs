using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Oasis.Projects
{
    public class ProjectsList : MonoBehaviour
    {
        private const bool kDebugOutput = true;
        private const string kFilename = "ProjectsList.json";

        [Serializable]
        public class ListItem
        {
            public string Path;
            public ulong LastModifiedTime;
        }

        [Serializable]
        public class ListItemsWrapper
        {
            public List<ListItem> ListItems = new List<ListItem>();
        }

        public UnityEvent OnListModified;

        public List<ListItem> ListItems
        {
            get
            {
                return _listItemsWrapper.ListItems;
            }
        }

        public string ProjectsListPath
        {
            get
            {
                return Path.Combine(Application.persistentDataPath, kFilename);
            }
        }

        private ListItemsWrapper _listItemsWrapper = null;


        private void Awake()
        {
            Load();
        }

        public void AddListItem(ListItem listItem)
        {
            ListItems.Add(listItem);
            Save();

            OnListModified?.Invoke();
        }

        // TODO remove list item

        public void Save()
        {
            string json = JsonUtility.ToJson(_listItemsWrapper, prettyPrint: true);
            File.WriteAllText(ProjectsListPath, json);
            Debug.LogError("Saved projects list to " + ProjectsListPath);
        }

        private void Load()
        {
            bool projectsListFilePresent = File.Exists(ProjectsListPath);

            if(!projectsListFilePresent)
            {
                _listItemsWrapper = new ListItemsWrapper();
                Save();
                Debug.LogError("Created new projects list");
            }
            else
            {
                string projectsListJson = File.ReadAllText(ProjectsListPath);
                _listItemsWrapper = JsonUtility.FromJson<ListItemsWrapper>(projectsListJson);
                Debug.LogError("Loaded projects list from " + ProjectsListPath);
            }

            OnListModified?.Invoke();
        }
    }

}
