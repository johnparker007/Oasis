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

        public virtual void Initialise(Layout.Component component, Editor layoutEditor)
        {
            LayoutEditor = layoutEditor;
        }
    }

}

