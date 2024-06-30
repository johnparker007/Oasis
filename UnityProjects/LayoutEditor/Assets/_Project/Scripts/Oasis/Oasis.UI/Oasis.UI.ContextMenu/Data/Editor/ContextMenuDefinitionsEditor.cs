using UnityEngine;

namespace Oasis.UI.ContextMenu.Data
{
    using System.IO;
    using UnityEditor;

    [CustomEditor(typeof(ContextMenuDefinitions))]
    public class ContextMenuDefinitionsEditor : Editor
    {
        private ContextMenuDefinitions _target = null;

        private void OnEnable()
        {
            _target = (ContextMenuDefinitions)target;
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

        public static void Rebuild(ContextMenuDefinitions contextMenuDefinitions)
        {
            contextMenuDefinitions.Definitions.Clear();

            string searchDirectoryPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(contextMenuDefinitions));

            string[] definitionGuids = AssetDatabase.FindAssets("t:ContextMenuDefinition", new[] { searchDirectoryPath });
            foreach (string definitionGuid in definitionGuids)
            {
                string definitionAssetPath = AssetDatabase.GUIDToAssetPath(definitionGuid);
                ContextMenuDefinition definition =
                    (ContextMenuDefinition)AssetDatabase.LoadAssetAtPath(definitionAssetPath, typeof(ContextMenuDefinition));
                contextMenuDefinitions.Definitions.Add(definition);
            }

            EditorUtility.SetDirty(contextMenuDefinitions);
            AssetDatabase.SaveAssets();
        }
    }
}
