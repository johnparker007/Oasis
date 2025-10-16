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
    public class View : SerializableDictionary
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
            List<Dictionary<string, object>> viewQuadRepresentations = new List<Dictionary<string, object>>();

            IReadOnlyList<ViewQuad> viewQuads = Data.ViewQuads;
            if (viewQuads != null)
            {
                foreach (ViewQuad viewQuad in viewQuads)
                {
                    if (viewQuad == null)
                    {
                        continue;
                    }

                    viewQuadRepresentations.Add(CreateViewQuadRepresentation(viewQuad));
                }
            }

            representation["view_quads"] = viewQuadRepresentations;

            int activeIndex = Data.ActiveViewQuadIndex;
            if (viewQuadRepresentations.Count == 0)
            {
                activeIndex = -1;
            }
            else if (activeIndex < 0 || activeIndex >= viewQuadRepresentations.Count)
            {
                activeIndex = Mathf.Clamp(activeIndex, 0, viewQuadRepresentations.Count - 1);
            }

            representation["active_view_quad_index"] = activeIndex;

            return representation;
        }

        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class ViewData
        {
            public string Name;
            public List<ViewQuad> ViewQuads = new List<ViewQuad>();
            public int ActiveViewQuadIndex = -1;
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
                Data.Name = value;
            }
        }

        public EditorView EditorView
        {
            get
            {
                return ViewController.GetEditorView(Name);
            }
        }

        public IReadOnlyList<ViewQuad> ViewQuads => Data.ViewQuads;

        public ViewQuad ActiveViewQuad
        {
            get
            {
                int index = Data.ActiveViewQuadIndex;
                if (index < 0 || index >= Data.ViewQuads.Count)
                {
                    return null;
                }

                return Data.ViewQuads[index];
            }
        }

        public bool HasViewQuad => ActiveViewQuad != null;

        public ViewQuad EnsureViewQuad()
        {
            if (Data.ViewQuads.Count == 0)
            {
                Data.ViewQuads.Add(new ViewQuad());
                Data.ActiveViewQuadIndex = 0;
            }
            else if (Data.ActiveViewQuadIndex < 0 || Data.ActiveViewQuadIndex >= Data.ViewQuads.Count)
            {
                Data.ActiveViewQuadIndex = 0;
            }

            return Data.ViewQuads[Data.ActiveViewQuadIndex];
        }

        public ViewQuad AddViewQuad()
        {
            ViewQuad newViewQuad = new ViewQuad();
            Data.ViewQuads.Add(newViewQuad);
            Data.ActiveViewQuadIndex = Data.ViewQuads.Count - 1;
            return newViewQuad;
        }

        public bool TrySetActiveViewQuad(ViewQuad viewQuad)
        {
            if (viewQuad == null)
            {
                if (Data.ViewQuads.Count == 0)
                {
                    return false;
                }

                if (Data.ActiveViewQuadIndex != 0)
                {
                    Data.ActiveViewQuadIndex = 0;
                    OnChanged?.Invoke();
                }

                return true;
            }

            int index = Data.ViewQuads.IndexOf(viewQuad);
            if (index < 0)
            {
                return false;
            }

            if (Data.ActiveViewQuadIndex == index)
            {
                return true;
            }

            Data.ActiveViewQuadIndex = index;
            OnChanged?.Invoke();
            return true;
        }

        public void Initialise(string name)
        {
            Name = name;

            // TOIMPROVE - need a better way of doing this rather than the View controlling the EditorView:
            EditorView editorView = EditorView;
            if (editorView == null)
            {
                return;
            }

            editorView.Initialise();
        }

        public void SetViewQuadRectangle(float top, float left, float bottom, float right)
        {
            ViewQuad viewQuad = EnsureViewQuad();
            viewQuad.Points[(int)ViewQuad.PointTypes.TopLeft] = new Vector2(left, top);
            viewQuad.Points[(int)ViewQuad.PointTypes.TopRight] = new Vector2(right, top);
            viewQuad.Points[(int)ViewQuad.PointTypes.BottomRight] = new Vector2(right, bottom);
            viewQuad.Points[(int)ViewQuad.PointTypes.BottomLeft] = new Vector2(left, bottom);
        }

        public void AddComponent(Component component)
        {
            Data.Components.Add(component);

            Editor.Instance.Project.Layout.OnAddComponent?.Invoke(component, this);
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

        private static Dictionary<string, object> CreateViewQuadRepresentation(ViewQuad viewQuad)
        {
            Vector2[] points = viewQuad?.Points;
            int pointCount = Enum.GetValues(typeof(ViewQuad.PointTypes)).Length;
            if (points == null || points.Length < pointCount)
            {
                points = new Vector2[pointCount];
            }

            return new Dictionary<string, object>
            {
                { "name", viewQuad.Name ?? string.Empty },
                { "top_left", CreatePointRepresentation(points[(int)ViewQuad.PointTypes.TopLeft]) },
                { "top_right", CreatePointRepresentation(points[(int)ViewQuad.PointTypes.TopRight]) },
                { "bottom_right", CreatePointRepresentation(points[(int)ViewQuad.PointTypes.BottomRight]) },
                { "bottom_left", CreatePointRepresentation(points[(int)ViewQuad.PointTypes.BottomLeft]) }
            };
        }

        private static Dictionary<string, float> CreatePointRepresentation(Vector2 point)
        {
            return new Dictionary<string, float>
            {
                { "x", point.x },
                { "y", point.y }
            };
        }


    }
}
