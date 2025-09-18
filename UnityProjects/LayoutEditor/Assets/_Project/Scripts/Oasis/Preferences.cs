using System;
using System.IO;
using UnityEngine;

public class Preferences : MonoBehaviour
{
    public static string kPathOasisFolderName = "Oasis";
    public static string kPathProjectsFolderName = "Projects";

    public static string kKeyProjectsFolder = "ProjectsFolder";
    public static string kKeyMameVersion = "MameVersion";

    public const int kDefaultMameVersion = 239;

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

    public int MameVersion
    {
        get
        {
            int storedVersion = PlayerPrefs.GetInt(kKeyMameVersion, kDefaultMameVersion);
            return Mathf.Clamp(storedVersion, 0, 9999);
        }
        set
        {
            int sanitisedValue = Mathf.Clamp(value, 0, 9999);
            PlayerPrefs.SetInt(kKeyMameVersion, sanitisedValue);
        }
    }

    protected string DefaultOasisFolderPath
    {
        get
        {
            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string oasisFolderPath = Path.Combine(myDocumentsPath, kPathOasisFolderName);
            return oasisFolderPath;
        }

    }

    protected string DefaultProjectsFolderPath
    {
        get
        {
            string projectsFolderPath = Path.Combine(DefaultOasisFolderPath, kPathProjectsFolderName);
            return projectsFolderPath;
        }
    }

    public void ResetProjectsFolderToDocumentsFolder()
    {
        PlayerPrefs.SetString(kKeyProjectsFolder, DefaultProjectsFolderPath);
        PlayerPrefs.Save();
    }


}
