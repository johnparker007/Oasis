using System;
using System.IO;
using UnityEngine;

public class Preferences : MonoBehaviour
{
    public static string kPathProjectsFolderName = "Projects";
    public static string kKeyProjectsFolder = "ProjectsFolder";

    public string ProjectsFolder
    {
        get
        {
            string path = PlayerPrefs.GetString(kKeyProjectsFolder);
            if(path == null || path.Length == 0)
            {
                ResetProjectsFolderToDocumentsFolder();
                path = PlayerPrefs.GetString(kKeyProjectsFolder);
            }

            return path;
        }
        set
        {
            PlayerPrefs.SetString(kKeyProjectsFolder, value);
        }
    }

    protected string DefaultProjectsFolderPath
    {
        get
        {
            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string projectsFolderPath = Path.Combine(myDocumentsPath, kPathProjectsFolderName);
            return projectsFolderPath;
        }
    }

    public void ResetProjectsFolderToDocumentsFolder()
    {
        PlayerPrefs.SetString(kKeyProjectsFolder, DefaultProjectsFolderPath);
        PlayerPrefs.Save();
    }


}
