using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu
{
    public class ContextMenuController : MonoBehaviour
    {
        public ContextMenu ContextMenuPrefab;

        public static ContextMenuController Instance = null;

        private ContextMenu _contextMenu = null;

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (this != Instance)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

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
            _contextMenu.AddItem("Test Normal");
            _contextMenu.AddItem("Test Normal 2");
            _contextMenu.AddToggle("Test Toggle", true);
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Test Normal 3");
            _contextMenu.AddItem("Test Normal 4");
            _contextMenu.AddItem("Test Normal 5");
        }

        public void DestroyMenu()
        {
            Destroy(_contextMenu.gameObject);
            _contextMenu = null;
        }


    }

}
