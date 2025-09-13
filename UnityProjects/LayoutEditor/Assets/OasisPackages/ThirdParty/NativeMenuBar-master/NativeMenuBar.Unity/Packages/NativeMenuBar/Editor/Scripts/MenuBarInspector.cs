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

            MenuBar menuBar = (MenuBar)target;

            GiveSeperatorsUniqueHyphenCounts(menuBar);

            var codeGenFile = Path.Combine(Application.dataPath, "NativeMenuBar", "Editor", "NativeMenuBarEditor_AutoGen.cs");

            if (GUILayout.Button("Generate Editor menu"))
            {
                var stringBuilder = new StringBuilder(headerString);
                

                menuBar.SetupMenuItemRoot();

                int separatorCount = 0;
                int unityMenuItemPriority = 0; // JP another hack just to try get this grotty NativeMenuBar system working better
                
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

                    // JP more hackiness from me, until I rip this system out and buil a better one:
                    if (commandNameSeparatorFixed.Contains("-"))
                    {
                        //   priority:
                        //     The order to display the menu item in a menu. If a menu item has a priority value
                        //     that is 10 or greater than the item that precedes it, a separator line divides
                        //     the two menu items from each other in the menu.
                        unityMenuItemPriority += 10;
                        continue;
                    }

                    stringBuilder.AppendLine($"    [UnityEditor.MenuItem(\"NativeMenuBar/{commandNameSeparatorFixed}\", priority = {unityMenuItemPriority})]");
                    stringBuilder.AppendLine($"    private static void {menuItemFullPathSeparatorFixed}()");
                    stringBuilder.AppendLine($"    {{");

                    //                    stringBuilder.AppendLine($"        menubar.MenuItems.Single(item => item.FullPath == \"{menuItem.FullPath}\").Action.Invoke();");
                    stringBuilder.AppendLine($"        menubar.MenuItems.Single(item => item.FullPath == \"{commandNameSeparatorFixed}\").Action.Invoke();");

                    stringBuilder.AppendLine($"    }}");
                    //stringBuilder.AppendLine($"    [UnityEditor.MenuItem(\"NativeMenuBar/{commandNameSeparatorFixed}\", true)]");
                    stringBuilder.AppendLine($"    [UnityEditor.MenuItem(\"NativeMenuBar/{commandNameSeparatorFixed}\", true, {unityMenuItemPriority})]");
                    stringBuilder.AppendLine($"    private static bool {menuItemFullPathSeparatorFixed}Validate()");
                    stringBuilder.AppendLine($"    {{");

                    //                    stringBuilder.AppendLine($"        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == \"{menuItem.FullPath}\").IsInteractable;");
                    stringBuilder.AppendLine($"        return UnityEngine.Application.isPlaying && menubar.MenuItems.Single(item => item.FullPath == \"{commandNameSeparatorFixed}\").IsInteractable;");

                    stringBuilder.AppendLine($"    }}");

                    ++unityMenuItemPriority;
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

        // JP added as part of making dividers work without errors in Unity Editor
        // TODO this needs to be recursive for nested menu levels, just goes one deep for now
        private static void GiveSeperatorsUniqueHyphenCounts(MenuBar menuBar)
        {
            int hyphenCount = 1;

            foreach(MenuRootItem menuRootItem in menuBar.MenuRoots)
            {
                foreach(Core.MenuItem menuItem in menuRootItem.MenuItems)
                {
                    if(menuItem.Name.StartsWith('-'))
                    {
                        menuItem.Name = new string('-', hyphenCount);
                        hyphenCount++;
                    }
                }
            }
        }
    }
}
