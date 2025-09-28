using System.Reflection;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Oasis.LayoutEditor.RuntimeHierarchyIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HierarchyField))]
    public sealed class RuntimeHierarchyRightClickCatcher : MonoBehaviour
    {
        private static readonly FieldInfo ClickListenerField = typeof(HierarchyField).GetField(
            "clickListener",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private RuntimeHierarchyRightClickBroadcaster _broadcaster;
        private HierarchyField _drawer;
        private PointerEventListener _clickListener;

        public HierarchyField Drawer => _drawer;

        internal void Configure(RuntimeHierarchyRightClickBroadcaster broadcaster, HierarchyField drawer)
        {
            if (broadcaster == null || drawer == null)
            {
                return;
            }

            if (_broadcaster == broadcaster && _drawer == drawer && _clickListener != null)
            {
                return;
            }

            Detach();

            _broadcaster = broadcaster;
            _drawer = drawer;

            Attach();
        }

        private void OnEnable()
        {
            Attach();
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
            _broadcaster?.Forget(_drawer);
            _broadcaster = null;
            _drawer = null;
        }

        private void Attach()
        {
            if (_clickListener != null)
            {
                return;
            }

            if (_drawer == null)
            {
                _drawer = GetComponent<HierarchyField>();
                if (_drawer == null)
                {
                    return;
                }
            }

            if (ClickListenerField == null)
            {
                return;
            }

            _clickListener = ClickListenerField.GetValue(_drawer) as PointerEventListener;

            if (_clickListener != null)
            {
                _clickListener.PointerClick += HandlePointerClick;
            }
        }

        private void Detach()
        {
            if (_clickListener == null)
            {
                return;
            }

            _clickListener.PointerClick -= HandlePointerClick;
            _clickListener = null;
        }

        private void HandlePointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            _broadcaster?.NotifyRightClick(_drawer, eventData);
        }
    }
}
