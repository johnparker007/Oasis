using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelEditorPreferences : PanelBase
    {
        [System.Serializable]
        public struct PreferenceSection
        {
            public string Category;
            public GameObject MenuPane;
        }

        public List<PreferenceSection> PreferenceSections = new List<PreferenceSection>();

        private RuntimeHierarchy _runtimeHierarchy;
        private RuntimeHierarchyStandaloneTransformCollection _runtimeHierarchyTransformCollection;
        private readonly List<Transform> _sectionTransforms = new List<Transform>();
        private readonly Dictionary<Transform, string> _sectionNamesByTransform = new Dictionary<Transform, string>();

        protected override void AddListeners()
        {
            EnsureRuntimeHierarchy();

            if (_runtimeHierarchy != null)
            {
                _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
                _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
            }
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            EnsureRuntimeHierarchy();

            _initialised = true;
        }

        protected override void Populate()
        {
            CreatePreferenceSections();
        }

        protected override void RemoveListeners()
        {
            if (_runtimeHierarchy != null)
            {
                _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
            }

            ClearPreferenceSections();
        }

        private void EnsureRuntimeHierarchy()
        {
            if (_runtimeHierarchy == null)
            {
                _runtimeHierarchy = GetComponentInChildren<RuntimeHierarchy>(true);

                if (_runtimeHierarchy != null && _runtimeHierarchyTransformCollection == null)
                {
                    _runtimeHierarchyTransformCollection = new RuntimeHierarchyStandaloneTransformCollection(_runtimeHierarchy);
                }
            }
        }

        private void CreatePreferenceSections()
        {
            EnsureRuntimeHierarchy();

            if (_runtimeHierarchy == null)
            {
                return;
            }

            ClearPreferenceSections();

            foreach (PreferenceSection preferenceSection in PreferenceSections)
            {
                if (string.IsNullOrEmpty(preferenceSection.Category))
                {
                    continue;
                }

                Transform sectionTransform = CreateSectionTransform(preferenceSection.Category);

                _runtimeHierarchyTransformCollection?.Add(sectionTransform);

                _sectionTransforms.Add(sectionTransform);
                _sectionNamesByTransform[sectionTransform] = preferenceSection.Category;
            }
        }

        private Transform CreateSectionTransform(string sectionName)
        {
            GameObject sectionObject = new GameObject(sectionName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            return sectionObject.transform;
        }

        private void ClearPreferenceSections()
        {
            if (_sectionTransforms.Count > 0)
            {
                if (_runtimeHierarchyTransformCollection != null)
                {
                    foreach (Transform sectionTransform in _sectionTransforms)
                    {
                        _runtimeHierarchyTransformCollection.Remove(sectionTransform);
                    }
                }

                foreach (Transform sectionTransform in _sectionTransforms)
                {
                    DestroyTransform(sectionTransform);
                }
            }

            _sectionTransforms.Clear();
            _sectionNamesByTransform.Clear();
            _runtimeHierarchyTransformCollection?.Clear();
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

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            if (selection == null || selection.Count == 0)
            {
                return;
            }

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                Transform selectedTransform = selection[i];

                if (selectedTransform != null &&
                    _sectionNamesByTransform.TryGetValue(selectedTransform, out string sectionName))
                {
                    OnPreferenceSectionSelected(sectionName);
                    return;
                }
            }
        }

        private void OnPreferenceSectionSelected(string sectionName)
        {
            foreach (PreferenceSection preferenceSection in PreferenceSections)
            {
                bool shouldBeActive = preferenceSection.Category == sectionName;

                if (preferenceSection.MenuPane != null &&
                    preferenceSection.MenuPane.activeSelf != shouldBeActive)
                {
                    preferenceSection.MenuPane.SetActive(shouldBeActive);
                }
            }
        }
    }

}
