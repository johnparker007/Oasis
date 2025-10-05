using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        [SerializeField] private RuntimeHierarchy _runtimeHierarchyFoldersTree = null;
        [SerializeField] private RuntimeHierarchy _runtimeHierarchyFilesAndFoldersList = null;

        private RuntimeHierarchyRightClickBroadcaster _foldersTreeRightClickBroadcaster = null;
        private RuntimeHierarchyRightClickBroadcaster _filesAndFoldersRightClickBroadcaster = null;
        private readonly List<Transform> _runtimeHierarchyAssetsRootTransforms = new List<Transform>();
        private readonly List<Transform> _runtimeHierarchyFilesAndFoldersTransforms = new List<Transform>();
        private RuntimeHierarchyStandaloneTransformCollection _runtimeHierarchyFilesAndFoldersStandaloneCollection;
        private readonly Dictionary<string, Transform> _directoryTransformsByPath = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher _assetsDirectoryWatcher;
        private string _watchedAssetsPath;
        private string _lastKnownProjectRootPath;
        private volatile bool _assetsHierarchyDirty;
        private string _currentlySelectedDirectoryPath;


        protected override void Awake()
        {
            base.Awake();

            
        }

        protected override void AddListeners()
        {
            _assetsHierarchyDirty = true;

            if (_runtimeHierarchyFoldersTree != null)
            {
                _runtimeHierarchyFoldersTree.OnSelectionChanged -= OnFoldersTreeSelectionChanged;
                _runtimeHierarchyFoldersTree.OnSelectionChanged += OnFoldersTreeSelectionChanged;
            }

            _foldersTreeRightClickBroadcaster = EnsureHierarchyBroadcaster(
                _runtimeHierarchyFoldersTree,
                _foldersTreeRightClickBroadcaster);

            if (_foldersTreeRightClickBroadcaster != null)
            {
                _foldersTreeRightClickBroadcaster.DrawerRightClicked -= OnFoldersTreeDrawerRightClicked;
                _foldersTreeRightClickBroadcaster.DrawerRightClicked += OnFoldersTreeDrawerRightClicked;
            }

            if (_runtimeHierarchyFilesAndFoldersList != null)
            {
                _runtimeHierarchyFilesAndFoldersList.OnItemDoubleClicked -= OnFilesAndFoldersItemDoubleClicked;
                _runtimeHierarchyFilesAndFoldersList.OnItemDoubleClicked += OnFilesAndFoldersItemDoubleClicked;
            }

            _filesAndFoldersRightClickBroadcaster = EnsureHierarchyBroadcaster(
                _runtimeHierarchyFilesAndFoldersList,
                _filesAndFoldersRightClickBroadcaster);

            if (_filesAndFoldersRightClickBroadcaster != null)
            {
                _filesAndFoldersRightClickBroadcaster.DrawerRightClicked -= OnFilesAndFoldersDrawerRightClicked;
                _filesAndFoldersRightClickBroadcaster.DrawerRightClicked += OnFilesAndFoldersDrawerRightClicked;
            }
        }

        protected override void RemoveListeners()
        {
            if (_foldersTreeRightClickBroadcaster != null)
            {
                _foldersTreeRightClickBroadcaster.DrawerRightClicked -= OnFoldersTreeDrawerRightClicked;
                _foldersTreeRightClickBroadcaster = null;
            }

            if (_filesAndFoldersRightClickBroadcaster != null)
            {
                _filesAndFoldersRightClickBroadcaster.DrawerRightClicked -= OnFilesAndFoldersDrawerRightClicked;
                _filesAndFoldersRightClickBroadcaster = null;
            }

            if (_runtimeHierarchyFoldersTree != null)
            {
                _runtimeHierarchyFoldersTree.OnSelectionChanged -= OnFoldersTreeSelectionChanged;
            }

            if (_runtimeHierarchyFilesAndFoldersList != null)
            {
                _runtimeHierarchyFilesAndFoldersList.OnItemDoubleClicked -= OnFilesAndFoldersItemDoubleClicked;
            }

            DisposeAssetsWatcher();
            ClearAssetsPseudoScene();
            ClearFilesAndFoldersPseudoScene();
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            if (_runtimeHierarchyFoldersTree == null || _runtimeHierarchyFilesAndFoldersList == null)
            {
                RuntimeHierarchy[] runtimeHierarchies = GetComponentsInChildren<RuntimeHierarchy>(true);

                if (runtimeHierarchies != null)
                {
                    for (int i = 0; i < runtimeHierarchies.Length; i++)
                    {
                        RuntimeHierarchy hierarchy = runtimeHierarchies[i];

                        if (hierarchy == null)
                        {
                            continue;
                        }

                        if (_runtimeHierarchyFoldersTree == null)
                        {
                            _runtimeHierarchyFoldersTree = hierarchy;
                            continue;
                        }

                        if (_runtimeHierarchyFilesAndFoldersList == null && hierarchy != _runtimeHierarchyFoldersTree)
                        {
                            _runtimeHierarchyFilesAndFoldersList = hierarchy;
                        }

                        if (_runtimeHierarchyFoldersTree != null && _runtimeHierarchyFilesAndFoldersList != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (_runtimeHierarchyFoldersTree != null)
            {
                _foldersTreeRightClickBroadcaster = EnsureHierarchyBroadcaster(
                    _runtimeHierarchyFoldersTree,
                    _foldersTreeRightClickBroadcaster);
                _runtimeHierarchyFoldersTree.CreatePseudoScene(kPseudoSceneName);
            }

            if (_runtimeHierarchyFilesAndFoldersList != null && _runtimeHierarchyFilesAndFoldersStandaloneCollection == null)
            {
                _runtimeHierarchyFilesAndFoldersStandaloneCollection = new RuntimeHierarchyStandaloneTransformCollection(
                    _runtimeHierarchyFilesAndFoldersList);
            }

            if (_runtimeHierarchyFilesAndFoldersList != null)
            {
                _filesAndFoldersRightClickBroadcaster = EnsureHierarchyBroadcaster(
                    _runtimeHierarchyFilesAndFoldersList,
                    _filesAndFoldersRightClickBroadcaster);
            }


            _initialised = true;
        }

        protected override void Populate()
        {
            RefreshAssetsPseudoScene();
        }

        private static RuntimeHierarchyRightClickBroadcaster EnsureHierarchyBroadcaster(
            RuntimeHierarchy hierarchy,
            RuntimeHierarchyRightClickBroadcaster currentBroadcaster)
        {
            if (hierarchy == null)
            {
                return null;
            }

            RuntimeHierarchyRightClickBroadcaster broadcaster = currentBroadcaster;

            if (broadcaster == null)
            {
                broadcaster = hierarchy.GetComponent<RuntimeHierarchyRightClickBroadcaster>();

                if (broadcaster == null)
                {
                    broadcaster = hierarchy.gameObject.AddComponent<RuntimeHierarchyRightClickBroadcaster>();
                }
            }

            broadcaster.ForceScan();
            return broadcaster;
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
            if (_runtimeHierarchyFoldersTree == null && _runtimeHierarchyFilesAndFoldersList == null)
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

            if (string.IsNullOrEmpty(assetsPath) || !Directory.Exists(assetsPath))
            {
                _currentlySelectedDirectoryPath = null;
                RefreshFilesAndFoldersList(null);
                return;
            }

            _directoryTransformsByPath[assetsPath] = null;

            string[] topLevelDirectories;

            try
            {
                topLevelDirectories = Directory.GetDirectories(assetsPath);
                Array.Sort(topLevelDirectories, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to enumerate directories in '{assetsPath}': {exception.Message}");
                topLevelDirectories = Array.Empty<string>();
            }

            if (_runtimeHierarchyFoldersTree != null)
            {
                foreach (string directoryPath in topLevelDirectories)
                {
                    string directoryName = Path.GetFileName(directoryPath);

                    Transform directoryTransform = CreateDirectoryTransform(directoryName, null, directoryPath, true);

                    _runtimeHierarchyFoldersTree.AddToPseudoScene(kPseudoSceneName, directoryTransform);
                    _runtimeHierarchyAssetsRootTransforms.Add(directoryTransform);

                    PopulateDirectoryTransforms(directoryPath, directoryTransform);
                }
            }

            RestoreFoldersTreeSelection();
            RefreshFilesAndFoldersList(_currentlySelectedDirectoryPath);
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

                Transform directoryTransform = CreateDirectoryTransform(directoryName, parentTransform, subDirectory, true);

                PopulateDirectoryTransforms(subDirectory, directoryTransform);
            }

        }

        private void OnFoldersTreeSelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            string directoryPath = GetDirectoryPathFromSelection(selection);

            if (string.IsNullOrEmpty(directoryPath))
            {
                directoryPath = GetCurrentProjectAssetsPath();
            }

            RefreshFilesAndFoldersList(directoryPath);
        }

        private string GetDirectoryPathFromSelection(ReadOnlyCollection<Transform> selection)
        {
            if (selection == null)
            {
                return null;
            }

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                Transform selectedTransform = selection[i];

                if (selectedTransform == null)
                {
                    continue;
                }

                DirectoryMetadata metadata = selectedTransform.GetComponent<DirectoryMetadata>();

                if (metadata != null && !string.IsNullOrEmpty(metadata.DirectoryPath))
                {
                    return metadata.DirectoryPath;
                }
            }

            return null;
        }

        private void RefreshFilesAndFoldersList(string directoryPath)
        {
            ClearFilesAndFoldersPseudoScene();

            string targetDirectory = directoryPath;

            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                targetDirectory = GetCurrentProjectAssetsPath();
            }

            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                _currentlySelectedDirectoryPath = null;
                return;
            }

            _currentlySelectedDirectoryPath = targetDirectory;

            if (_runtimeHierarchyFilesAndFoldersList == null)
            {
                return;
            }

            string[] subDirectories = Array.Empty<string>();

            try
            {
                subDirectories = Directory.GetDirectories(targetDirectory);
                Array.Sort(subDirectories, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to enumerate directories in '{targetDirectory}': {exception.Message}");
                subDirectories = Array.Empty<string>();
            }

            foreach (string subDirectory in subDirectories)
            {
                string directoryName = Path.GetFileName(subDirectory);

                Transform directoryTransform = CreateDirectoryTransform(directoryName, null, subDirectory, false);

                _runtimeHierarchyFilesAndFoldersStandaloneCollection?.Add(directoryTransform);
                _runtimeHierarchyFilesAndFoldersTransforms.Add(directoryTransform);
            }

            string[] files = Array.Empty<string>();

            try
            {
                files = Directory.GetFiles(targetDirectory);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to enumerate files in '{targetDirectory}': {exception.Message}");
                files = Array.Empty<string>();
            }

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);

                Transform fileTransform = CreateFileTransform(fileName, filePath);

                _runtimeHierarchyFilesAndFoldersStandaloneCollection?.Add(fileTransform);
                _runtimeHierarchyFilesAndFoldersTransforms.Add(fileTransform);
            }
        }

        private void OnFilesAndFoldersItemDoubleClicked(HierarchyData data)
        {
            if (data == null)
            {
                return;
            }

            Transform boundTransform = data.BoundTransform;

            if (boundTransform == null)
            {
                return;
            }

            DirectoryMetadata directoryMetadata = boundTransform.GetComponent<DirectoryMetadata>();

            if (directoryMetadata != null && !string.IsNullOrEmpty(directoryMetadata.DirectoryPath))
            {
                NavigateToDirectory(directoryMetadata.DirectoryPath);
                return;
            }

            FileMetadata fileMetadata = boundTransform.GetComponent<FileMetadata>();

            if (fileMetadata != null && !string.IsNullOrEmpty(fileMetadata.FilePath))
            {
                LaunchFile(fileMetadata.FilePath);
            }
        }

        private void NavigateToDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            if (_runtimeHierarchyFoldersTree != null &&
                _directoryTransformsByPath.TryGetValue(directoryPath, out Transform directoryTransform) &&
                directoryTransform != null)
            {
                _runtimeHierarchyFoldersTree.Select(directoryTransform);
                return;
            }

            RefreshFilesAndFoldersList(directoryPath);
        }

        private void LaunchFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to launch file '{filePath}': {exception.Message}");
            }
