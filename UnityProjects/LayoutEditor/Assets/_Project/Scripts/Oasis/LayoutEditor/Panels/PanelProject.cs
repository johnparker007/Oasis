using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Oasis.LayoutEditor;

namespace Oasis.LayoutEditor.Panels
{
    [DisallowMultipleComponent]
    public class PanelProject : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 1f;
        private const int IndentWidth = 18;

        private static readonly Color sTreeItemNormalColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color sTreeItemSelectedColor = new Color(0.22f, 0.45f, 0.86f, 0.6f);
        private static readonly Color sTreeBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
        private static readonly Color sContentBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        private RectTransform _treeContent;
        private RectTransform _contentList;
        private bool _uiBuilt = false;
        private string _currentAssetsPath = null;
        private readonly Dictionary<string, TreeNode> _nodesByPath = new(StringComparer.OrdinalIgnoreCase);
        private TreeNode _selectedNode = null;
        private float _nextRefreshTime = 0f;
        private Text _statusMessage = null;

        private class TreeNode
        {
            public TreeNode Parent;
            public string Name;
            public string FullPath;
            public List<TreeNode> Children = new();
            public int Depth;
            public bool IsExpanded = true;

            public RectTransform Container;
            public RectTransform ChildrenContainer;
            public Button HeaderButton;
            public Image HeaderBackground;
            public Text HeaderLabel;
        }

        private void Awake()
        {
            EnsureInitialised();
        }

        private void OnEnable()
        {
            if (!_uiBuilt)
            {
                EnsureInitialised();
            }

            ForceRefresh();
        }

