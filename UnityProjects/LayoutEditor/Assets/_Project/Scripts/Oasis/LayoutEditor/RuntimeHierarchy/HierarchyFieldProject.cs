using RuntimeInspectorNamespace;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class HierarchyFieldProject : HierarchyField
{
    [SerializeField] private Image _folderIconImage;

    [SerializeField] private Sprite _closedFolderEmptySprite;
    [SerializeField] private Sprite _closedFolderNonEmptySprite;
    [SerializeField] private Sprite _openFolderNonEmptySprite;
    [SerializeField] private Sprite _genericFileSprite;

    private HierarchyData _lastObservedData;
    private bool _lastObservedExpandedStateForIcon;
    private int _lastObservedChildCount;
    private bool _hasCachedDirectoryContent;
    private bool _cachedDirectoryHasContent;
    private Sprite _lastAppliedSprite;

    private static Type s_directoryMetadataType;
    private static PropertyInfo s_directoryPathProperty;
    private static PropertyInfo s_directoryRepresentedInHierarchyTreeProperty;
    private static Type s_fileMetadataType;

    private void OnEnable()
    {
        UpdateFolderIcon(true);
    }

    private void Update()
    {
        UpdateFolderIcon(false);
    }

    private void UpdateFolderIcon(bool forceRefresh)
    {
        if (_folderIconImage == null)
        {
            return;
        }

        HierarchyData data = Data;

        if (data == null)
        {
            ResetCachedState();
            return;
        }

        bool isExpanded = data.IsExpanded;
        bool dataChanged = forceRefresh || data != _lastObservedData;
        int childCount = data.ChildCount;
        bool childCountChanged = forceRefresh || dataChanged || childCount != _lastObservedChildCount;

        bool isFileEntry = IsFileEntry(data);
        bool treatAsExpanded = false;
        Sprite targetSprite;

        if (isFileEntry)
        {
            targetSprite = _genericFileSprite;
            _hasCachedDirectoryContent = false;
            _cachedDirectoryHasContent = false;
        }
        else
        {
            bool useExpandedStateForIcon = ShouldUseTreeEntryIcons(data);
            treatAsExpanded = useExpandedStateForIcon && isExpanded;
            bool expansionChanged = forceRefresh || treatAsExpanded != _lastObservedExpandedStateForIcon;

            if (dataChanged)
            {
                _hasCachedDirectoryContent = false;
            }

            if (treatAsExpanded)
            {
                _hasCachedDirectoryContent = false;
            }
            else if (!_hasCachedDirectoryContent || childCountChanged || expansionChanged)
            {
                _cachedDirectoryHasContent = DetermineHasContent(data, childCount);
                _hasCachedDirectoryContent = true;
            }

            targetSprite = treatAsExpanded
                ? _openFolderNonEmptySprite
                : (_cachedDirectoryHasContent ? _closedFolderNonEmptySprite : _closedFolderEmptySprite);
        }

        if (_lastAppliedSprite != targetSprite)
        {
            _folderIconImage.sprite = targetSprite;
            _lastAppliedSprite = targetSprite;
        }

        _lastObservedData = data;
        _lastObservedExpandedStateForIcon = treatAsExpanded;
        _lastObservedChildCount = childCount;
    }

    private void ResetCachedState()
    {
        if (_lastAppliedSprite != null)
        {
            _folderIconImage.sprite = null;
            _lastAppliedSprite = null;
        }

        _lastObservedData = null;
        _lastObservedExpandedStateForIcon = false;
        _lastObservedChildCount = 0;
        _hasCachedDirectoryContent = false;
        _cachedDirectoryHasContent = false;
    }

    private static bool ShouldUseTreeEntryIcons(HierarchyData data)
    {
        if (data == null)
        {
            return true;
        }

        Transform boundTransform = data.BoundTransform;

        if (boundTransform == null)
        {
            return true;
        }

        EnsureProjectMetadataReflection();

        if (s_directoryMetadataType == null)
        {
            return true;
        }

        Component metadata = boundTransform.GetComponent(s_directoryMetadataType);

        if (metadata == null)
        {
            return true;
        }

        if (s_directoryRepresentedInHierarchyTreeProperty == null)
        {
            return true;
        }

        object representedValue = s_directoryRepresentedInHierarchyTreeProperty.GetValue(metadata, null);

        return representedValue is bool representedInHierarchyTree ? representedInHierarchyTree : true;
    }

    private bool DetermineHasContent(HierarchyData data, int childCount)
    {
        if (data == null)
        {
            return false;
        }

        if (childCount > 0)
        {
            return true;
        }

        Transform boundTransform = data.BoundTransform;

        if (boundTransform == null)
        {
            return false;
        }

        EnsureProjectMetadataReflection();

        if (s_directoryMetadataType == null || s_directoryPathProperty == null)
        {
            return false;
        }

        Component metadata = boundTransform.GetComponent(s_directoryMetadataType);

        if (metadata == null)
        {
            return false;
        }

        string directoryPath = s_directoryPathProperty.GetValue(metadata, null) as string;

        if (string.IsNullOrEmpty(directoryPath))
        {
            return false;
        }

        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                if (!string.IsNullOrEmpty(entry))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return true;
        }

        return false;
    }

    private static bool IsFileEntry(HierarchyData data)
    {
        if (data == null)
        {
            return false;
        }

        Transform boundTransform = data.BoundTransform;

        if (boundTransform == null)
        {
            return false;
        }

        EnsureProjectMetadataReflection();

        if (s_fileMetadataType == null)
        {
            return false;
        }

        Component metadata = boundTransform.GetComponent(s_fileMetadataType);

        return metadata != null;
    }

    private static void EnsureProjectMetadataReflection()
    {
        if (s_directoryMetadataType != null && s_fileMetadataType != null)
        {
            return;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly assembly = assemblies[i];
            if (s_directoryMetadataType == null)
            {
                Type directoryMetadataType = assembly.GetType("Oasis.LayoutEditor.Panels.PanelProject+DirectoryMetadata");

                if (directoryMetadataType != null)
                {
                    s_directoryMetadataType = directoryMetadataType;
                    s_directoryPathProperty = directoryMetadataType.GetProperty("DirectoryPath", BindingFlags.Public | BindingFlags.Instance);
                    s_directoryRepresentedInHierarchyTreeProperty = directoryMetadataType.GetProperty("RepresentedInHierarchyTree", BindingFlags.Public | BindingFlags.Instance);
                }
            }

            if (s_fileMetadataType == null)
            {
                Type fileMetadataType = assembly.GetType("Oasis.LayoutEditor.Panels.PanelProject+FileMetadata");

                if (fileMetadataType != null)
                {
                    s_fileMetadataType = fileMetadataType;
                }
            }

            if (s_directoryMetadataType != null && s_fileMetadataType != null)
            {
                break;
            }
        }
    }
}
