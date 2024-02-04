using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Oasis.Layout;
using Oasis.LayoutEditor;

using Component = Oasis.Layout.Component;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;

namespace Oasis
{
    public class LayoutObject : MonoBehaviour
    {
        public UnityEvent OnChanged = new UnityEvent();
        public UnityEvent OnDirty = new UnityEvent();
        public UnityEvent<Component> OnAddComponent = new UnityEvent<Component>();

        public List<Component> Components = new List<Component>();
        public List<EditorComponent> EditorComponents = new List<EditorComponent>();

        public Editor LayoutEditor
        {
            get;
            set;
        }

        //private bool _changed = false;
        //private bool _dirty = false;

        public bool Dirty
        {
            get;
            set;
        }

        public void AddComponent(Component component)
        {
            Components.Add(component);

            EditorComponent editorComponent = null;
            if (component.GetType() == typeof(ComponentBackground))
            {
                EditorComponentBackground editorComponentBackground = Instantiate(
                    LayoutEditor.EditorComponentBackgroundPrefab, 
                    LayoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentBackground;

                editorComponentBackground.Initialise((ComponentBackground)component, LayoutEditor);
            }
            else if (component.GetType() == typeof(ComponentLamp))
            {
                EditorComponentLamp editorComponentLamp = Instantiate(
                    LayoutEditor.EditorComponentLampPrefab,
                    LayoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentLamp;

                editorComponentLamp.Initialise((ComponentLamp)component, LayoutEditor);
            }
            else if (component.GetType() == typeof(ComponentReel))
            {
                EditorComponentReel editorComponentReel = Instantiate(
                    LayoutEditor.EditorComponentReelPrefab,
                    LayoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentReel;

                editorComponentReel.Initialise((ComponentReel)component, LayoutEditor);
            }
            else if (component.GetType() == typeof(Component7Segment))
            {
                EditorComponent7Segment editorComponent7Segment = Instantiate(
                    LayoutEditor.EditorComponentSevenSegmentPrefab,
                    LayoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponent7Segment;

                editorComponent7Segment.Initialise((Component7Segment)component, LayoutEditor);
            }
            else if (component.GetType() == typeof(ComponentAlpha))
            {
                EditorComponentAlpha editorComponentAlpha = Instantiate(
                    LayoutEditor.EditorComponentAlphaPrefab,
                    LayoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentAlpha;

                editorComponentAlpha.Initialise((ComponentAlpha)component, LayoutEditor);
            }

            if(editorComponent != null)
            {
                //LayoutEditor.UIController.RuntimeHierarchy.AddToPseudoScene(
                //    editorComponent.HierarchyPseudoSceneName, editorComponent.transform);

                //editorComponent.gameObject.name = editorComponent.HierarchyName;

                LayoutEditor.UIController.RuntimeHierarchy.AddToPseudoScene(
                    editorComponent.HierarchyPseudoSceneName, component.transform);

                component.gameObject.name = editorComponent.HierarchyName;
            }

            OnAddComponent?.Invoke(component);
            OnChanged?.Invoke();
        }
    }
}
