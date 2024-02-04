using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Layout
{
    // maybe extend to monobehaviour?  Then can inspect the Components objects in Unity Editor when running
    public abstract class Component : MonoBehaviour
    {
        // TODO this will prob want to be serialisable, only runtime stuff like link to
        // EditorCanvas component object should be non-serialisable properties
        public RectInt RectInt
        {
            get;
            set;
        }

    }

}
