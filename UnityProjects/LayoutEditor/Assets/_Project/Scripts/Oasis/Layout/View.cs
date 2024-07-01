using Oasis.LayoutEditor;
using Oasis.LayoutEditor.Tools;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;


namespace Oasis.Layout
{
    public class View : MonoBehaviour
    {
        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class ViewData
        {
            public string Name;
            public List<Component> Components = new List<Component>();
        }

        public ViewData Data = new ViewData();

        private Editor _layoutEditor = null;

        public string Name
        {
            get
            {
                return Data.Name;
            }
            set
            {
                Data.Name = gameObject.name = value;
            }
        }

        public void Initialise(string name, Editor editor)
        {
            Name = name;
            _layoutEditor = editor;
        }

        public void AddComponent(Component component, bool overlay = false)
        {
            Data.Components.Add(component);

            EditorComponent editorComponent = null;
            if (component.GetType() == typeof(ComponentBackground))
            {
                EditorComponentBackground editorComponentBackground = Instantiate(
                    _layoutEditor.EditorComponentBackgroundPrefab,
                    _layoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentBackground;

                editorComponentBackground.Initialise((ComponentBackground)component, _layoutEditor);


                // JP quick hack for now:
                RectTransform editorCanvasRectTransform = _layoutEditor.UIController.EditorCanvasGameObject.GetComponent<RectTransform>();
                editorCanvasRectTransform.sizeDelta = new Vector2(component.Size.x, component.Size.y);

            }
            else if (component.GetType() == typeof(ComponentLamp))
            {
                EditorComponentLamp editorComponentLamp = Instantiate(
                    _layoutEditor.EditorComponentLampPrefab,
                    _layoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentLamp;

                editorComponentLamp.Initialise((ComponentLamp)component, _layoutEditor);
            }
            else if (component.GetType() == typeof(ComponentReel))
            {
                ComponentReel componentReel = (ComponentReel)component;
                if (overlay)
                {
                    EditorComponentOverlay editorComponentOverlay = Instantiate(
                        _layoutEditor.EditorComponentOverlayPrefab,
                        _layoutEditor.UIController.EditorCanvasGameObject.transform);

                    editorComponent = editorComponentOverlay;

                    editorComponentOverlay.Initialise(componentReel, _layoutEditor);
                }
                else
                {
                    EditorComponentReel editorComponentReel = Instantiate(
                        _layoutEditor.EditorComponentReelPrefab,
                        _layoutEditor.UIController.EditorCanvasGameObject.transform);

                    editorComponent = editorComponentReel;

                    editorComponentReel.Initialise(componentReel, _layoutEditor);

                    if (componentReel.OverlayOasisImage != null)
                    {
                        AddComponent(component, true);
                    }
                }
            }
            else if (component.GetType() == typeof(Component7Segment))
            {
                EditorComponent7Segment editorComponent7Segment = Instantiate(
                    _layoutEditor.EditorComponentSevenSegmentPrefab,
                    _layoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponent7Segment;

                editorComponent7Segment.Initialise((Component7Segment)component, _layoutEditor);
            }
            else if (component.GetType() == typeof(ComponentAlpha))
            {
                EditorComponentAlpha editorComponentAlpha = Instantiate(
                    _layoutEditor.EditorComponentAlphaPrefab,
                    _layoutEditor.UIController.EditorCanvasGameObject.transform);

                editorComponent = editorComponentAlpha;

                editorComponentAlpha.Initialise((ComponentAlpha)component, _layoutEditor);
            }

            if (editorComponent != null)
            {
                //LayoutEditor.UIController.RuntimeHierarchy.AddToPseudoScene(
                //    editorComponent.HierarchyPseudoSceneName, editorComponent.transform);

                //editorComponent.gameObject.name = editorComponent.HierarchyName;

                _layoutEditor.UIController.RuntimeHierarchy.AddToPseudoScene(
                    editorComponent.HierarchyPseudoSceneName, component.transform);

                component.gameObject.name = editorComponent.HierarchyName;
            }

            _layoutEditor.Layout.OnAddComponent?.Invoke(component, this);
            OnChanged?.Invoke();
        }

        public void RemapLamps(string[] mfmeLampTable, string[] mameLampTable)
        {
            Mpu4LampRemapper lampRemapper = new Mpu4LampRemapper(mfmeLampTable, mameLampTable);

            foreach (Component component in Data.Components)
            {
                // TODO buttons, leds as lamps, any other lamp driven components
                if (component.GetType() != typeof(ComponentLamp))
                {
                    continue;
                }

                ComponentLamp componentLamp = (ComponentLamp)component;

                // TODO I think only lamps 0-127 are scrambled
                componentLamp.Number = lampRemapper.GetRemappedLampNumber(componentLamp.Number);
            }
        }
    }
}
