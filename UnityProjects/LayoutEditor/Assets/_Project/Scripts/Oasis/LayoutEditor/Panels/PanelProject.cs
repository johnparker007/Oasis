using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using NativeWindowsContextMenu;
using System.Diagnostics;
#endif

namespace Oasis.LayoutEditor.Panels
{
    public class PanelProject : PanelBase
    {
        private const string kPseudoSceneName = "Assets";

        private RuntimeHierarchy _runtimeHierarchy = null;
        private RuntimeHierarchyRightClickBroadcaster _hierarchyRightClickBroadcaster = null;
        private readonly List<Transform> _runtimeHierarchyAssetsRootTransforms = new List<Transform>();
        private FileSystemWatcher _assetsDirectoryWatcher;
        private string _watchedAssetsPath;
        private string _lastKnownProjectRootPath;
        private volatile bool _assetsHierarchyDirty;


        protected override void Awake()
        {
            base.Awake();

            
        }

        protected override void AddListeners()
        {
            _assetsHierarchyDirty = true;

            EnsureHierarchyBroadcaster();

            if (_hierarchyRightClickBroadcaster != null)
            {
                _hierarchyRightClickBroadcaster.DrawerRightClicked -= OnHierarchyDrawerRightClicked;
                _hierarchyRightClickBroadcaster.DrawerRightClicked += OnHierarchyDrawerRightClicked;
            }
        }

        protected override void RemoveListeners()
        {
            if (_hierarchyRightClickBroadcaster != null)
            {
                _hierarchyRightClickBroadcaster.DrawerRightClicked -= OnHierarchyDrawerRightClicked;
            }

            DisposeAssetsWatcher();
            ClearAssetsPseudoScene();
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _runtimeHierarchy = GetComponentInChildren<RuntimeHierarchy>(true);

            if (_runtimeHierarchy != null)
            {
                EnsureHierarchyBroadcaster();
            }

            _runtimeHierarchy.CreatePseudoScene(kPseudoSceneName);


            _initialised = true;
        }

        protected override void Populate()
        {
            RefreshAssetsPseudoScene();
        }

        private void EnsureHierarchyBroadcaster()
        {
            if (_runtimeHierarchy == null)
            {
                _hierarchyRightClickBroadcaster = null;
                return;
            }

            if (_hierarchyRightClickBroadcaster != null)
            {
                return;
            }

            _hierarchyRightClickBroadcaster = _runtimeHierarchy.GetComponent<RuntimeHierarchyRightClickBroadcaster>();

            if (_hierarchyRightClickBroadcaster == null)
            {
                _hierarchyRightClickBroadcaster = _runtimeHierarchy.gameObject.AddComponent<RuntimeHierarchyRightClickBroadcaster>();
            }

            _hierarchyRightClickBroadcaster.ForceScan();
        }

        protected override void Update()
        {
            base.Update();

            if (!_initialised)
            {
                return;
            }

            string currentProjectRootPath = GetCurrentProjectRootPath();

            if (!string.Equals(_lastKnownProjectRootPath, currentProjectRootPath, StringComparison.OrdinalIgnoreCase))
            {
                _lastKnownProjectRootPath = currentProjectRootPath;
                _assetsHierarchyDirty = true;
            }

            if (_assetsHierarchyDirty)
            {
                _assetsHierarchyDirty = false;
                RefreshAssetsPseudoScene();
            }
        }

        private void RefreshAssetsPseudoScene()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            string assetsPath = GetCurrentProjectAssetsPath();

            SetupAssetsWatcher(assetsPath);

            CreateAssetsHierarchy(assetsPath);
        }

