using RuntimeInspectorNamespace;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oasis.LayoutEditor.RuntimeHierarchyIntegration
{
    internal sealed class RuntimeHierarchyStandaloneTransformCollection
    {
        private static readonly FieldInfo SceneDataField = typeof(RuntimeHierarchy).GetField(
            "sceneData",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SearchSceneDataField = typeof(RuntimeHierarchy).GetField(
            "searchSceneData",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly RuntimeHierarchy _runtimeHierarchy;
        private readonly List<Transform> _transforms = new List<Transform>();

        private string _pseudoSceneName;
        private HierarchyDataRootPseudoScene _pseudoSceneData;
        private HierarchyDataRootSearch _pseudoSceneSearchData;

        public RuntimeHierarchyStandaloneTransformCollection(RuntimeHierarchy runtimeHierarchy)
        {
            _runtimeHierarchy = runtimeHierarchy;
        }

        public void Add(Transform transform)
        {
            if (_runtimeHierarchy == null || transform == null)
            {
                return;
            }

            if (_transforms.Contains(transform))
            {
                return;
            }

            EnsurePseudoScene();

            if (string.IsNullOrEmpty(_pseudoSceneName))
            {
                return;
            }

            _runtimeHierarchy.AddToPseudoScene(_pseudoSceneName, transform);

            _transforms.Add(transform);

            EnsurePseudoSceneExpanded();
        }

        public void Remove(Transform transform)
        {
            if (_runtimeHierarchy == null || transform == null)
            {
                return;
            }

            if (!_transforms.Remove(transform))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pseudoSceneName))
            {
                _runtimeHierarchy.RemoveFromPseudoScene(_pseudoSceneName, transform, false);
            }

            if (_transforms.Count == 0)
            {
                DeletePseudoScene();
            }
        }

        public void Clear()
        {
            if (_runtimeHierarchy == null || _transforms.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_pseudoSceneName))
            {
                _runtimeHierarchy.RemoveFromPseudoScene(_pseudoSceneName, _transforms, false);
                DeletePseudoScene();
            }

            _transforms.Clear();
        }

        private void EnsurePseudoScene()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_pseudoSceneName))
            {
                _pseudoSceneName = $"Standalone_{_runtimeHierarchy.GetInstanceID()}_{Guid.NewGuid():N}";
            }

            if (_pseudoSceneData == null)
            {
                _runtimeHierarchy.CreatePseudoScene(_pseudoSceneName);
                UpdatePseudoSceneReferences();
            }
        }

        private void UpdatePseudoSceneReferences()
        {
            if (_runtimeHierarchy == null || SceneDataField == null || SearchSceneDataField == null)
            {
                return;
            }

            List<HierarchyDataRoot> sceneData = SceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;
            List<HierarchyDataRoot> searchSceneData = SearchSceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;

            if (sceneData == null || searchSceneData == null)
            {
                return;
            }

            for (int i = 0; i < sceneData.Count && i < searchSceneData.Count; i++)
            {
                if (sceneData[i] is HierarchyDataRootPseudoScene pseudoScene &&
                    pseudoScene.Name == _pseudoSceneName)
                {
                    _pseudoSceneData = pseudoScene;
                    _pseudoSceneSearchData = searchSceneData[i] as HierarchyDataRootSearch;
                    EnsurePseudoSceneExpanded();
                    return;
                }
            }
        }

        private void EnsurePseudoSceneExpanded()
        {
            if (_pseudoSceneData != null)
            {
                _pseudoSceneData.IsExpanded = true;
            }

            if (_pseudoSceneSearchData != null)
            {
                _pseudoSceneSearchData.IsExpanded = true;
            }
        }

        private void DeletePseudoScene()
        {
            if (_runtimeHierarchy == null || string.IsNullOrEmpty(_pseudoSceneName))
            {
                return;
            }

            _runtimeHierarchy.DeletePseudoScene(_pseudoSceneName);

            _pseudoSceneData = null;
            _pseudoSceneSearchData = null;
            _pseudoSceneName = null;
        }
    }
}
