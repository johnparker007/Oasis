using NativeMenuBar.Core;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NativeMenuBar.Editor
{
    [CustomEditor(typeof(MenuBar))]
    public class MenuBarInspector : UnityEditor.Editor
    {
        private string headerString = 
$@"
//
// MACHINE-GENERATED CODE - DO NOT MODIFY BY HAND!
//
// NOTE: You definitely SHOULD commit this file to source control!!!
//
// To regenerate this file, select MenuBar component, look
// in the custom Inspector window and press the Generate Editor menu.
//

using System.Linq;

";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var codeGenFile = Path.Combine(Application.dataPath, "NativeMenuBar", "Editor", "NativeMenuBarEditor_AutoGen.cs");

            if(GUILayout.Button("Generate Editor menu"))
            {
                var stringBuilder = new StringBuilder(headerString);
                var menuBar = (MenuBar)target;

                menuBar.SetupMenuItemRoot();

                int separatorCount = 0;
                
                stringBuilder.AppendLine($"public static class NativeMenuBar_AutoGen{Environment.NewLine}{{{Environment.NewLine}");
                stringBuilder.AppendLine($"    private static NativeMenuBar.Core.MenuBar menubar = UnityEngine.Object.FindObjectOfType<NativeMenuBar.Core.MenuBar>();");
                foreach(var menuItem in  menuBar.MenuItems.Where(item => item.MenuItems.Count == 0))
                {
                    var commandName = menuItem.Shortcut != ' '
                        ? $"{menuItem.FullPath} _{menuItem.Shortcut}"
                        : $"{menuItem.FullPath}";

                    string menuItemFullPathSeparatorFixed = SanitizeFolderName(menuItem.FullPath);
                    string commandNameSeparatorFixed = commandName;
                    if(menuItemFullPathSeparatorFixed.EndsWith("-"))
                    {
                        menuItemFullPathSeparatorFixed = menuItemFullPathSeparatorFixed.Replace("-", "_");
                        menuItemFullPathSeparatorFixed += separatorCount;
                        for (int spaceCharCount = 0; spaceCharCount < separatorCount; ++spaceCharCount)
                        {
                            commandNameSeparatorFixed += "-";
                        }
                        //commandNameSeparatorFixed += separatorCount;

                        separatorCount++;
                    }

                    stringBuilder.AppendLine($"    [UnityEditor.MenuItem(\"NativeMenuBar/{commandNameSeparatorFixed}\")]");
                    stringBuilder.AppendLine($"    private static void {menuItemFullPathSeparatorFixed}()");
                    stringBuilder.AppendLine($"    {{");
                    stringBuilder.AppendLine($"        menubar.MenuItems.Single(item => item.FullPath == \"{menuItem.FullPath}\").Action.Invoke();");
                    stringBuilder.AppendLine($"    }}");
                    stringBuilder.AppendLine($"    [UnityEditor.MenuItem(\"NativeMenuBar/{commandNameSeparatorFixed}\", true)]");
                    stringBuilder.AppendLine($"    private static bool {menuItemFullPathSeparatorFixed}Validate()");
                    stringBuilder.AppendLine($"    {{");
                    stringBuilder.AppendLine($"        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == \"{menuItem.FullPath}\").IsInteractable;");
                    stringBuilder.AppendLine($"    }}");
                }
                stringBuilder.AppendLine($"{Environment.NewLine}}}");

                if(!Directory.Exists(Path.GetDirectoryName(codeGenFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(codeGenFile));
                }
                File.WriteAllText(codeGenFile, stringBuilder.ToString());

                AssetDatabase.Refresh();
            }
        }

        // https://gist.github.com/raducugheorghe/66e4e44b69caf2e4c7ab
        public static string SanitizeFolderName(string name)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(name, "").Replace(" ", string.Empty);
        }
    }
}
