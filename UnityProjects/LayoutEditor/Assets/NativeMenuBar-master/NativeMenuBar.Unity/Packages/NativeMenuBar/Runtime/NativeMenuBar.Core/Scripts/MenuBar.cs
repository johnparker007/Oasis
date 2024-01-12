using AOT;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

[assembly: InternalsVisibleTo("NativeMenuBar.Editor")]

namespace NativeMenuBar.Core
{
    public class MenuBar : MonoBehaviour
    {
        #region DLL Import
#if UNITY_EDITOR
        internal static void StartPlugin() => Editor.NativeMenuBarEditor.StartPlugin();
        internal static uint AddMenuRoot(string menu) => Editor.NativeMenuBarEditor.AddMenuRoot(menu);
        internal static uint AddMenuItem(uint menuRoot, string menuItem, bool hasSubItem) => Editor.NativeMenuBarEditor.AddMenuItem(menuRoot, menuItem, hasSubItem);
        internal static bool IsMenuItemSelected(uint menuId) => Editor.NativeMenuBarEditor.IsMenuItemSelected(menuId);
        internal static void SetMenuItemSelected(uint menuId, bool state) => Editor.NativeMenuBarEditor.SetMenuItemSelected(menuId, state);
        internal static bool IsMenuItemEnabled(uint menuId) => Editor.NativeMenuBarEditor.IsMenuItemEnabled(menuId);
        internal static void SetMenuItemEnabled(uint menuId, bool state) => Editor.NativeMenuBarEditor.SetMenuItemEnabled(menuId, state);
        internal static void SetMenuItemCallback(MenuItemClicked menuItemClickedCallback) => Editor.NativeMenuBarEditor.SetMenuItemCallback(menuItemClickedCallback);
        internal static void BuildMenu() => Editor.NativeMenuBarEditor.BuildMenu();

#elif UNITY_STANDALONE_WIN
        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void StartPlugin();

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint AddMenuRoot(string menu);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint AddMenuItem(uint menuRoot, string menuItem, bool hasSubItem);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool IsMenuItemSelected(uint menuId);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SetMenuItemSelected(uint menuId, bool state);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool IsMenuItemEnabled(uint menuId);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SetMenuItemEnabled(uint menuId, bool state);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SetMenuItemCallback([MarshalAs(UnmanagedType.FunctionPtr)] MenuItemClicked menuItemClickedCallback);

        [DllImport("NativeMenuBar.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BuildMenu();
#elif UNITY_STANDALONE_OSX
// TODO
#elif UNITY_STANDALONE_LINUX
// TODO
#else
        // No implementation as fallback
        internal static void StartPlugin() { }
        internal static uint AddMenuRoot(string menu) { return default; }
        internal static uint AddMenuItem(uint menuRoot, string menuItem, bool hasSubItem) { return default; }
        internal static bool IsMenuItemSelected(uint menuId) { return default; }
        internal static void SetMenuItemSelected(uint menuId, bool state) { }
        internal static bool IsMenuItemEnabled(uint menuId) { return default; }
        internal static void SetMenuItemEnabled(uint menuId, bool state) { }
        internal static void SetMenuItemCallback(MenuItemClicked menuItemClickedCallback) { }
        internal static void BuildMenu() { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MenuItemClicked(uint menuItemId);
        #endregion

        [SerializeField]
        private List<MenuRootItem> menuRootItems = default;

        public MenuRootItem[] MenuRoots => menuRootItems.ToArray();

        public MenuItem[] MenuItems => menuRootItems.SelectMany(mri => mri.AllChidren).ToArray();

        private static readonly List<uint> menuItemClickedIds = new List<uint>();

        private void Awake()
        {
            StartPlugin();
            SetupMenuItemRoot();
            SetMenuItemCallback(OnMenuItemClicked);
            BuildMenu();
        }

        internal void SetupMenuItemRoot()
        {
            if(menuRootItems != null)
            {
                foreach (var menuRootItem in menuRootItems)
                {
                    menuRootItem.Id = AddMenuRoot(menuRootItem.Name);
                    menuRootItem.FullPath = menuRootItem.Name;
                    SetupMenuItemRecursively(menuRootItem);
                }
            }
        }

        private void SetupMenuItemRecursively(AbstractMenuItem parent)
        {
            foreach (var menuItem in parent.MenuItems)
            {
                var menuItemName = menuItem.Name;
                
                // If it does not have subItems we can add suffix the shortcut
                if (menuItem.MenuItems.Count == 0)
                {
                    if (menuItem.ShortcutCombination != MenuItem.CombinationKeys.None && !char.IsWhiteSpace(menuItem.Shortcut))
                    {
                        menuItemName += $"\t{menuItem.ShortcutCombination}+{menuItem.Shortcut.ToString().ToUpperInvariant()}";
                    }
                    else if (!char.IsWhiteSpace(menuItem.Shortcut))
                    {
                        menuItemName += $"\t{menuItem.Shortcut.ToString().ToUpperInvariant()}";
                    }
                }
                menuItem.Id = AddMenuItem(parent.Id, menuItemName, menuItem.MenuItems.Count > 0);
                menuItem.FullPath = $"{parent.FullPath}/{menuItem.Name}";

                SetupMenuItemRecursively(menuItem);
            }
        }

        private void Update()
        {
            // Check the shortcuts to trigger eventsa
            foreach (var menuItem in MenuItems)
            {
                var keycodes = menuItem.ShortcutKeys;
                if (menuItem.ShortcutCombination == MenuItem.CombinationKeys.None || keycodes.Any(k => Input.GetKey(k)))
                {
                    if (System.Enum.TryParse(menuItem.Shortcut.ToString().ToUpperInvariant(), out KeyCode keyCode) && Input.GetKeyDown(keyCode))
                    {
                        menuItem.Action?.Invoke();
                    }
                }
            }

            // Trigger events for clicked menu items
            foreach (var menuItemId in menuItemClickedIds)
            {
                var menuItem = MenuItems.FirstOrDefault(m => m.Id == menuItemId);
                if (menuItem != null)
                {
                    menuItem.Action?.Invoke();
                }
            }
            menuItemClickedIds.Clear();
        }

        [MonoPInvokeCallback(typeof(MenuItemClicked))]
        internal static void OnMenuItemClicked(uint menuItemId)
        {
            menuItemClickedIds.Add(menuItemId);
        }
    }
}