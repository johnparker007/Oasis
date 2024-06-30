using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.ContextMenu.Data
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "ContextMenus", menuName = "Oasis/Data/ContextMenus")]
    public class ContextMenuDefinitions : ScriptableObject
    {
        public List<ContextMenuDefinition> Definitions = new List<ContextMenuDefinition>();
    }
}


