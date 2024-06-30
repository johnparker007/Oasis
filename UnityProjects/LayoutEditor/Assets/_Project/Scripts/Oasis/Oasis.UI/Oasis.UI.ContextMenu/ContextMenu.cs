using Oasis.UI.ContextMenu.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenu : MonoBehaviour
    {
        public ContextMenuDefinitions ContextMenuDefinitions;
        public ContextMenu ContextMenuPrefab;
        public ContextMenuItemCommand ContextMenuItemCommandPrefab;
        public ContextMenuItemToggle ContextMenuItemTogglePrefab;
        public ContextMenuItemLink ContextMenuItemLinkPrefab;
        public GameObject ContextMenuSeparatorPrefab;

        private List<ContextMenuItemBase> _items = new List<ContextMenuItemBase>();

        public void Initialise(string name)
        {
            ContextMenuDefinition contextMenuDefinition = ContextMenuDefinitions.GetMenu(name);
            foreach (ContextMenuDefinitionBase element in contextMenuDefinition.Elements)
            {
                if (element.GetType() == typeof(ContextMenuItemDefinition))
                {
                    AddItemCommand((ContextMenuItemDefinition)element);
                }
                else if (element.GetType() == typeof(ContextMenuSeparatorDefinition))
                {
                    AddSeparator();
                }
                else if (element.GetType() == typeof(ContextMenuDefinition))
                {
                    AddMenuLink((ContextMenuDefinition)element);
                }


            }
        }

        private void AddItemCommand(ContextMenuItemDefinition definition)
        {
            ContextMenuItemCommand item;
            if (definition.Toggle)
            {
                item = Instantiate(ContextMenuItemTogglePrefab, transform);
            }
            else
            {
                item = Instantiate(ContextMenuItemCommandPrefab, transform);
            }

            item.Initialise(definition.DisplayText);

            _items.Add(item);
        }

        public void AddMenuLink(ContextMenuDefinition definition)
        {
            ContextMenuItemLink item = Instantiate(ContextMenuItemLinkPrefab, transform);
            // TODO not quite right, menu's need to have Display Text field for when they are a link
            item.Initialise(definition.Name);

            _items.Add(item);
        }

        public void AddSeparator()
        {
            Instantiate(ContextMenuSeparatorPrefab, transform);
        }
    }
}