#else
            try
            {
                string fileUri = new Uri(filePath).AbsoluteUri;
                Application.OpenURL(fileUri);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to launch file '{filePath}': {exception.Message}");
            }
#endif
        }

        private void RestoreFoldersTreeSelection()
        {
            if (_runtimeHierarchyFoldersTree == null)
            {
                return;
            }

            string desiredDirectoryPath = _currentlySelectedDirectoryPath;

            if (string.IsNullOrEmpty(desiredDirectoryPath))
            {
                desiredDirectoryPath = GetCurrentProjectAssetsPath();
            }

            if (string.IsNullOrEmpty(desiredDirectoryPath))
            {
                return;
            }

            if (_directoryTransformsByPath.TryGetValue(desiredDirectoryPath, out Transform directoryTransform) && directoryTransform != null)
            {
                ReadOnlyCollection<Transform> currentSelection = _runtimeHierarchyFoldersTree.CurrentSelection;

                if (!IsTransformInSelection(currentSelection, directoryTransform))
                {
                    _runtimeHierarchyFoldersTree.Select(directoryTransform);
                }
            }
            else
            {
                ReadOnlyCollection<Transform> currentSelection = _runtimeHierarchyFoldersTree.CurrentSelection;

                if (currentSelection != null && currentSelection.Count > 0)
                {
                    _runtimeHierarchyFoldersTree.Deselect();
                }
            }
        }

        private static bool IsTransformInSelection(ReadOnlyCollection<Transform> selection, Transform targetTransform)
        {
            if (selection == null || targetTransform == null)
            {
                return false;
            }

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                if (selection[i] == targetTransform)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearFilesAndFoldersPseudoScene()
        {
            _runtimeHierarchyFilesAndFoldersStandaloneCollection?.Clear();

            if (_runtimeHierarchyFilesAndFoldersTransforms.Count > 0)
            {
                foreach (Transform entryTransform in _runtimeHierarchyFilesAndFoldersTransforms)
                {
                    DestroyTransform(entryTransform);
                }
            }

            _runtimeHierarchyFilesAndFoldersTransforms.Clear();
        }

        private Transform CreateFileTransform(string name, string filePath)
        {
            GameObject gameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            FileMetadata metadata = gameObject.AddComponent<FileMetadata>();
            metadata.Initialise(filePath);

            return gameObject.transform;
        }

        private void OnFoldersTreeDrawerRightClicked(HierarchyField drawer, PointerEventData eventData)
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

        private void OnFilesAndFoldersDrawerRightClicked(HierarchyField drawer, PointerEventData eventData)
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
            if (_runtimeHierarchyAssetsRootTransforms.Count > 0)
            {
                if (_runtimeHierarchyFoldersTree != null)
                {
                    foreach (Transform rootTransform in _runtimeHierarchyAssetsRootTransforms)
                    {
                        _runtimeHierarchyFoldersTree.RemoveFromPseudoScene(kPseudoSceneName, rootTransform, false);
                    }
                }

                foreach (Transform rootTransform in _runtimeHierarchyAssetsRootTransforms)
                {
                    DestroyTransform(rootTransform);
                }
            }

            _runtimeHierarchyAssetsRootTransforms.Clear();
            _directoryTransformsByPath.Clear();
        }

        private Transform CreateDirectoryTransform(string name, Transform parentTransform, string directoryPath, bool registerDirectoryTransform)
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

            if (registerDirectoryTransform && !string.IsNullOrEmpty(directoryPath))
            {
                _directoryTransformsByPath[directoryPath] = transform;
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

        private sealed class FileMetadata : MonoBehaviour
        {
            public string FilePath { get; private set; }

            public void Initialise(string filePath)
            {
                FilePath = filePath;
            }
        }
    }

}
