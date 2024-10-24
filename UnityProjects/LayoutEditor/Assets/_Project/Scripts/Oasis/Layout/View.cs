using Oasis.LayoutEditor;
using Oasis.LayoutEditor.Tools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;


namespace Oasis.Layout
{
    public class View : MonoBehaviour, SerializableDictionary
    {
        public void SetRepresentation(Dictionary<string, object> representation)
        {
            if ((string)representation["type"] != this.GetType().Name)
            {
                return;
            }
            // TODO: Rehydrate views/components

        }

        public Dictionary<string, object> GetRepresentation()
        {
            Dictionary<string, object> representation = new Dictionary<string, object>();
            representation["type"] = GetType().Name;
            representation["items"] = new Dictionary<string, object>();
            representation["unknown"] = new List<Dictionary<string, object>>();
            foreach (SerializableDictionary component in Data.Components)
            {
                Dictionary<string, object> componentData = component.GetRepresentation();
                string componentKey = Component.GetComponentKey(componentData);
                if (componentKey != "")
                {
                    ((Dictionary<string, object>)representation["items"])[componentKey] = component.GetRepresentation();
                    continue;
                }
                ((List<Dictionary<string, object>>)representation["unknown"]).Add(component.GetRepresentation());
            }
            return representation;
        }

        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class ViewData
        {
            public string Name;
            public List<Component> Components = new List<Component>();
        }

        public ViewData Data = new ViewData();

        public UnityEvent OnChanged = new UnityEvent();

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

        public EditorView EditorView
        {
            get
            {
                return ViewController.GetEditorView(Name);
            }
        }

        public void Initialise(string name)
        {
            Name = name;
        }

        public void AddComponent(Component component, bool overlay = false)
        {
            Data.Components.Add(component);

            EditorComponent editorComponent = null;
            if (component.GetType() == typeof(ComponentBackground))
            {
                editorComponent = AddComponentBackground((ComponentBackground)component);
            }
            else if (component.GetType() == typeof(ComponentLamp))
            {
                editorComponent = AddComponentLamp((ComponentLamp)component);
            }
            else if (component.GetType() == typeof(ComponentReel))
            {
                editorComponent = AddComponentReel((ComponentReel)component, overlay);
            }
            else if (component.GetType() == typeof(Component7Segment))
            {
                editorComponent = AddComponent7Segment((Component7Segment)component);
            }
            else if (component.GetType() == typeof(ComponentAlpha))
            {
                editorComponent = AddComponentAlpha((ComponentAlpha)component);
            }

            // TODO this will most/all be removed fully when the new Hierarchy implementation is done
            if (editorComponent != null)
            {
                //LayoutEditor.UIController.RuntimeHierarchy.AddToPseudoScene(
                //    editorComponent.HierarchyPseudoSceneName, editorComponent.transform);

                //editorComponent.gameObject.name = editorComponent.HierarchyName;

                
                //Editor.Instance.UIController.RuntimeHierarchy.AddToPseudoScene(
                //    editorComponent.HierarchyPseudoSceneName, component.transform);

                component.gameObject.name = editorComponent.HierarchyName;
            }

            Editor.Instance.Project.Layout.OnAddComponent?.Invoke(component, this);
            OnChanged?.Invoke();
        }

        public Component GetComponentByGuid(string guid)
        {
            return Data.Components.Find(x => x.Guid == guid);
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
                if (!componentLamp.Number.HasValue)
                {
                    continue;
                }

                // TODO I think only lamps 0-127 are scrambled
                componentLamp.Number = lampRemapper.GetRemappedLampNumber((int)componentLamp.Number);
            }
        }

        private EditorComponent AddComponentBackground(ComponentBackground component)
        {
            EditorComponentBackground editorComponentBackground = Instantiate(
                Editor.Instance.EditorComponentBackgroundPrefab,
                //Editor.Instance.UIController.EditorCanvasGameObject.transform);
                EditorView.GraphicRaycaster.transform);

            editorComponentBackground.Initialise(component);

            // JP quick hack for now:
            RectTransform editorCanvasRectTransform = EditorView.GraphicRaycaster.GetComponent<RectTransform>();
            editorCanvasRectTransform.sizeDelta = new Vector2(component.Size.x, component.Size.y);

            return editorComponentBackground;
        }

        private EditorComponent AddComponentLamp(ComponentLamp component)
        {
            EditorComponentLamp editorComponentLamp = Instantiate(
                Editor.Instance.EditorComponentLampPrefab,
                EditorView.GraphicRaycaster.transform);

            editorComponentLamp.Initialise(component);

            return editorComponentLamp;
        }

        private EditorComponent AddComponentReel(ComponentReel component, bool overlay)
        {
            EditorComponent editorComponent;
            if (overlay)
            {
                EditorComponentOverlay editorComponentOverlay = Instantiate(
                    Editor.Instance.EditorComponentOverlayPrefab,
                    EditorView.GraphicRaycaster.transform);

                editorComponent = editorComponentOverlay;

                editorComponentOverlay.Initialise(component);
            }
            else
            {
                EditorComponentReel editorComponentReel = Instantiate(
                    Editor.Instance.EditorComponentReelPrefab,
                    EditorView.GraphicRaycaster.transform);

                editorComponent = editorComponentReel;

                editorComponentReel.Initialise(component);

                if (component.OverlayOasisImage != null)
                {
                    AddComponent(component, true);
                }
            }

            return editorComponent;
        }

        private EditorComponent AddComponent7Segment(Component7Segment component)
        {
            EditorComponent7Segment editorComponent7Segment = Instantiate(
                Editor.Instance.EditorComponentSevenSegmentPrefab,
                EditorView.GraphicRaycaster.transform);

            editorComponent7Segment.Initialise(component);

            return editorComponent7Segment;
        }

        private EditorComponent AddComponentAlpha(ComponentAlpha component)
        {
            // TODO kinda hacky for now, until decided how this is going to work wrt design:
            // TOIMPROVE - I think it's probvably better to have a single EditorComponentAlpha,
            // that can be set into one of the four 'modes'; 14, 14+semicolon, 16, 16+semecolon
            // and theoretically changed on the fly - more versatile
            EditorComponent editorComponent;
            switch (Editor.Instance.Project.Settings.FruitMachine.Platform)
            {
                case MAME.MameController.PlatformType.Scorpion4:
                    EditorComponentAlpha14 editorComponentAlpha14 = Instantiate(
                        Editor.Instance.EditorComponentAlpha14Prefab,
                        EditorView.GraphicRaycaster.transform);

                    editorComponent = editorComponentAlpha14;

                    editorComponentAlpha14.Initialise(component);
                    break;
                case MAME.MameController.PlatformType.MPU4:
                default:
                    EditorComponentAlpha editorComponentAlpha16 = Instantiate(
                        Editor.Instance.EditorComponentAlphaPrefab,
                        EditorView.GraphicRaycaster.transform);

                    editorComponent = editorComponentAlpha16;

                    editorComponentAlpha16.Initialise(component);
                    break;
            }

            return editorComponent;
        }

    }
}
