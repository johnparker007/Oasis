using Oasis.LayoutEditor.RuntimeHierarchyIntegration;
using RuntimeInspectorNamespace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private RuntimeHierarchyRightClickBroadcaster _hierarchyBroadcaster;
        private readonly List<string> _sectionPseudoSceneNames = new List<string>();
        private readonly HashSet<string> _sectionPseudoSceneLookup = new HashSet<string>();

        protected override void AddListeners()
        {
            EnsureRuntimeHierarchy();

            if (_runtimeHierarchy != null)
            {
                _runtimeHierarchy.OnSelectionChanged -= OnRuntimeHierarchySelectionChanged;
                _runtimeHierarchy.OnSelectionChanged += OnRuntimeHierarchySelectionChanged;
            }

            EnsureHierarchyBroadcaster();

            if (_hierarchyBroadcaster != null)
            {
                _hierarchyBroadcaster.DrawerClicked -= OnHierarchyDrawerClicked;
                _hierarchyBroadcaster.DrawerClicked += OnHierarchyDrawerClicked;
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

            if (_hierarchyBroadcaster != null)
            {
                _hierarchyBroadcaster.DrawerClicked -= OnHierarchyDrawerClicked;
            }

            ClearPreferenceSections();
        }

        private void EnsureRuntimeHierarchy()
        {
            if (_runtimeHierarchy == null)
            {
                _runtimeHierarchy = GetComponentInChildren<RuntimeHierarchy>(true);
            }
        }

        private void EnsureHierarchyBroadcaster()
        {
            if (_runtimeHierarchy == null)
            {
                _hierarchyBroadcaster = null;
                return;
            }

            if (_hierarchyBroadcaster != null)
            {
                return;
            }

            _hierarchyBroadcaster = _runtimeHierarchy.GetComponent<RuntimeHierarchyRightClickBroadcaster>();

            if (_hierarchyBroadcaster == null)
            {
                _hierarchyBroadcaster = _runtimeHierarchy.gameObject.AddComponent<RuntimeHierarchyRightClickBroadcaster>();
            }

            _hierarchyBroadcaster.ForceScan();
        }

        private void CreatePreferenceSections()
        {
            EnsureRuntimeHierarchy();
            EnsureHierarchyBroadcaster();

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

                if (_sectionPseudoSceneLookup.Add(preferenceSection.Category))
                {
                    _sectionPseudoSceneNames.Add(preferenceSection.Category);
                    _runtimeHierarchy.CreatePseudoScene(preferenceSection.Category);
                }
            }

            _hierarchyBroadcaster?.ForceScan();
        }

        private void ClearPreferenceSections()
        {
            if (_sectionPseudoSceneNames.Count > 0 && _runtimeHierarchy != null)
            {
                foreach (string pseudoSceneName in _sectionPseudoSceneNames)
                {
                    _runtimeHierarchy.DeletePseudoScene(pseudoSceneName);
                }
            }

            _sectionPseudoSceneNames.Clear();
            _sectionPseudoSceneLookup.Clear();
        }

        private void OnRuntimeHierarchySelectionChanged(ReadOnlyCollection<Transform> selection)
        {
            TrySelectPreferenceSectionFromSelection(selection);
        }

        private bool TrySelectPreferenceSectionFromSelection(ReadOnlyCollection<Transform> selection)
        {
            if (selection == null || selection.Count == 0)
            {
                return false;
            }

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                Transform selectedTransform = selection[i];

                if (selectedTransform == null)
                {
                    continue;
                }

                string sectionName = selectedTransform.name;

                if (!string.IsNullOrEmpty(sectionName) &&
                    _sectionPseudoSceneLookup.Contains(sectionName))
                {
                    OnPreferenceSectionSelected(sectionName);
                    return true;
                }
            }

            return false;
        }

        private void OnHierarchyDrawerClicked(HierarchyField drawer, PointerEventData eventData)
        {
            if (drawer == null || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var data = drawer.Data;

            if (data is HierarchyDataRootPseudoScene pseudoScene &&
                _sectionPseudoSceneLookup.Contains(pseudoScene.Name))
            {
                OnPreferenceSectionSelected(pseudoScene.Name);
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