        private void CreateAssetsHierarchy(string assetsPath)
        {
            ClearAssetsPseudoScene();

            if (string.IsNullOrEmpty(assetsPath))
            {
                return;
            }

            string[] topLevelDirectories;

            try
            {
                topLevelDirectories = Directory.GetDirectories(assetsPath);
                Array.Sort(topLevelDirectories, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to enumerate directories in '{assetsPath}': {exception.Message}");
                return;
            }

            foreach (string directoryPath in topLevelDirectories)
            {
                string directoryName = Path.GetFileName(directoryPath);

                Transform directoryTransform = CreateNamedTransform(directoryName, null, directoryPath);

                _runtimeHierarchy.AddToPseudoScene(kPseudoSceneName, directoryTransform);
                _runtimeHierarchyAssetsRootTransforms.Add(directoryTransform);

                PopulateDirectoryTransforms(directoryPath, directoryTransform);
            }
        }

        private void PopulateDirectoryTransforms(string directoryPath, Transform parentTransform)
        {
            string[] subDirectories;

            try
            {
                subDirectories = Directory.GetDirectories(directoryPath);
                Array.Sort(subDirectories, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to enumerate directories in '{directoryPath}': {exception.Message}");
                subDirectories = Array.Empty<string>();
            }

            foreach (string subDirectory in subDirectories)
            {
                string directoryName = Path.GetFileName(subDirectory);

                Transform directoryTransform = CreateNamedTransform(directoryName, parentTransform, subDirectory);

                PopulateDirectoryTransforms(subDirectory, directoryTransform);
            }

        }

        private void OnHierarchyDrawerRightClicked(HierarchyField drawer, PointerEventData eventData)
        {
            if (drawer == null)
            {
                return;
            }

            HierarchyData data = drawer.Data;

            if (data == null)
            {
                return;
            }

            Transform boundTransform = data.BoundTransform;

            if (boundTransform == null)
            {
                if (data is HierarchyDataRootPseudoScene pseudoSceneData &&
                    string.Equals(pseudoSceneData.Name, kPseudoSceneName, StringComparison.Ordinal))
                {
                    string assetsPath = GetCurrentProjectAssetsPath();

                    if (!string.IsNullOrEmpty(assetsPath))
                    {
                        ShowDirectoryContextMenu(assetsPath);
                    }
                }

                return;
            }

            DirectoryMetadata metadata = boundTransform.GetComponent<DirectoryMetadata>();

            if (metadata == null || string.IsNullOrEmpty(metadata.DirectoryPath))
            {
                return;
            }

            ShowDirectoryContextMenu(metadata.DirectoryPath);
        }

        private void ClearAssetsPseudoScene()
        {
            if (_runtimeHierarchyAssetsRootTransforms.Count == 0)
            {
                return;
            }

            if (_runtimeHierarchy != null)
            {
                foreach (Transform rootTransform in _runtimeHierarchyAssetsRootTransforms)
                {
                    _runtimeHierarchy.RemoveFromPseudoScene(kPseudoSceneName, rootTransform, false);
                }
            }

            foreach (Transform rootTransform in _runtimeHierarchyAssetsRootTransforms)
            {
                DestroyTransform(rootTransform);
            }

            _runtimeHierarchyAssetsRootTransforms.Clear();
        }

        private Transform CreateNamedTransform(string name, Transform parentTransform, string directoryPath)
        {
            GameObject gameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            DirectoryMetadata metadata = gameObject.AddComponent<DirectoryMetadata>();
            metadata.Initialise(directoryPath);

            Transform transform = gameObject.transform;

            if (parentTransform != null)
            {
                transform.SetParent(parentTransform, false);
            }

            return transform;
        }

        private void DestroyTransform(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(transform.gameObject);
            }
            else
            {
                DestroyImmediate(transform.gameObject);
            }
        }

        private void ShowDirectoryContextMenu(string directoryPath)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (string.IsNullOrEmpty(directoryPath))
            {
                return;
            }

            NativeContextMenuManager manager = EnsureContextMenuManager();

            if (manager == null)
            {
                return;
            }

            bool directoryExists = Directory.Exists(directoryPath);

            var menuItems = new List<NativeContextMenuManager.MenuItemSpec>
            {
                new NativeContextMenuManager.MenuItemSpec(
                    "Show in Explorer",
                    () => ShowDirectoryInExplorer(directoryPath),
                    directoryExists)
            };

            manager.ShowMenuAtCursor(menuItems);
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static NativeContextMenuManager EnsureContextMenuManager()
        {
            if (NativeContextMenuManager.Instance != null)
            {
                return NativeContextMenuManager.Instance;
            }

            GameObject managerObject = new GameObject("NativeContextMenuManager");
            return managerObject.AddComponent<NativeContextMenuManager>();
        }

        private static void ShowDirectoryInExplorer(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"\"{directoryPath}\"");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to open directory '{directoryPath}' in Explorer: {exception.Message}");
            }
        }
#endif

        private string GetCurrentProjectRootPath()
        {
            Oasis.Editor editorInstance = Oasis.Editor.Instance;

            if (editorInstance?.ProjectsController == null)
            {
                return null;
            }

            return editorInstance.ProjectsController.ProjectRootPath;
        }

        private string GetCurrentProjectAssetsPath()
        {
            Oasis.Editor editorInstance = Oasis.Editor.Instance;

            if (editorInstance?.ProjectsController == null)
            {
                return null;
            }

            string projectRootPath = editorInstance.ProjectsController.ProjectRootPath;

            if (string.IsNullOrEmpty(projectRootPath))
            {
                return null;
            }

            string assetsPath;

            try
            {
                assetsPath = editorInstance.ProjectsController.ProjectAssetsPath;
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!Directory.Exists(assetsPath))
            {
                return null;
            }

            return assetsPath;
        }

        private void SetupAssetsWatcher(string assetsPath)
        {
            if (string.Equals(_watchedAssetsPath, assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DisposeAssetsWatcher();

            if (string.IsNullOrEmpty(assetsPath))
            {
                return;
            }

            try
            {
                FileSystemWatcher watcher = new FileSystemWatcher(assetsPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };

                watcher.Created += OnAssetsDirectoryChanged;
                watcher.Changed += OnAssetsDirectoryChanged;
                watcher.Deleted += OnAssetsDirectoryChanged;
                watcher.Renamed += OnAssetsDirectoryRenamed;
                watcher.EnableRaisingEvents = true;

                _assetsDirectoryWatcher = watcher;
                _watchedAssetsPath = assetsPath;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to watch assets directory '{assetsPath}': {exception.Message}");
            }
        }

        private void DisposeAssetsWatcher()
        {
            if (_assetsDirectoryWatcher == null)
            {
                _watchedAssetsPath = null;
                return;
            }

            _assetsDirectoryWatcher.EnableRaisingEvents = false;
            _assetsDirectoryWatcher.Created -= OnAssetsDirectoryChanged;
            _assetsDirectoryWatcher.Changed -= OnAssetsDirectoryChanged;
            _assetsDirectoryWatcher.Deleted -= OnAssetsDirectoryChanged;
            _assetsDirectoryWatcher.Renamed -= OnAssetsDirectoryRenamed;
            _assetsDirectoryWatcher.Dispose();
            _assetsDirectoryWatcher = null;
            _watchedAssetsPath = null;
        }

        private void OnAssetsDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            _assetsHierarchyDirty = true;
        }

        private void OnAssetsDirectoryRenamed(object sender, RenamedEventArgs e)
        {
            _assetsHierarchyDirty = true;
        }

        private sealed class DirectoryMetadata : MonoBehaviour
        {
            public string DirectoryPath { get; private set; }

            public void Initialise(string directoryPath)
            {
                DirectoryPath = directoryPath;
            }
        }
    }

}
