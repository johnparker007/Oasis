using System.Collections.Generic;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor.RuntimeHierarchyIntegration
{
    [DisallowMultipleComponent]
    public sealed class RuntimeHierarchyRightClickBroadcaster : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.25f;

        public event System.Action<HierarchyField, PointerEventData> DrawerRightClicked;

        private RuntimeHierarchy _runtimeHierarchy;
        private readonly HashSet<HierarchyField> _observedDrawers = new HashSet<HierarchyField>();
        private float _nextScanTime;

        private void Awake()
        {
            _runtimeHierarchy = GetComponent<RuntimeHierarchy>();
        }

        private void OnEnable()
        {
            ForceScan();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            ScanForDrawers();
        }

        public void ForceScan()
        {
            _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            ScanForDrawers();
        }

        private void ScanForDrawers()
        {
            if (_runtimeHierarchy == null)
            {
                return;
            }

            var drawers = _runtimeHierarchy.GetComponentsInChildren<HierarchyField>(true);

            for (int i = 0; i < drawers.Length; i++)
            {
                HierarchyField drawer = drawers[i];

                if (drawer == null || !drawer.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (_observedDrawers.Contains(drawer))
                {
                    continue;
                }

                AttachCatcher(drawer);
            }
        }

        private void AttachCatcher(HierarchyField drawer)
        {
            var catcher = drawer.GetComponent<RuntimeHierarchyRightClickCatcher>();

            if (catcher == null)
            {
                catcher = drawer.gameObject.AddComponent<RuntimeHierarchyRightClickCatcher>();
            }

            catcher.Configure(this, drawer);
            _observedDrawers.Add(drawer);
        }

        internal void NotifyRightClick(HierarchyField drawer, PointerEventData eventData)
        {
            DrawerRightClicked?.Invoke(drawer, eventData);
        }

        internal void Forget(HierarchyField drawer)
        {
            if (drawer != null)
            {
                _observedDrawers.Remove(drawer);
            }
        }
    }
}
