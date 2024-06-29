using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenuItem : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI TextMeshProUGUI;

        public void Initialise(string text)
        {
            TextMeshProUGUI.text = text;
        }
    }
}
