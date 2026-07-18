using System.IO;
using UnityEditor;
using UnityEngine;

namespace OasisPlayer.EditorTools
{
    public static class SaveRuntimeMaterial
    {
        [MenuItem("Oasis/Save Selected Runtime Material")]
        private static void SaveSelectedRuntimeMaterial()
        {
            var renderer = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Renderer>()
                : null;

            if (renderer == null)
            {
                Debug.LogError(
                    "Select a GameObject containing a Renderer with the runtime material.");
                return;
            }

            var sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                Debug.LogError("The selected Renderer has no materials.");
                return;
            }

            var sourceMaterial = sourceMaterials[0];
            if (sourceMaterial == null)
            {
                Debug.LogError("The selected material slot is empty.");
                return;
            }

            var defaultName = SanitizeFileName(sourceMaterial.name) + ".mat";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Runtime Material",
                defaultName,
                "mat",
                "Choose where to save the captured runtime material.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Clone the runtime material, including shader, properties,
            // keywords, render queue and other material state.
            var savedMaterial = new Material(sourceMaterial)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };

            AssetDatabase.CreateAsset(savedMaterial, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = savedMaterial;

            Debug.Log(
                "Saved runtime material '" + sourceMaterial.name +
                "' using shader '" +
                (sourceMaterial.shader != null
                    ? sourceMaterial.shader.name
                    : "<null>") +
                "' to '" + path + "'.");
        }

        private static string SanitizeFileName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value)
                ? "CapturedGltfMaterial"
                : value.Replace(" (Instance)", string.Empty);

            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidCharacter, '_');
            }

            return result;
        }
    }
}