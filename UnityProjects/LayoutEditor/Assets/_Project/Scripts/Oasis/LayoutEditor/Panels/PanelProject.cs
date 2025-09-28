using RuntimeInspectorNamespace;
using System;
using System.IO;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelProject : PanelBase
    {
        private const string kPseudoSceneName = "Assets";

        private RuntimeHierarchy _runtimeHierarchy = null;
        private Transform _runtimeHierarchyAssetsRootTransform;
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
        }

        protected override void RemoveListeners()
        {
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

            _runtimeHierarchy.CreatePseudoScene(kPseudoSceneName);


            _initialised = true;
        }

        protected override void Populate()
        {
            RefreshAssetsPseudoScene();
        }

        private void Update()
        {
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

            Transform assetsRootTransform = CreateAssetsHierarchy(assetsPath);

            if (assetsRootTransform != null)
            {
                _runtimeHierarchy.AddToPseudoScene(kPseudoSceneName, assetsRootTransform);
            }
        }

        private Transform CreateAssetsHierarchy(string assetsPath)
        {
            ClearAssetsPseudoScene();

            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            string rootName = Path.GetFileName(assetsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.IsNullOrEmpty(rootName))
            {
                rootName = kPseudoSceneName;
            }

            _runtimeHierarchyAssetsRootTransform = CreateNamedTransform(rootName, null);

            PopulateDirectoryTransforms(assetsPath, _runtimeHierarchyAssetsRootTransform);

            return _runtimeHierarchyAssetsRootTransform;
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
                Debug.LogWarning($"Failed to enumerate directories in '{directoryPath}': {exception.Message}");
                subDirectories = Array.Empty<string>();
            }

            foreach (string subDirectory in subDirectories)
            {
                string directoryName = Path.GetFileName(subDirectory);

                Transform directoryTransform = CreateNamedTransform(directoryName, parentTransform);

                PopulateDirectoryTransforms(subDirectory, directoryTransform);
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(directoryPath);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to enumerate files in '{directoryPath}': {exception.Message}");
                files = Array.Empty<string>();
            }

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                CreateNamedTransform(fileName, parentTransform);
            }
        }

        private void ClearAssetsPseudoScene()
        {
            if (_runtimeHierarchyAssetsRootTransform == null)
            {
                return;
            }

            if (_runtimeHierarchy != null)
            {
                _runtimeHierarchy.RemoveFromPseudoScene(kPseudoSceneName, _runtimeHierarchyAssetsRootTransform, false);
            }

            DestroyTransform(_runtimeHierarchyAssetsRootTransform);
            _runtimeHierarchyAssetsRootTransform = null;
        }

        private Transform CreateNamedTransform(string name, Transform parentTransform)
        {
            GameObject gameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

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
                Debug.LogWarning($"Failed to watch assets directory '{assetsPath}': {exception.Message}");
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
    }

}
