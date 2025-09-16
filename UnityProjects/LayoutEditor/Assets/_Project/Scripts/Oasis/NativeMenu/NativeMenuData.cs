using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.NativeMenu
{
    /// <summary>
    /// Serializable description of a menu entry before it is converted into runtime menu items.
    /// </summary>
    public sealed class MenuEntry
    {
        public MenuEntry(string path, int priority, Action action, Func<bool> enabledEvaluator, Func<bool> checkedEvaluator, string shortcut)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Priority = priority;
            Action = action;
            EnabledEvaluator = enabledEvaluator ?? (() => action != null);
            CheckedEvaluator = checkedEvaluator;
            Shortcut = shortcut;
        }

        public string Path { get; }
        public int Priority { get; }
        public Action Action { get; }
        public Func<bool> EnabledEvaluator { get; }
        public Func<bool> CheckedEvaluator { get; }
        public string Shortcut { get; }
    }

    /// <summary>
    /// Runtime representation of a menu item in the hierarchical menu tree.
    /// </summary>
    public sealed class NativeMenuItem
    {
        private readonly List<NativeMenuItem> _children = new List<NativeMenuItem>();

        internal NativeMenuItem(string title, string fullPath, bool isSeparator)
        {
            Title = title;
            FullPath = fullPath;
            IsSeparator = isSeparator;
        }

        public string Title { get; }
        public string FullPath { get; }
        public bool IsSeparator { get; }
        public int Priority { get; private set; }
        public string Shortcut { get; private set; }
        internal Func<bool> EnabledEvaluator { get; private set; }
        internal Func<bool> CheckedEvaluator { get; private set; }
        internal Action Action { get; private set; }

        public IReadOnlyList<NativeMenuItem> Children => _children;
        public bool HasChildren => _children.Count > 0;
        public bool HasAction => Action != null;

        internal static NativeMenuItem CreateSeparator(string fullPath)
        {
            return new NativeMenuItem("-", fullPath, true)
            {
                EnabledEvaluator = () => false,
                CheckedEvaluator = () => false,
            };
        }

        internal void ApplyEntry(MenuEntry entry)
        {
            Priority = entry.Priority;
            Action = entry.Action;
            Shortcut = entry.Shortcut;
            EnabledEvaluator = entry.EnabledEvaluator ?? (() => entry.Action != null);
            CheckedEvaluator = entry.CheckedEvaluator ?? (() => false);
        }

        internal void EnsureContainerEvaluators()
        {
            if (IsSeparator)
            {
                EnabledEvaluator = () => false;
                CheckedEvaluator = () => false;
            }
            else
            {
                EnabledEvaluator ??= () => true;
                CheckedEvaluator ??= () => false;
            }
        }

        internal void AddChild(NativeMenuItem child)
        {
            _children.Add(child);
        }

        internal void ReplaceChildren(List<NativeMenuItem> children)
        {
            _children.Clear();
            _children.AddRange(children);
        }

        internal bool EvaluateEnabled()
        {
            if (IsSeparator)
            {
                return false;
            }

            try
            {
                if (EnabledEvaluator != null)
                {
                    return EnabledEvaluator();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            return HasAction || HasChildren;
        }

        internal bool EvaluateChecked()
        {
            if (IsSeparator)
            {
                return false;
            }

            if (CheckedEvaluator == null)
            {
                return false;
            }

            try
            {
                return CheckedEvaluator();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        internal void Invoke()
        {
            if (Action == null)
            {
                return;
            }

            try
            {
                Action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
