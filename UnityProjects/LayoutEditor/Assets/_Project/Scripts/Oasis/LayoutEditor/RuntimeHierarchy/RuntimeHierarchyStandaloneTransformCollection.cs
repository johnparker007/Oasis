using RuntimeInspectorNamespace;
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

        private static readonly MethodInfo SetListViewDirtyMethod = typeof(RuntimeHierarchy).GetMethod(
            "SetListViewDirty",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly RuntimeHierarchy _runtimeHierarchy;
        private readonly List<Entry> _entries = new List<Entry>();

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

            if (SceneDataField == null || SearchSceneDataField == null)
            {
                return;
            }

            if (_entries.Exists(entry => entry.Transform == transform))
            {
                return;
            }

            HierarchyDataRootStandaloneTransform rootData = new HierarchyDataRootStandaloneTransform(_runtimeHierarchy, transform);
            HierarchyDataRootSearch searchData = new HierarchyDataRootSearch(_runtimeHierarchy, rootData)
            {
                IsExpanded = true
            };

            List<HierarchyDataRoot> sceneData = SceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;
            List<HierarchyDataRoot> searchSceneData = SearchSceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;

            if (sceneData == null || searchSceneData == null)
            {
                return;
            }

            sceneData.Add(rootData);
            searchSceneData.Add(searchData);

            _entries.Add(new Entry(transform, rootData, searchData));

            rootData.IsExpanded = true;

            MarkHierarchyDirty();
        }

        public void Remove(Transform transform)
        {
            if (_runtimeHierarchy == null || transform == null)
            {
                return;
            }

            int entryIndex = _entries.FindIndex(entry => entry.Transform == transform);

            if (entryIndex < 0)
            {
                return;
            }

            Entry entry = _entries[entryIndex];
            RemoveEntry(entry);
            _entries.RemoveAt(entryIndex);

            MarkHierarchyDirty();
        }

        public void Clear()
        {
            if (_runtimeHierarchy == null || _entries.Count == 0)
            {
                return;
            }

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                RemoveEntry(_entries[i]);
            }

            _entries.Clear();
            MarkHierarchyDirty();
        }

        private void RemoveEntry(Entry entry)
        {
            if (SceneDataField == null || SearchSceneDataField == null)
            {
                return;
            }

            List<HierarchyDataRoot> sceneData = SceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;
            List<HierarchyDataRoot> searchSceneData = SearchSceneDataField.GetValue(_runtimeHierarchy) as List<HierarchyDataRoot>;

            if (sceneData != null)
            {
                sceneData.Remove(entry.RootData);
            }

            if (searchSceneData != null)
            {
                searchSceneData.Remove(entry.SearchData);
            }
        }

        private void MarkHierarchyDirty()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            if (SetListViewDirtyMethod != null)
            {
                SetListViewDirtyMethod.Invoke(_runtimeHierarchy, null);
            }
        }

        private readonly struct Entry
        {
            public Entry(Transform transform, HierarchyDataRootStandaloneTransform rootData, HierarchyDataRootSearch searchData)
            {
                Transform = transform;
                RootData = rootData;
                SearchData = searchData;
            }

            public Transform Transform { get; }
            public HierarchyDataRootStandaloneTransform RootData { get; }
            public HierarchyDataRootSearch SearchData { get; }
        }

        private sealed class HierarchyDataRootStandaloneTransform : HierarchyDataRootPseudoScene
        {
            private readonly Transform _transform;

            public HierarchyDataRootStandaloneTransform(RuntimeHierarchy hierarchy, Transform transform)
                : base(hierarchy, string.Empty)
            {
                _transform = transform;
            }

            public override string Name => _transform ? _transform.name : "<missing>";

            public override int ChildCount => _transform ? _transform.childCount : 0;

            public override Transform BoundTransform => _transform;

            public override bool IsActive => _transform ? _transform.gameObject.activeInHierarchy : false;

            public override void RefreshContent()
            {
            }

            public override bool Refresh()
            {
                m_depth = 0;

                bool changed = base.Refresh();

                if (_transform == null)
                {
                    m_height = 0;
                    m_depth = -1;
                }

                return changed;
            }

            public override Transform GetChild(int index)
            {
                return _transform ? _transform.GetChild(index) : null;
            }

            public override Transform GetNearestRootOf(Transform target)
            {
                if (!_transform || target == null)
                {
                    return null;
                }

                if (ReferenceEquals(target, _transform) || target.IsChildOf(_transform))
                {
                    return _transform;
                }

                return null;
            }
        }
    }
}
