using Oasis.UI.ContextMenu.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenuController : MonoBehaviour
    {
        public ContextMenu ContextMenuPrefab;

        private ContextMenu _contextMenu = null;

        // TEMP - HACK to test!
        private void Update()
        {
            if(UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                if(_contextMenu == null)
                {
                    CreateMenu("Tab");
                    //CreateMenu("Emulation");
                }
                else
                {
                    DestroyMenu();
                }
            }
        }

        public void CreateMenu(string name)
        {
            _contextMenu = Instantiate(ContextMenuPrefab, transform);
            _contextMenu.Initialise(name);
        }

        public void DestroyMenu()
        {
            Destroy(_contextMenu.gameObject);
            _contextMenu = null;
        }


    }

}
