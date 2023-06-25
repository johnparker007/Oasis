using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.EditorCoroutines.Editor;

[CustomEditor(typeof(MFMEExtractorNew))]
public class MFMEExtractorNewEditor : Editor
{
    //private static List<MachineConfigurationData> _machineConfigurationDatas = null;
    //public override void OnInspectorGUI()
    //{
    //    MFMEExtractorNew mfmeExtractorNew = target as MFMEExtractorNew;

    //    if (GUILayout.Button("EXTRACT FROM MFME"))
    //    {
    //        mfmeExtractorNew.MFMEExtractor.gameObject.SetActive(true);
    //        mfmeExtractorNew.MFMEExtractor.Converter.gameObject.SetActive(false);
    //        mfmeExtractorNew.MFMEExtractor.MFMEDataLayoutBuilder.gameObject.SetActive(false);

    //        _machineConfigurationDatas = new List<MachineConfigurationData>();

    //        List<MFMELayoutFolderData> mfmeLayoutFolderDatas = new List<MFMELayoutFolderData>();
    //        if(mfmeExtractorNew.MfmeLayoutFolderDatas.Count > 0 && mfmeExtractorNew.MfmeLayoutFolderDatas[0] != null)
    //        {
    //            mfmeLayoutFolderDatas = mfmeExtractorNew.MfmeLayoutFolderDatas;
    //        }
    //        else
    //        {
    //            mfmeLayoutFolderDatas = MFMELayoutDatabase.GetMFMELayoutFolderDatas(mfmeExtractorNew.Platform);
    //        }

    //        List<MFMELayoutFolderData> alreadyExtractedLayouts = new List<MFMELayoutFolderData>();
    //        foreach (MFMELayoutFolderData mfmeLayoutFolderData in mfmeLayoutFolderDatas)
    //        {
    //            MFMELayoutData mfmeLayoutData = mfmeLayoutFolderData.AutoChosenMAMESourceLayout;

    //            string categoryGameNoSpacesDirectoryName =
    //                mfmeLayoutData.CategoryDirectoryName.Replace(" ", "")
    //                + Path.GetFileNameWithoutExtension(mfmeLayoutData.LayoutGamFileName);

    //            string targetGamePath = Path.Combine(
    //                MFMEExeHelper.EditorMFMERootPath,
    //                "Layouts", "_CLASSICS_For_MAME",
    //                categoryGameNoSpacesDirectoryName);

    //            MachineConfigurationData machineConfigurationData =
    //                mfmeLayoutData.CreateMachineConfigurationDataInstance(categoryGameNoSpacesDirectoryName);

    //            if (!Directory.Exists(targetGamePath))
    //            {
    //                Directory.CreateDirectory(targetGamePath);
    //            }
    //            FileHelper.CopyDirectory(mfmeLayoutData.LayoutDirectoryPath, targetGamePath, false);


    //            string[] layoutDirectoryFilepaths = Directory.GetFiles(mfmeLayoutData.LayoutDirectoryPath);
    //            bool spaceInFmlFilenameFound = false;
    //            foreach(string layoutDirectoryFilepath in layoutDirectoryFilepaths)
    //            {
    //                string layoutDirectoryFilename = Path.GetFileName(layoutDirectoryFilepath);
    //                if(Path.GetExtension(layoutDirectoryFilename) == ".fml"
    //                    && layoutDirectoryFilename.Contains(" "))
    //                {
    //                    spaceInFmlFilenameFound = true;
    //                }
    //            }

    //            //int y = 00;
    //            //int x = 3 / y;

    //            if(spaceInFmlFilenameFound)
    //            {
    //                Debug.LogError("Skipping - spaceInFmlFilenameFound");
    //                continue;
    //            }

    //            string extractCompletedMarkerFilePath = Path.Combine(
    //                Converter.GetOutputDirectoryPath(machineConfigurationData), MFMEExtractor.kExtractCompletedFilename);

    //            string extractFailedMarkerFilePath = Path.Combine(
    //                Converter.GetOutputDirectoryPath(machineConfigurationData), MFMEExtractor.kExtractFailedFilename);

    //            if ((!File.Exists(extractCompletedMarkerFilePath) || mfmeExtractorNew.IgnoreExtractCompletedMarker)
    //                && !File.Exists(extractFailedMarkerFilePath))
    //            {
    //                _machineConfigurationDatas.Add(machineConfigurationData);
    //                Debug.Log("Adding " + machineConfigurationData.MFMEGameFilename + " as ExtractCompleted/Failed marker file not found");
    //            }
    //            else
    //            {
    //                Debug.Log("Skipping " + machineConfigurationData.MFMEGameFilename + " as ExtractCompleted/Failed marker file found");
    //            }
    //        }

    //        mfmeExtractorNew.MFMEExtractor.MachineConfigurations = _machineConfigurationDatas.ToArray();
    //        EditorApplication.ExecuteMenuItem("Edit/Play");
    //    }

    //    base.OnInspectorGUI();
    //}

}

