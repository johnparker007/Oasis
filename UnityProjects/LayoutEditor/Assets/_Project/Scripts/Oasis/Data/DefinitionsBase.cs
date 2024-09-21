using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Data
{
    [System.Serializable]
    public class DefinitionsBase : ScriptableObject
    {
        public List<DefinitionBase> Definitions = new List<DefinitionBase>();

        public DefinitionBase GetDefinition(string name)
        {
            return Definitions.Find(definition => definition.Name == name);
        }
    }
}


