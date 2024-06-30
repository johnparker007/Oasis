using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu.Data
{
    [CreateAssetMenu(fileName = "ContextMenuData", menuName = "Oasis/Data/ContextMenu", order = 1)]
    public class ContextMenuDefinition : ContextMenuDefinitionBase
    {
        public List<ContextMenuDefinitionBase> Elements;
    }
}
