using System.Collections.Generic;
using System.Linq;
using static NativeMenuBar.Core.MenuBar;

namespace NativeMenuBar.Core.Editor
{
    public static class NativeMenuBarEditor
    {
        private static uint id = 0;

        private static List<MenuItemEditor> menuItemsEditor = new List<MenuItemEditor>();

        internal static void StartPlugin() 
        {
            // Nothing to do here
        }

        internal static uint AddMenuRoot(string menu) 
        {
            var item = new MenuItemEditor()
            {
                Id = id++,
                IsInteractable = true,
                IsToggled = false
            };
            menuItemsEditor.Add(item);
            return item.Id; 
        }

        internal static uint AddMenuItem(uint menuRoot, string menuItem, bool hasSubItem) 
        {
            var item = new MenuItemEditor()
            {
                Id = id++,
                IsInteractable = true,
                IsToggled = false
            };
            menuItemsEditor.Add(item);
            return item.Id;
        }

        internal static bool IsMenuItemSelected(uint menuId)
        {
            return menuItemsEditor.Single(item => item.Id == menuId).IsToggled;
        }

        internal static void SetMenuItemSelected(uint menuId, bool state)
        {
            menuItemsEditor.Single(item => item.Id == menuId).IsToggled = state;
        }

        internal static bool IsMenuItemEnabled(uint menuId)
        {
            return menuItemsEditor.Single(item => item.Id == menuId).IsInteractable;
        }

        internal static void SetMenuItemEnabled(uint menuId, bool state)
        {
            menuItemsEditor.Single(item => item.Id == menuId).IsInteractable = state;
        }

        internal static void SetMenuItemCallback(MenuItemClicked menuItemClickedCallback)
        {
            // Nothing to do here
        }

        internal static void BuildMenu() 
        { 
            // Nothing to do here
        }

        private class MenuItemEditor
        {
            public uint Id { get; set; }
            public bool IsInteractable { get; set; }
            public bool IsToggled { get; set; }
        }
    }
}