        private void Update()
        {
            if (!_uiBuilt)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
                RefreshIfProjectChanged(false);
            }
        }

        public void EnsureInitialised()
        {
            if (_uiBuilt)
            {
                return;
            }

            DisableExistingViewComponents();
            BuildRuntimeUi();
            _uiBuilt = true;
        }

        public void ForceRefresh()
        {
            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshIfProjectChanged(true);
        }

        private void DisableExistingViewComponents()
        {
            EditorPanel editorPanel = GetComponent<EditorPanel>();
            if (editorPanel != null)
            {
                editorPanel.enabled = false;
            }

            Zoom zoom = GetComponent<Zoom>();
            if (zoom != null)
            {
                zoom.enabled = false;
            }

            // Hide the existing view specific children that belong to the layout canvas.
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }

        private void BuildRuntimeUi()
        {
            RectTransform rootRect = GetComponent<RectTransform>();

            GameObject containerObject = new("ProjectPanelRoot", typeof(RectTransform));
            containerObject.transform.SetParent(rootRect, false);
            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup horizontalLayout = containerObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = 6f;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = true;
            horizontalLayout.childForceExpandHeight = true;
            horizontalLayout.padding = new RectOffset(6, 6, 6, 6);

            // Tree area
            RectTransform treeArea = CreateColumn(containerRect, "TreeArea", 240f, sTreeBackgroundColor, out ScrollRect treeScrollRect, out RectTransform treeContent);
            treeArea.SetSiblingIndex(0);
            _treeContent = treeContent;
            treeScrollRect.vertical = true;
            treeScrollRect.horizontal = false;

            VerticalLayoutGroup treeLayout = treeContent.gameObject.AddComponent<VerticalLayoutGroup>();
            treeLayout.spacing = 4f;
            treeLayout.childControlWidth = true;
            treeLayout.childControlHeight = false;
            treeLayout.childForceExpandWidth = true;
            treeLayout.childForceExpandHeight = false;

            ContentSizeFitter treeFitter = treeContent.gameObject.AddComponent<ContentSizeFitter>();
            treeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Content list area
            RectTransform contentArea = CreateColumn(containerRect, "ContentArea", -1f, sContentBackgroundColor, out ScrollRect contentScrollRect, out RectTransform contentList);
            _contentList = contentList;
            contentArea.SetSiblingIndex(1);
            contentScrollRect.vertical = true;
            contentScrollRect.horizontal = false;

            VerticalLayoutGroup contentLayout = contentList.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = contentList.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static RectTransform CreateColumn(
            RectTransform parent,
            string name,
            float preferredWidth,
            Color backgroundColor,
            out ScrollRect scrollRect,
            out RectTransform content)
        {
            GameObject columnObject = new(name, typeof(RectTransform));
            columnObject.transform.SetParent(parent, false);
            RectTransform columnRect = columnObject.GetComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(0f, 0f);
            columnRect.anchorMax = new Vector2(1f, 1f);
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;

            LayoutElement layoutElement = columnObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
                layoutElement.flexibleWidth = 0f;
            }
            else
            {
                layoutElement.flexibleWidth = 1f;
            }

            GameObject scrollViewObject = new("ScrollView", typeof(RectTransform));
            scrollViewObject.transform.SetParent(columnRect, false);
            RectTransform scrollViewRect = scrollViewObject.GetComponent<RectTransform>();
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.offsetMin = Vector2.zero;
            scrollViewRect.offsetMax = Vector2.zero;

            Image background = scrollViewObject.AddComponent<Image>();
            background.color = backgroundColor;

            scrollRect = scrollViewObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObject = new("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(scrollViewRect, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportObject.AddComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportRect, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            scrollRect.viewport = viewportRect;
            scrollRect.content = content;

            return columnRect;
        }

        private void RefreshIfProjectChanged(bool force)
        {
            string assetsPath = null;
            if (Editor.Instance != null && Editor.Instance.ProjectsController != null)
            {
                assetsPath = Editor.Instance.ProjectsController.ProjectAssetsPath;
            }

            if (string.IsNullOrWhiteSpace(assetsPath) || !Directory.Exists(assetsPath))
            {
                if (force || _currentAssetsPath != null)
                {
                    _currentAssetsPath = null;
                    DisplayStatusMessage("No project loaded.");
                }
                return;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(assetsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PanelProject: Failed to normalise assets path '{assetsPath}': {exception.Message}");
                DisplayStatusMessage("Unable to read project assets.");
                return;
            }

            if (!force && string.Equals(_currentAssetsPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentAssetsPath = normalizedPath;
            RebuildTree();
        }

        private void DisplayStatusMessage(string message)
        {
            ClearTreeUi();
            ClearContentUi();

            if (_statusMessage == null)
            {
                GameObject labelObject = new("StatusMessage", typeof(RectTransform));
                labelObject.transform.SetParent(_contentList, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = new Vector2(8f, -32f);
                labelRect.offsetMax = new Vector2(-8f, 0f);

                Text messageText = labelObject.AddComponent<Text>();
                messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                messageText.fontSize = 16;
                messageText.alignment = TextAnchor.MiddleLeft;
                messageText.color = Color.white;
                _statusMessage = messageText;
            }

            _statusMessage.text = message;
            _statusMessage.gameObject.SetActive(true);
        }

        private void RebuildTree()
        {
            ClearTreeUi();
            ClearContentUi();

            if (_statusMessage != null)
            {
                _statusMessage.gameObject.SetActive(false);
            }

            TreeNode rootNode = new TreeNode
            {
                Parent = null,
                Name = Path.GetFileName(_currentAssetsPath),
                FullPath = _currentAssetsPath,
                Depth = 0,
                IsExpanded = true
            };

            _nodesByPath.Clear();
            _nodesByPath[rootNode.FullPath] = rootNode;

            BuildDirectoryChildren(rootNode);
            CreateTreeNodeUi(rootNode, _treeContent);
            ExpandRecursively(rootNode);
            SelectNode(rootNode);
        }

        private void ExpandRecursively(TreeNode node)
        {
            node.ChildrenContainer?.gameObject.SetActive(node.IsExpanded);
            UpdateNodeLabel(node);
            foreach (TreeNode child in node.Children)
            {
                ExpandRecursively(child);
            }
        }

        private void BuildDirectoryChildren(TreeNode parent)
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(parent.FullPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PanelProject: Failed to read directories for '{parent.FullPath}': {exception.Message}");
                directories = Array.Empty<string>();
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            foreach (string directory in directories)
            {
                TreeNode child = new TreeNode
                {
                    Parent = parent,
                    Name = Path.GetFileName(directory),
                    FullPath = directory,
                    Depth = parent.Depth + 1,
                    IsExpanded = false
                };

                parent.Children.Add(child);
                _nodesByPath[child.FullPath] = child;

                BuildDirectoryChildren(child);
            }
        }

        private void CreateTreeNodeUi(TreeNode node, RectTransform parent)
        {
            GameObject containerObject = new(node.Name + "_Node", typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(0f, 1f);
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layoutGroup = containerObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 2f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.padding = new RectOffset(node.Depth * IndentWidth, 0, 0, 0);

            GameObject headerObject = new("Header", typeof(RectTransform));
            headerObject.transform.SetParent(containerRect, false);
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            Image background = headerObject.AddComponent<Image>();
            background.color = sTreeItemNormalColor;

            Button button = headerObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            button.colors = colors;

            GameObject labelObject = new("Label", typeof(RectTransform));
            labelObject.transform.SetParent(headerRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;

            node.Container = containerRect;
            node.HeaderButton = button;
            node.HeaderBackground = background;
            node.HeaderLabel = label;

            button.onClick.AddListener(() => OnTreeNodeClicked(node));

            GameObject childrenObject = new("Children", typeof(RectTransform));
            childrenObject.transform.SetParent(containerRect, false);
            RectTransform childrenRect = childrenObject.GetComponent<RectTransform>();
            childrenRect.anchorMin = new Vector2(0f, 1f);
            childrenRect.anchorMax = new Vector2(1f, 1f);
            childrenRect.pivot = new Vector2(0f, 1f);
            childrenRect.offsetMin = Vector2.zero;
            childrenRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup childrenLayout = childrenObject.AddComponent<VerticalLayoutGroup>();
            childrenLayout.spacing = 2f;
            childrenLayout.childControlWidth = true;
            childrenLayout.childControlHeight = false;
            childrenLayout.childForceExpandWidth = true;
            childrenLayout.childForceExpandHeight = false;

            node.ChildrenContainer = childrenRect;

            foreach (TreeNode child in node.Children)
            {
                CreateTreeNodeUi(child, childrenRect);
            }

            node.ChildrenContainer.gameObject.SetActive(node.IsExpanded && node.Children.Count > 0);
            UpdateNodeLabel(node);
        }

        private void OnTreeNodeClicked(TreeNode node)
        {
            if (node.Children.Count > 0)
            {
                node.IsExpanded = !node.IsExpanded;
                node.ChildrenContainer.gameObject.SetActive(node.IsExpanded);
                UpdateNodeLabel(node);
            }

            SelectNode(node);
        }

        private void SelectNode(TreeNode node)
        {
            if (_selectedNode == node)
            {
                PopulateContentList(node);
                return;
            }

            if (_selectedNode != null && _selectedNode.HeaderBackground != null)
            {
                _selectedNode.HeaderBackground.color = sTreeItemNormalColor;
            }

            _selectedNode = node;

            if (_selectedNode != null && _selectedNode.HeaderBackground != null)
            {
                _selectedNode.HeaderBackground.color = sTreeItemSelectedColor;
            }

            PopulateContentList(node);
        }

        private void PopulateContentList(TreeNode node)
        {
            ClearContentUi();

            if (node == null)
            {
                return;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(node.FullPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PanelProject: Failed to enumerate directories for '{node.FullPath}': {exception.Message}");
                directories = Array.Empty<string>();
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            foreach (string directory in directories)
            {
                string directoryPath = directory;
                string directoryName = Path.GetFileName(directoryPath);
                Button button = CreateContentItem(directoryName, true);
                button.onClick.AddListener(() => OnContentDirectoryClicked(directoryPath));
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(node.FullPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PanelProject: Failed to enumerate files for '{node.FullPath}': {exception.Message}");
                files = Array.Empty<string>();
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                CreateContentItem(fileName, false);
            }
        }

        private void OnContentDirectoryClicked(string directoryPath)
        {
            if (!_nodesByPath.TryGetValue(directoryPath, out TreeNode node))
            {
                return;
            }

            // Ensure all parents are expanded so the node is visible.
            TreeNode current = node.Parent;
            while (current != null)
            {
                if (!current.IsExpanded)
                {
                    current.IsExpanded = true;
                    current.ChildrenContainer?.gameObject.SetActive(true);
                    UpdateNodeLabel(current);
                }
                current = current.Parent;
            }

            node.IsExpanded = true;
            node.ChildrenContainer?.gameObject.SetActive(node.Children.Count > 0);
            UpdateNodeLabel(node);

            SelectNode(node);
        }

        private Button CreateContentItem(string label, bool isDirectory)
        {
            GameObject itemObject = new(label, typeof(RectTransform));
            itemObject.transform.SetParent(_contentList, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0f, 1f);
            itemRect.offsetMin = Vector2.zero;
            itemRect.offsetMax = Vector2.zero;

            Image background = itemObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.2f);

            Button button = itemObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            button.colors = colors;

            GameObject labelObject = new("Label", typeof(RectTransform));
            labelObject.transform.SetParent(itemRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            Text text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = isDirectory ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
            text.text = isDirectory ? $"📁 {label}" : $"📄 {label}";

            LayoutElement layoutElement = itemObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 32f;
            layoutElement.preferredHeight = 32f;

            if (!isDirectory)
            {
                button.interactable = false;
            }

            return button;
        }

        private void UpdateNodeLabel(TreeNode node)
        {
            if (node.HeaderLabel == null)
            {
                return;
            }

            string prefix;
            if (node.Children.Count > 0)
            {
                prefix = node.IsExpanded ? "▼ " : "▶ ";
            }
            else
            {
                prefix = "   ";
            }

            node.HeaderLabel.text = prefix + node.Name;
        }

        private void ClearTreeUi()
        {
            if (_treeContent == null)
            {
                return;
            }

            for (int i = _treeContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_treeContent.GetChild(i).gameObject);
            }

            _nodesByPath.Clear();
            _selectedNode = null;
        }

        private void ClearContentUi()
        {
            if (_contentList == null)
            {
                return;
            }

            for (int i = _contentList.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentList.GetChild(i).gameObject);
            }
        }
    }
}
