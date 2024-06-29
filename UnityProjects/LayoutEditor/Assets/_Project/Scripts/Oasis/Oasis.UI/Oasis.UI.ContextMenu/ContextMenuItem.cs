using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenuItem : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI TextMeshProUGUI;

        public bool SubMenuLink
        {
            get;
            private set;
        } = false;

        public bool Toggle
        {
            get;
            private set;
        } = false;

        public void Initialise(string text)
        {
            TextMeshProUGUI.text = text;
            //SubMenuLink = subMenuLink;
            //Toggle = toggle;
        }
    }
}
