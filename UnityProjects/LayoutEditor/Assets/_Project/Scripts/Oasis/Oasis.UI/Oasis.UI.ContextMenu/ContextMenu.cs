using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenu : MonoBehaviour
    {
        public ContextMenuItem ContextMenuItemPrefab;
        public GameObject ContextMenuSeparatorPrefab;

        private List<ContextMenuItem> _items = new List<ContextMenuItem>();

        public void AddItem(string text)
        {
            ContextMenuItem item = Instantiate(ContextMenuItemPrefab, transform);
            item.Initialise(text);

            _items.Add(item);
        }

        public void AddToggle(string text, bool state)
        {

        }

        public void AddSeparator()
        {
            Instantiate(ContextMenuSeparatorPrefab, transform);
        }
    }
}
