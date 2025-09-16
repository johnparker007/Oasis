using System;
using UnityEngine;

namespace Oasis.NativeMenuNEW
{
    public static class NativeMenuRegistry
    {
        private static NativeMenuManager _manager;

        public static event Action ManagerChanged;

        public static bool HasManager => _manager != null;

        public static void Register(NativeMenuManager manager)
        {
            _manager = manager;
            ManagerChanged?.Invoke();
        }

        public static void Unregister(NativeMenuManager manager)
        {
            if (_manager == manager)
            {
                _manager = null;
                ManagerChanged?.Invoke();
            }
        }

        public static bool Execute(string path)
        {
            if (!Application.isPlaying || _manager == null)
            {
                return false;
            }

            return _manager.Execute(path);
        }

        public static bool CanExecute(string path)
        {
            return Application.isPlaying && _manager != null && _manager.CanExecute(path);
        }

        public static bool IsItemEnabled(string path)
        {
            return Application.isPlaying && _manager != null && _manager.IsItemEnabled(path);
        }

        public static bool IsItemChecked(string path)
        {
            return Application.isPlaying && _manager != null && _manager.IsItemChecked(path);
        }

        public static void RequestRefresh()
        {
            _manager?.RequestRefresh();
        }

        internal static NativeMenuManager CurrentManager => _manager;
    }
}
