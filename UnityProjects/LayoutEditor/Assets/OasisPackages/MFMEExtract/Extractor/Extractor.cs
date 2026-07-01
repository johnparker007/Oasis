using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using MFMEExtract;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Events;
using Oasis.MfmeTools.Shared.Extract;
using System;
//using Newtonsoft.Json;

public static class Extractor
{
    public static Layout Layout = null;
    public static string LayoutDirectoryPath = null;

    public static UnityEvent<Layout> OnLayoutLoaded = new UnityEvent<Layout>();

    //    public static MFMEExtract.ExtractComponentBase LastComponent
    //    {
    //        get
    //        {
    //            return Layout.Components.Last();
    //        }
    //    }

    //    public static void NewLayout(string asName)
    //    {
    //        Layout = new Layout() { ASName = asName };
    //    }

    //    public static void LoadLayout(MachineConfigurationData machineConfigurationData)
    //    {
    //        string outputDirectoryPath = Converter.GetOutputDirectoryPath(machineConfigurationData);
    //        LoadLayout(outputDirectoryPath, machineConfigurationData.Name);
    //    }

    //public static void LoadLayout(string path)
    //{
    //    const string kTempFilename = "";
    //    LoadLayout(path, kTempFilename);
    //}


    //    public static void SaveLayout(string directoryPath)
    //    {
    //        string json = JsonConvert.SerializeObject(Layout, Formatting.Indented, new JsonSerializerSettings 
    //            { 
    //                TypeNameHandling = TypeNameHandling.Auto //, ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    //            });

    //        string filePath = Path.Combine(directoryPath, Layout.ASName + ".json");

    //        File.WriteAllText(filePath, json);
    //    }

    //    private static void LoadLayout(string directoryPath, string asName)
    public static void LoadLayout(string filePath)
    {
        LayoutDirectoryPath = Path.GetDirectoryName(filePath);

        string json = File.ReadAllText(filePath);

        // change the MfmeTools assembly string to the one that Unity expects:
        json = json.Replace("MfmeTools\",", "Assembly-CSharp\",");

        Layout = JsonConvert.DeserializeObject<Layout>(
            json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

        OnLayoutLoaded?.Invoke(Layout);
    }
}
