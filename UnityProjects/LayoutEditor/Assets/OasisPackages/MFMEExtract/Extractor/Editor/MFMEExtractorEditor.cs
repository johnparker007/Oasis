using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.EditorCoroutines.Editor;

[CustomEditor(typeof(MFMEExtractor))]
public class MFMEExtractorEditor : Editor
{
    //public override void OnInspectorGUI()
    //{
    //    MFMEExtractor mfmeExtractor = target as MFMEExtractor;

    //    GUI.backgroundColor = Color.cyan;
    //    if (GUILayout.Button("EXTRACT FROM MFME"))
    //    {
    //        mfmeExtractor.gameObject.SetActive(true);
    //        mfmeExtractor.Converter.gameObject.SetActive(false);
    //        mfmeExtractor.MFMEDataLayoutBuilder.gameObject.SetActive(false);

    //        EditorApplication.ExecuteMenuItem("Edit/Play");
    //    }

    //    GUI.backgroundColor = Color.grey;
    //    base.OnInspectorGUI();
    //}

}

