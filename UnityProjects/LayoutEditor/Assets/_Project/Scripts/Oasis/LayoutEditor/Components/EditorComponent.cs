using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.LayoutEditor
{
    public abstract class EditorComponent : MonoBehaviour
    {
        public Editor LayoutEditor
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

        public virtual void Initialise(Layout.Component component, Editor layoutEditor)
        {
            LayoutEditor = layoutEditor;
        }

        protected abstract void UpdateStateFromEmulation();
    }

}

