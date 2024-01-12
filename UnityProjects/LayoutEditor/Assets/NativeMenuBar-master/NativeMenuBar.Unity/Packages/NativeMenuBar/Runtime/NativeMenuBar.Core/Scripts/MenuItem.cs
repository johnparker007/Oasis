using System;
using UnityEngine;
using UnityEngine.Events;

namespace NativeMenuBar.Core
{
    [Serializable]
    public class MenuItem : AbstractMenuItem
    {
        [SerializeField]
        private char shortcut = ' ';
        public char Shortcut
        {
            get => shortcut;
            set => shortcut = value;
        }

        [SerializeField]
        private CombinationKeys shortcutCombination = default;
        public CombinationKeys ShortcutCombination
        {
            get => shortcutCombination;
            set => shortcutCombination = value;
        }
        public KeyCode[] ShortcutKeys
        {
            get
            {
                switch (shortcutCombination)
                {
                    case CombinationKeys.Shift:
                        return new KeyCode[] { KeyCode.LeftShift, KeyCode.RightShift };
                    case CombinationKeys.Ctrl:
                        return new KeyCode[] { KeyCode.LeftControl, KeyCode.RightControl };
                    case CombinationKeys.Alt:
                        return new KeyCode[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr };
                    case CombinationKeys.None:
                    default:
                        return Array.Empty<KeyCode>();
                }
            }
        }

        [SerializeField]
        private UnityEvent action = default;
        public UnityEvent Action
        {
            get => action;
            set => action = value;
        }

        public bool IsToggled
        {
            get => MenuBar.IsMenuItemSelected(Id);
            set => MenuBar.SetMenuItemSelected(Id, value);
        }

        public bool IsInteractable
        {
            get => MenuBar.IsMenuItemEnabled(Id);
            set => MenuBar.SetMenuItemEnabled(Id, value);
        }

        public enum CombinationKeys
        {
            None,
            Shift,
            Ctrl,
            Alt
        }
    }
}