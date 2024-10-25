using Oasis.LayoutEditor;
using Oasis.LayoutEditor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
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
            public ViewQuad ViewQuad = new ViewQuad();
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

            // TODO - this is just a workaround hack for testing for now!!!
            const int kDEBUGLeft = 0;
            const int kDEBUGRight = 500;
            const int kDEBUGTop = 0;
            const int kDEBUGBottom = 500;

            Data.ViewQuad.Points[(int)ViewQuad.PointTypes.TopLeft] = new Vector2(kDEBUGLeft, kDEBUGTop);
            Data.ViewQuad.Points[(int)ViewQuad.PointTypes.TopRight] = new Vector2(kDEBUGRight, kDEBUGTop);
            Data.ViewQuad.Points[(int)ViewQuad.PointTypes.BottomLeft] = new Vector2(kDEBUGLeft, kDEBUGBottom);
            Data.ViewQuad.Points[(int)ViewQuad.PointTypes.BottomRight] = new Vector2(kDEBUGRight, kDEBUGBottom);


            // TOIMPROVE - need a better way of doing this rather than the View controlling the EditorView:
            EditorView.Initialise();
        }

        public void AddComponent(Component component, bool overlay = false)
        {
            Data.Components.Add(component);

            Editor.Instance.Project.Layout.OnAddComponent?.Invoke(component, this, overlay);
            OnChanged?.Invoke();
        }

        public void RemoveComponent(Component component)
        {
            Data.Components.Remove(component);

            Editor.Instance.Project.Layout.OnRemoveComponent?.Invoke(component, this);
            OnChanged?.Invoke();
        }

        public void RemoveComponents(List<Component> components)
        {
            foreach(Component component in components)
            {
                RemoveComponent(component);
            }
        }

        public void RemoveComponents(Component[] components)
        {
            RemoveComponents(components.ToList());
        }

        public void RemoveAllComponents()
        {
            RemoveComponents(Data.Components);
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


    }
}
