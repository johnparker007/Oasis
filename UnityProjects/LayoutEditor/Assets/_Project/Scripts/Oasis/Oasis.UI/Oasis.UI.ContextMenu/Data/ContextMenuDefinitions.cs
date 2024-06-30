using Oasis.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oasis.UI.ContextMenu.Data
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "ContextMenus", menuName = "Oasis/Data/ContextMenus")]
    public class ContextMenuDefinitions : DefinitionsBase
    {
        public List<ContextMenuDefinition> GetMenus()
        {
            return Definitions.OfType<ContextMenuDefinition>().ToList();
        }

        public List<ContextMenuItemDefinition> GetMenuItems()
        {
            return Definitions.OfType<ContextMenuItemDefinition>().ToList();
        }

        public ContextMenuDefinition GetMenu(string name)
        {
            return GetMenus().Find(x => x.Name == name);
        }

        public ContextMenuItemDefinition GetMenuItem(string name)
        {
            return GetMenuItems().Find(x => x.Name == name);
        }
    }
}


