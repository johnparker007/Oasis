using DynamicPanels;
using Oasis.UI;
using Oasis.UI.ContextMenu;
using RuntimeInspectorNamespace;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis
{
    public class UIController : MonoBehaviour
    {
        public GameObject EditorCanvasGameObject;
        public RuntimeHierarchy RuntimeHierarchy;
        public DynamicPanelsCanvas DynamicPanelsCanvas;
        public ContextMenuController ContextMenuController;


        private void Start()
        {
            RuntimeHierarchy.ConnectedInspector.ComponentFilter += InspectorComponentFilter;
        }

        private void OnDestroy()
        {
            RuntimeHierarchy.ConnectedInspector.ComponentFilter -= InspectorComponentFilter;
        }

        private void InspectorComponentFilter(GameObject gameObject, List<Component> components)
        {
            // JP strip out Transform as we don't want that shown on any of our LayoutEditor objects
            components.RemoveAll(x => x.GetType() == typeof(Transform));
        }
    }
}

