using UnityEngine;

namespace Oasis.Data
{
    using System.IO;
    using UnityEditor;

    public class DefinitionsBaseEditor : Editor
    {
        protected DefinitionsBase _target = default;

        private void OnEnable()
        {
            _target = (DefinitionsBase)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild"))
            {
                Rebuild(_target);
            }
            EditorGUILayout.Space();

            base.OnInspectorGUI();
        }

        public static void Rebuild(DefinitionsBase definitions)
        {
            definitions.Definitions.Clear();

            string searchDirectoryPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(definitions));

            string[] definitionGuids = AssetDatabase.FindAssets("t:DefinitionBase", new[] { searchDirectoryPath });
            foreach (string definitionGuid in definitionGuids)
            {
                string definitionAssetPath = AssetDatabase.GUIDToAssetPath(definitionGuid);
                DefinitionBase definition =
                    (DefinitionBase)AssetDatabase.LoadAssetAtPath(definitionAssetPath, typeof(DefinitionBase));
                definitions.Definitions.Add(definition);
            }

            EditorUtility.SetDirty(definitions);
            AssetDatabase.SaveAssets();
        }
    }
}
