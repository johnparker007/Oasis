using Oasis.UI.ContextMenu.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenuController : MonoBehaviour
    {
        public ContextMenu ContextMenuPrefab;
        public ContextMenuDefinitions ContextMenuDefinitions;

        private ContextMenu _contextMenu = null;

        // TEMP - HACK to test!
        private void Update()
        {
            if(UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                if(_contextMenu == null)
                {
                    CreateMenu();
                }
                else
                {
                    DestroyMenu();
                }
            }
        }

        public void CreateMenu()
        {
            _contextMenu = Instantiate(ContextMenuPrefab, transform);

            ContextMenuDefinition contextMenuDefinition = ContextMenuDefinitions.GetMenu("Emulation");
            foreach(ContextMenuDefinitionBase element in contextMenuDefinition.Elements)
            {
                if(element.GetType() == typeof(ContextMenuItemDefinition))
                {
                    _contextMenu.AddItem(((ContextMenuItemDefinition)element).DisplayText);
                }
                else if(element.GetType() == typeof(ContextMenuSeparatorDefinition))
                {
                    _contextMenu.AddSeparator();
                }

            }
        }

        public void DestroyMenu()
        {
            Destroy(_contextMenu.gameObject);
            _contextMenu = null;
        }


    }

}
