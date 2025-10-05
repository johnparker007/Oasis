using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.UI;

public class HierarchyFieldProject : HierarchyField
{
    [SerializeField] private Image _folderIconImage;

    [SerializeField] private Sprite _closedFolderEmptySprite;
    [SerializeField] private Sprite _closedFolderNonEmptySprite;
    [SerializeField] private Sprite _openFolderNonEmptySprite;
    
}
