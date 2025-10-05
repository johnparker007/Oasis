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

    private HierarchyData _lastObservedData;
    private bool _lastObservedExpandedState;
    private int _lastObservedChildCount;
    private bool _hasCachedDirectoryContent;
    private bool _cachedDirectoryHasContent;
    private Sprite _lastAppliedSprite;

    private static Type s_directoryMetadataType;
    private static PropertyInfo s_directoryPathProperty;

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
        bool expansionChanged = forceRefresh || isExpanded != _lastObservedExpandedState;
        int childCount = data.ChildCount;
        bool childCountChanged = forceRefresh || dataChanged || childCount != _lastObservedChildCount;

        if (dataChanged)
        {
            _hasCachedDirectoryContent = false;
        }

        if (isExpanded)
        {
            _hasCachedDirectoryContent = false;
        }
        else if (!_hasCachedDirectoryContent || childCountChanged || expansionChanged)
        {
            _cachedDirectoryHasContent = DetermineHasContent(data, childCount);
            _hasCachedDirectoryContent = true;
        }

        Sprite targetSprite = isExpanded
            ? _openFolderNonEmptySprite
            : (_cachedDirectoryHasContent ? _closedFolderNonEmptySprite : _closedFolderEmptySprite);

        if (_lastAppliedSprite != targetSprite)
        {
            _folderIconImage.sprite = targetSprite;
            _lastAppliedSprite = targetSprite;
        }

        _lastObservedData = data;
        _lastObservedExpandedState = isExpanded;
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
        _lastObservedExpandedState = false;
        _lastObservedChildCount = 0;
        _hasCachedDirectoryContent = false;
        _cachedDirectoryHasContent = false;
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

        EnsureDirectoryMetadataReflection();

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

    private static void EnsureDirectoryMetadataReflection()
    {
        if (s_directoryMetadataType != null)
        {
            return;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly assembly = assemblies[i];
            Type metadataType = assembly.GetType("Oasis.LayoutEditor.Panels.PanelProject+DirectoryMetadata");

            if (metadataType == null)
            {
                continue;
            }

            s_directoryMetadataType = metadataType;
            s_directoryPathProperty = metadataType.GetProperty("DirectoryPath", BindingFlags.Public | BindingFlags.Instance);
            break;
        }
    }
}
