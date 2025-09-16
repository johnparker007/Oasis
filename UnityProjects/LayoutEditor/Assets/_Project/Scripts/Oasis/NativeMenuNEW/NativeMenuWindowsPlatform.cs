#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Oasis.NativeMenuNEW
{
    internal sealed class NativeMenuWindowsPlatform : INativeMenuPlatform
    {
        private const uint MF_STRING = 0x00000000;
        private const uint MF_CHECKED = 0x00000008;
        private const uint MF_POPUP = 0x00000010;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_DISABLED = 0x00000002;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint WM_COMMAND = 0x0111;
        private const int GWLP_WNDPROC = -4;

        private NativeMenuManager _manager;
        private IntPtr _hwnd;
        private IntPtr _menuHandle;
        private readonly Dictionary<int, NativeMenuItem> _commandMap = new Dictionary<int, NativeMenuItem>();
        private int _nextCommandId = 1000;
        private WndProc _wndProcDelegate;
        private IntPtr _previousWndProc;
        private bool _hookInstalled;

        public void Initialize(NativeMenuManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _manager.MenuStructureChanged += OnMenuStructureChanged;

            _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = GetForegroundWindow();
            }

            InstallWndProcHook();
            RebuildMenu();
        }

        private void OnMenuStructureChanged()
        {
            RebuildMenu();
        }

        private void RebuildMenu()
        {
            DestroyMenu();

            if (_manager == null || _hwnd == IntPtr.Zero)
            {
                return;
            }

            _commandMap.Clear();
            _nextCommandId = 1000;

            _menuHandle = CreateMenu();
            if (_menuHandle == IntPtr.Zero)
            {
                return;
            }

            foreach (var root in _manager.RootItems)
            {
                if (root.Children.Count == 0)
                {
                    continue;
                }

                IntPtr submenu = CreatePopupMenu();
                if (submenu == IntPtr.Zero)
                {
                    continue;
                }

                AppendMenuRecursive(submenu, root.Children);
                AppendMenu(_menuHandle, MF_POPUP, ToUIntPtr(submenu), root.Title);
            }

            SetMenu(_hwnd, _menuHandle);
            DrawMenuBar(_hwnd);
        }

        private void AppendMenuRecursive(IntPtr parentMenu, IReadOnlyList<NativeMenuItem> items)
        {
            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    AppendMenu(parentMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
                    continue;
                }

                string label = item.Title;
                if (!string.IsNullOrEmpty(item.Shortcut))
                {
                    label += "\t" + item.Shortcut;
                }

                uint flags = MF_STRING;
                if (!item.EvaluateEnabled())
                {
                    flags |= MF_GRAYED | MF_DISABLED;
                }

                if (item.EvaluateChecked())
                {
                    flags |= MF_CHECKED;
                }

                if (item.HasChildren)
                {
                    IntPtr submenu = CreatePopupMenu();
                    if (submenu == IntPtr.Zero)
                    {
                        continue;
                    }

                    AppendMenuRecursive(submenu, item.Children);
                    AppendMenu(parentMenu, flags | MF_POPUP, ToUIntPtr(submenu), label);
                }
                else
                {
                    int commandId = _nextCommandId++;
                    _commandMap[commandId] = item;
                    AppendMenu(parentMenu, flags, new UIntPtr((uint)commandId), label);
                }
            }
        }

        private void DestroyMenu()
        {
            if (_menuHandle != IntPtr.Zero)
            {
                SetMenu(_hwnd, IntPtr.Zero);
                DrawMenuBar(_hwnd);
                DestroyMenu(_menuHandle);
                _menuHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.MenuStructureChanged -= OnMenuStructureChanged;
                _manager = null;
            }

            DestroyMenu();

            if (_hookInstalled && _hwnd != IntPtr.Zero && _previousWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _previousWndProc);
                _hookInstalled = false;
            }

            _commandMap.Clear();
        }

        private void InstallWndProcHook()
        {
            if (_hookInstalled || _hwnd == IntPtr.Zero)
            {
                return;
            }

            _wndProcDelegate = CustomWndProc;
            IntPtr newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _previousWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newProc);
            if (_previousWndProc != IntPtr.Zero)
            {
                _hookInstalled = true;
            }
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_COMMAND)
            {
                int commandId = unchecked((int)wParam.ToInt64()) & 0xffff;
                if (_commandMap.TryGetValue(commandId, out var item))
                {
                    item.Invoke();
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
        }

        private static UIntPtr ToUIntPtr(IntPtr ptr)
        {
            if (IntPtr.Size == 8)
            {
                return new UIntPtr((ulong)ptr.ToInt64());
            }

            return new UIntPtr((uint)ptr.ToInt32());
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateMenu();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "AppendMenuW")]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetMenu(IntPtr hWnd, IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
#endif
