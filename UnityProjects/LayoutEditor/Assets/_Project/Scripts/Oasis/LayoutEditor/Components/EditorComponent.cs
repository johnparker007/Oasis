using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent : MonoBehaviour
    {
        public abstract string HierarchyPseudoSceneName
        {
            get;
        }

        public abstract string HierarchyName
        {
            get;
        }

        public Editor LayoutEditor
        {
            get;
            protected set;
        }

        public Layout.Component Component
        {
            get;
            private set;
        }

        protected virtual void Awake()
        {
        }

        protected virtual void LateUpdate()
        {
            // we call this in LateUpdate, as from stepping through desktop recordings, this brings the latency
            // between lamps on the internal MAME layout and the Unity rendered Lamps etc to zero frames (perfectly
            // in sync):
            UpdateStateFromEmulation();
        }

        protected virtual void OnDestroy()
        {
            Component.OnValueSet -= OnComponentValueSet;
        }

        public virtual void Initialise(Layout.Component component, Editor layoutEditor)
        {
            Component = component;
            LayoutEditor = layoutEditor;

            Component.OnValueSet += OnComponentValueSet;

            Refresh();
        }

        protected abstract void UpdateStateFromEmulation();

        protected virtual void Refresh()
        {
        }

        protected virtual void OnComponentValueSet(Component component)
        {
            Debug.LogError("value set on component " + component);
            Refresh();
        }
    }

}

