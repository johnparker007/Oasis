// Unity Windows Native Context Menu (Runtime)
// Works in Editor (Game view) and Windows builds (windowed / borderless fullscreen).
// Displays a true OS popup (TrackPopupMenuEx), supports submenus, disabled/checked items,
// and optional OS-level hotkeys via RegisterHotKey + PeekMessage.
//
// Files: put this entire file in Assets/Scripts or split into two: NativeContextMenuManager.cs and NativeContextMenuDemo.cs
// Target platform: Windows only.

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NativeWindowsContextMenu
{
    [Flags]
    public enum HotkeyModifiers : uint
    {
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000
    }

    public struct Hotkey
    {
        public HotkeyModifiers Modifiers;
        public uint VirtualKey; // Win32 VK_* value

        public Hotkey(HotkeyModifiers mods, uint vk)
        {
            Modifiers = mods; VirtualKey = vk;
        }

        // Helper: Ctrl + letter (A-Z)
        public static Hotkey Ctrl(char letter)
        {
            letter = char.ToUpperInvariant(letter);
            return new Hotkey(HotkeyModifiers.Control, (uint)letter);
        }
    }

    public sealed class NativeContextMenuManager : MonoBehaviour
    {
        public static NativeContextMenuManager Instance { get; private set; }

        // Show the menu even if mouse is captured by Unity
        [SerializeField] bool setForegroundOnShow = true;

        // In Editor, avoid global hotkeys to not steal Editor shortcuts. Build-only by default.
        [SerializeField] bool allowGlobalHotkeysInEditor = false;

        // --- Public API types ---
        public sealed class MenuItemSpec
        {
            public string Text;              // Use "\tCtrl+S" to show native shortcut text on the right
            public bool Enabled = true;
            public bool Checked = false;
            public bool Separator = false;
            public Action OnClick;           // Invoked when item chosen (no submenu)
            public Hotkey? Shortcut;         // Optional OS hotkey (when registered via RegisterHotkey)
            public List<MenuItemSpec> Children = new List<MenuItemSpec>();

            public MenuItemSpec() { }
            public MenuItemSpec(string text, Action onClick = null, bool enabled = true, bool isChecked = false)
            { Text = text; OnClick = onClick; Enabled = enabled; Checked = isChecked; }

            public static MenuItemSpec Sep() => new MenuItemSpec { Separator = true };
        }

        // --- Win32 interop ---
        const uint MF_STRING = 0x00000000;
        const uint MF_CHECKED = 0x00000008;
        const uint MF_POPUP = 0x00000010;
        const uint MF_SEPARATOR = 0x00000800;
        const uint MF_GRAYED = 0x00000001;
        const uint MF_DISABLED = 0x00000002;

        const uint TPM_LEFTALIGN = 0x0000;
        const uint TPM_TOPALIGN = 0x0000;
        const uint TPM_RIGHTBUTTON = 0x0002;
        const uint TPM_RETURNCMD = 0x0100;
        const uint TPM_NOANIMATION = 0x4000; // optional

        const uint WM_HOTKEY = 0x0312;
        const uint WM_NULL = 0x0000;
        const uint PM_REMOVE = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "AppendMenuW")]
        static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int TrackPopupMenuEx(IntPtr hmenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hWnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public POINT pt;
        }

        // --- Implementation state ---
        int _nextCommandId = 1000;
        readonly Dictionary<int, Action> _commandMap = new Dictionary<int, Action>();

        int _nextHotkeyId = 1;
        readonly Dictionary<int, Action> _hotkeyMap = new Dictionary<int, Action>();
        readonly List<int> _registeredHotkeyIds = new List<int>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            PumpHotkeyMessages();
        }

        void OnDestroy()
        {
            // Clean up any hotkeys
            IntPtr hwnd = GetUnityWindow();
            foreach (var id in _registeredHotkeyIds)
            {
                try { UnregisterHotKey(hwnd, id); } catch { }
            }
            _registeredHotkeyIds.Clear();
            _hotkeyMap.Clear();
        }

        IntPtr GetUnityWindow()
        {
            // Prefer the active window (player). In Editor, this is the Editor; OK for owner.
            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) hwnd = GetForegroundWindow();
            return hwnd;
        }

        // --- Public API ---
        public void ShowMenuAtCursor(List<MenuItemSpec> items)
        {
            _commandMap.Clear();
            IntPtr menu = BuildMenuRecursive(items);
            if (menu == IntPtr.Zero) return;

            try
            {
                if (setForegroundOnShow)
                {
                    SetForegroundWindow(GetUnityWindow());
                }

                GetCursorPos(out var p);
                int cmd = TrackPopupMenuEx(menu,
                    TPM_LEFTALIGN | TPM_TOPALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD /*| TPM_NOANIMATION*/,
                    p.X, p.Y, GetUnityWindow(), IntPtr.Zero);

                // Required quirk to ensure the menu properly dismisses on click outside
                PostMessage(GetUnityWindow(), WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (cmd != 0 && _commandMap.TryGetValue(cmd, out var action) && action != null)
                {
                    try { action.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                }
            }
            finally
            {
                DestroyMenu(menu); // destroys submenus added via AppendMenu(MF_POPUP,...)
            }
        }

        public void RegisterHotkey(Hotkey hotkey, Action action, bool editorAllowed = false)
        {
            bool inEditor = Application.isEditor;
            if (inEditor && !allowGlobalHotkeysInEditor && !editorAllowed)
            {
                // Avoid colliding with Unity Editor shortcuts; user can enable via inspector.
                return;
            }

            IntPtr hwnd = GetUnityWindow();
            int id = _nextHotkeyId++;

            if (!RegisterHotKey(hwnd, id, (uint)hotkey.Modifiers, hotkey.VirtualKey))
            {
                // If already registered by another app, we silently ignore.
                return;
            }

            _registeredHotkeyIds.Add(id);
            _hotkeyMap[id] = action;
        }

        // Convenience: register all hotkeys declared on the menu spec tree
        public void RegisterHotkeysFromMenuTree(List<MenuItemSpec> items)
        {
            foreach (var it in items)
            {
                if (it.Shortcut.HasValue && it.OnClick != null)
                    RegisterHotkey(it.Shortcut.Value, it.OnClick);
                if (it.Children != null && it.Children.Count > 0)
                    RegisterHotkeysFromMenuTree(it.Children);
            }
        }

        // --- Internals ---
        IntPtr BuildMenuRecursive(List<MenuItemSpec> items)
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return IntPtr.Zero;

            foreach (var item in items)
            {
                if (item.Separator)
                {
                    AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
                    continue;
                }

                uint itemFlags = MF_STRING;
                if (!item.Enabled) itemFlags |= (MF_GRAYED | MF_DISABLED);
                if (item.Checked) itemFlags |= MF_CHECKED;

                string label = item.Text ?? string.Empty;

                if (item.Children != null && item.Children.Count > 0)
                {
                    // Submenu
                    IntPtr sub = BuildMenuRecursive(item.Children);
                    if (sub == IntPtr.Zero) continue;
                    AppendMenu(hMenu, itemFlags | MF_POPUP, ToUIntPtr(sub), label);
                }
                else
                {
                    int id = _nextCommandId++;
                    if (item.OnClick != null)
                        _commandMap[id] = item.OnClick;
                    AppendMenu(hMenu, itemFlags, new UIntPtr((uint)id), label);
                }
            }

            return hMenu;
        }

        static UIntPtr ToUIntPtr(IntPtr ptr)
        {
            unchecked
            {
                if (IntPtr.Size == 8) return new UIntPtr((ulong)ptr.ToInt64());
                return new UIntPtr((uint)ptr.ToInt32());
            }
        }

        void PumpHotkeyMessages()
        {
            // WM_HOTKEY is posted to the thread or window. Poll and dispatch.
            while (PeekMessage(out var msg, IntPtr.Zero, WM_HOTKEY, WM_HOTKEY, PM_REMOVE))
            {
                int id = unchecked((int)msg.wParam.ToUInt32());
                if (_hotkeyMap.TryGetValue(id, out var action) && action != null)
                {
                    try { action.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }

        // Helper: append a shortcut string like "\tCtrl+S" to label text
        public static string WithShortcutText(string baseText, Hotkey shortcut)
        {
            string rhs = ShortcutToDisplay(shortcut);
            return string.IsNullOrEmpty(rhs) ? baseText : (baseText + "\t" + rhs);
        }

        public static string ShortcutToDisplay(Hotkey hk)
        {
            List<string> parts = new List<string>(4);
            if (hk.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (hk.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (hk.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (hk.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");

            string key;
            if (hk.VirtualKey >= 'A' && hk.VirtualKey <= 'Z') key = ((char)hk.VirtualKey).ToString();
            else key = "VK-" + hk.VirtualKey.ToString("X");

            parts.Add(key);
            return string.Join("+", parts);
        }
    }


}
#else
using UnityEngine;
using UnityEngine.EventSystems;

namespace NativeWindowsContextMenu
{
    // Stub for non-Windows platforms
    public sealed class NativeContextMenuManager : MonoBehaviour
    {
        public static NativeContextMenuManager Instance; void Awake(){ Instance=this; }
        public void ShowMenuAtCursor(object _) { Debug.LogWarning("Native Windows context menu is only available on Windows."); }
    }

    public sealed class NativeContextMenuDemo : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) { Debug.LogWarning("Windows-only demo"); }
    }
}
#endif
