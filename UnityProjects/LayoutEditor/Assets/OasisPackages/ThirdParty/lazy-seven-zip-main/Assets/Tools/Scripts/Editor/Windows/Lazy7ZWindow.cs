#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LazyJedi.SevenZip
{
    public class Lazy7ZWindow : EditorWindow
    {
        #region WINDOW

        public static Lazy7ZWindow Window;

        [MenuItem("Lazy-Jedi/Tools/Lazy 7Zip", priority = 401)]
        public static void OpenWindow()
        {
            Window = GetWindow<Lazy7ZWindow>("Lazy 7Zip");
            Window.Show();
        }

        #endregion

        #region FIELDS

        #endregion

        #region UNITY METHODS

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Work In Progress!");
        }

        #endregion

        #region METHODS

        #endregion
    }
#endif
}