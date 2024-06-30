using UnityEngine;

namespace Oasis.UI.ContextMenu.Data
{
    [CreateAssetMenu(fileName = "ContextMenuItemData", menuName = "Oasis/Data/ContextMenuItem", order = 1)]
    public class ContextMenuItemDefinition : ContextMenuDefinitionBase
    {
        public string EventName;
        public bool Toggle;
        public string DisplayText;
    }
}
