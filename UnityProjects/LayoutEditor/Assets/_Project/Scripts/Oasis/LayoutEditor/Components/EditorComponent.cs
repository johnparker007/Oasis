using Oasis.MAME;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent : MonoBehaviour
    {
        public Layout.Component Component
        {
            get;
            private set;
        }

        protected MameController MameController
        {
            get
            {
                return Editor.Instance.MameController;
            }
        }

        protected virtual void Awake()
        {
            Editor.Instance.OnDisplayTextSet.AddListener(OnDisplayTextSet);
        }

        protected virtual void Start()
        {
            ShowDisplayElements(Editor.Instance.DisplayText);
        }

        // Ideally don't override this in derived EditorComponents, so that blocking logic due
        // to Emulation not running holds true:
        protected void LateUpdate()
        {
            // we call this in LateUpdate, as from stepping through desktop recordings, this brings the latency
            // between lamps on the internal MAME layout and the Unity rendered Lamps etc to zero frames (perfectly
            // in sync):
            if (Editor.Instance != null && Editor.Instance.MameController.Process != null)
            {
                UpdateStateFromEmulation();
            }
        }

        protected virtual void OnDestroy()
        {
            if (Component != null)
            {
                Component.OnValueSet -= OnComponentValueSet;
            }

            Editor.Instance.OnDisplayTextSet.RemoveListener(OnDisplayTextSet);
        }

        public virtual void Initialise(Layout.Component component)
        {
            //Prevent null pointer exception
            if (component == null) {
                return;
            }

            Component = component;

            Component.OnValueSet += OnComponentValueSet;

            Refresh();
        }

        protected abstract void UpdateStateFromEmulation();

        protected virtual void Refresh()
        {
        }

        protected virtual void ShowDisplayElements(bool forceText)
        {
        }

        protected virtual void OnComponentValueSet(Layout.Component component)
        {
            Refresh();
        }

        protected virtual void OnDisplayTextSet(bool enabled)
        {
            ShowDisplayElements(enabled);
        }
    }

}

