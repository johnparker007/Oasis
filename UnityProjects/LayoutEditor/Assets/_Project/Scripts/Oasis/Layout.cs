using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Oasis.Layout;
using Component = Oasis.Layout.Component;

namespace Oasis
{
    public class LayoutObject : MonoBehaviour
    {
        public UnityEvent OnChanged = new UnityEvent();
        public UnityEvent OnDirty = new UnityEvent();

        public List<Component> Components = new List<Component>();

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

            OnChanged?.Invoke();
        }
    }
}
