using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public abstract class ContextMenuItemBase : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI TextMeshProUGUI;

        public virtual void Initialise(string text)
        {
            TextMeshProUGUI.text = text;
        }
    }
}
