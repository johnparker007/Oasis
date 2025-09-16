using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oasis.NativeMenu
{
    public sealed class NativeMenuManager
    {
        private readonly Dictionary<string, NativeMenuItem> _itemsByPath = new Dictionary<string, NativeMenuItem>();
        private readonly Dictionary<string, NativeMenuItem> _nodesByPath = new Dictionary<string, NativeMenuItem>();
        private readonly List<NativeMenuItem> _rootItems = new List<NativeMenuItem>();

        public event Action MenuStructureChanged;

        public IReadOnlyList<NativeMenuItem> RootItems => _rootItems;

        public void LoadMenu(IEnumerable<MenuEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _itemsByPath.Clear();
            _nodesByPath.Clear();
            _rootItems.Clear();

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Path))
                {
                    continue;
                }

                var segments = entry.Path.Split('/');
                if (segments.Length < 2)
                {
                    Debug.LogWarning($"Menu path '{entry.Path}' must contain at least two segments.");
                    continue;
                }

                NativeMenuItem parent = null;
                string currentPath = string.Empty;

                for (int i = 0; i < segments.Length; i++)
                {
                    string segment = segments[i];
                    currentPath = string.IsNullOrEmpty(currentPath) ? segment : string.Concat(currentPath, "/", segment);
                    bool isLeaf = i == segments.Length - 1;

                    if (!_nodesByPath.TryGetValue(currentPath, out var node))
                    {
                        node = new NativeMenuItem(segment, currentPath, isSeparator: false);
                        _nodesByPath[currentPath] = node;

                        if (parent == null)
                        {
                            _rootItems.Add(node);
                        }
                        else
                        {
                            parent.AddChild(node);
                        }
                    }

                    parent = node;

                    if (isLeaf)
                    {
                        node.ApplyEntry(entry);
                        _itemsByPath[currentPath] = node;
                    }
                }
            }

            foreach (var node in _nodesByPath.Values)
            {
                node.EnsureContainerEvaluators();
            }

            foreach (var root in _rootItems)
            {
                SortChildrenRecursively(root);
            }

            MenuStructureChanged?.Invoke();
        }

        private void SortChildrenRecursively(NativeMenuItem parent)
        {
            if (!parent.HasChildren)
            {
                return;
            }

            var orderedChildren = parent.Children.Where(c => !c.IsSeparator).OrderBy(c => c.Priority).ToList();
            var sorted = new List<NativeMenuItem>(orderedChildren.Count);

            for (int i = 0; i < orderedChildren.Count; i++)
            {
                var child = orderedChildren[i];
                if (i > 0)
                {
                    int previousPriority = orderedChildren[i - 1].Priority;
                    if (child.Priority - previousPriority >= 11)
                    {
                        string separatorPath = string.Concat(parent.FullPath, "/__sep", i.ToString());
                        sorted.Add(NativeMenuItem.CreateSeparator(separatorPath));
                    }
                }

                sorted.Add(child);
                SortChildrenRecursively(child);
            }

            parent.ReplaceChildren(sorted);
        }

        public bool CanExecute(string path)
        {
            return TryGetItem(path, out var item) && item.HasAction && item.EvaluateEnabled();
        }

        public bool Execute(string path)
        {
            if (!TryGetItem(path, out var item))
            {
                return false;
            }

            if (!item.HasAction || !item.EvaluateEnabled())
            {
                return false;
            }

            item.Invoke();
            return true;
        }

        public bool IsItemEnabled(string path)
        {
            return TryGetItem(path, out var item) && item.EvaluateEnabled();
        }

        public bool IsItemChecked(string path)
        {
            return TryGetItem(path, out var item) && item.EvaluateChecked();
        }

        public bool TryGetItem(string path, out NativeMenuItem item)
        {
            return _itemsByPath.TryGetValue(path, out item);
        }

        public void RequestRefresh()
        {
            MenuStructureChanged?.Invoke();
        }
    }
}
