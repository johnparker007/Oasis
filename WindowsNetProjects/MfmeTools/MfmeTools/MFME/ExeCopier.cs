using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.MFME
{
    public class ExeCopier
    {
        private struct PotentialPathData
        {
            public string Path;
            public bool SearchCurrentAndUp;
            public bool SearchCurrentAndDown;
        }

        private static readonly byte[] kMfmeExeHash =
        {
            77, 124, 162, 246, 210, 160, 243, 137, 255, 77, 97, 53, 248, 76, 3, 108, 45, 184, 33, 185, 15, 85, 58, 176, 237, 87, 142, 24, 23, 68, 10, 247
        };


        private List<PotentialPathData> _initialPotentialPathsData = new List<PotentialPathData>();
        private List<string> _foundLinkedPathsContainingMFMEString = new List<string>();

        private List<string> _recursiveSearchedDownPaths = new List<string>();
        private List<string> _recursiveSearchedUpPaths = new List<string>();

        private List<string> _searchedPaths = new List<string>();

        private string _matchedLatestMFMEExePath = null;


        public void Initialise()
        {
            TryToCopyMFMEExeIfNotPresent();
        }

        private void TryToCopyMFMEExeIfNotPresent()
        {
            if (ExeHelper.IsLatestMFMEExePresent(kMfmeExeHash))
            {
                OutputLog.Log("MFMEExeCopier - latest MFME exe already present, doing nothing.");
                return;
            }
            
            OutputLog.LogWarning("MFMEExeCopier - latest MFME exe not present, attempting to find...");

            FindInitialPotentialPaths();

            SearchInitialPotentialPaths();

            if (_matchedLatestMFMEExePath == null)
            {
                SearchFoundLinkedPathsContainingMFMEString();
            }

            if (_matchedLatestMFMEExePath != null)
            {
                OutputLog.Log("MFMEExeCopier - MFME exe found, copying from " + _matchedLatestMFMEExePath);
                File.Copy(_matchedLatestMFMEExePath, ExeHelper.MFMEExePath, true);
                OutputLog.Log("MFMEExeCopier - called copy, now MFMEExeHelper.IsLatestMFMEExePresent returning " + ExeHelper.IsLatestMFMEExePresent(kMfmeExeHash));
            }
            else
            {
                OutputLog.LogError("MFMEExeCopier - MFME exe NOT found!  Search paths:");
                foreach (string searchedPath in _searchedPaths)
                {
                    OutputLog.Log(searchedPath);
                }
            }
        }

        private void FindInitialPotentialPaths()
        {
            _initialPotentialPathsData.Add(new PotentialPathData()
            {
                Path = GetRegistryStringData("InstallPath"),
                SearchCurrentAndUp = true,
                SearchCurrentAndDown = true
            });

            _initialPotentialPathsData.Add(new PotentialPathData()
            {
                Path = GetRegistryStringData("PathName"),
                SearchCurrentAndUp = true,
                SearchCurrentAndDown = true
            });

            _initialPotentialPathsData.Add(new PotentialPathData()
            {
                Path = ExeHelper.MFMERootPath,
                SearchCurrentAndUp = true,
                SearchCurrentAndDown = true
            });

            _initialPotentialPathsData.Add(new PotentialPathData()
            {
                Path = GetRegistryStringData("BrowseDir"),
                SearchCurrentAndUp = true,
                SearchCurrentAndDown = true
            });

            _initialPotentialPathsData.Add(new PotentialPathData()
            {
                Path = GetRegistryStringData("ExportDir"),
                SearchCurrentAndUp = true,
                SearchCurrentAndDown = true
            });

            List<string> historyPaths = GetHistoryPaths();
            foreach (string historyPath in historyPaths)
            {
                _initialPotentialPathsData.Add(new PotentialPathData()
                {
                    Path = historyPath,
                    SearchCurrentAndUp = true,
                    SearchCurrentAndDown = true
                });
            }

            List<string> additionalFolders = GetAdditionalFolders(); // do this later as including SearchCurrentAndDown which is slow for what are probaly parent layout folders
            foreach (string additionalFolder in additionalFolders)
            {
                _initialPotentialPathsData.Add(new PotentialPathData()
                {
                    Path = additionalFolder,
                    SearchCurrentAndUp = true,
                    SearchCurrentAndDown = true
                });
            }
        }

        private string GetRegistryStringData(string key)
        {
            string registryStringData = (string)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\CJW\\MFME", key, null);
            if (registryStringData == null)
            {
                OutputLog.LogWarning("MFMEExeCopier - couldn't get string data for key [" + key + "] from Windows Registry");
            }

            return registryStringData;
        }

        private List<string> GetAdditionalFolders()
        {
            // 'AdditionalFolders' is semicolon-seperated values e.g: C:\projects\Arcade_MFME\Layouts;C:\downloads

            List<string> additionalFolders = new List<string>();

            string additionalFoldersSemicolonSeperated = GetRegistryStringData("AdditionalFolders");
            if (additionalFoldersSemicolonSeperated != null)
            {
                additionalFolders = additionalFoldersSemicolonSeperated.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return additionalFolders;
        }

        private List<string> GetHistoryPaths()
        {
            // in registry as history0, history1 ... history9

            List<string> historyPaths = new List<string>();

            for (int historyPathIndex = 0; historyPathIndex <= 9; ++historyPathIndex)
            {
                string historyPath = GetRegistryStringData("history" + historyPathIndex);
                if (historyPath != null)
                {
                    historyPaths.Add(historyPath);
                }
            }

            return historyPaths;
        }

        private void SearchInitialPotentialPaths()
        {
            foreach (PotentialPathData potentialPathData in _initialPotentialPathsData)
            {
                SearchInitialPotentialPath(potentialPathData);

                if (_matchedLatestMFMEExePath != null)
                {
                    return;
                }
            }
        }

        private void SearchInitialPotentialPath(PotentialPathData potentialPathData)
        {
            if (potentialPathData.Path == null
                || potentialPathData.Path.Length == 0
                || !Directory.Exists(potentialPathData.Path))
            {
                return;
            }

            if (potentialPathData.SearchCurrentAndDown)
            {
                SearchPotentialPathCurrentAndDown(potentialPathData);
            }

            if (potentialPathData.SearchCurrentAndUp)
            {
                SearchPotentialPathCurrentAndUp(potentialPathData);
            }
        }

        private void SearchPotentialPathCurrentAndDown(PotentialPathData potentialPathData)
        {
            DirectoryInfo potentialPathDirectoryInfo = new DirectoryInfo(potentialPathData.Path);

            RecursiveSearchDown(potentialPathDirectoryInfo);
        }

        private void SearchPotentialPathCurrentAndUp(PotentialPathData potentialPathData)
        {
            DirectoryInfo potentialPathDirectoryInfo = new DirectoryInfo(potentialPathData.Path);

            RecursiveSearchUp(potentialPathDirectoryInfo, true);
        }

        private void RecursiveSearchDown(DirectoryInfo directoryInfo)
        {
            if (!directoryInfo.Exists)
            {
                return;
            }

            if (_matchedLatestMFMEExePath != null)
            {
                return;
            }

            if (_recursiveSearchedDownPaths.Contains(directoryInfo.FullName))
            {
                return;
            }
            _recursiveSearchedDownPaths.Add(directoryInfo.FullName);

            foreach (DirectoryInfo subDirectoryInfo in directoryInfo.EnumerateDirectories())
            {
                RecursiveSearchDown(subDirectoryInfo);
            }

            if (_matchedLatestMFMEExePath == null)
            {
                string mfmeExePath = FindMFMEExePath(directoryInfo.FullName);
                if (mfmeExePath != null)
                {
                    _matchedLatestMFMEExePath = mfmeExePath;
                    return;
                }
            }
        }

        private void RecursiveSearchUp(DirectoryInfo directoryInfo, bool captureLinkedPathsContainingMFMEString)
        {
            if (!directoryInfo.Exists)
            {
                return;
            }

            if (_matchedLatestMFMEExePath != null)
            {
                return;
            }

            if (_recursiveSearchedUpPaths.Contains(directoryInfo.FullName))
            {
                return;
            }
            _recursiveSearchedUpPaths.Add(directoryInfo.FullName);

            DirectoryInfo parentDirectoryInfo = directoryInfo.Parent;
            if (parentDirectoryInfo != null)
            {
                RecursiveSearchUp(parentDirectoryInfo, captureLinkedPathsContainingMFMEString);
            }

            if (captureLinkedPathsContainingMFMEString)
            {
                FindDirectoriesContainingMFMEString(directoryInfo);
            }

            if (_matchedLatestMFMEExePath == null)
            {
                string mfmeExePath = FindMFMEExePath(directoryInfo.FullName);
                if (mfmeExePath != null)
                {
                    _matchedLatestMFMEExePath = mfmeExePath;
                    return;
                }
            }
        }

        private void FindDirectoriesContainingMFMEString(DirectoryInfo directoryInfo)
        {
            foreach (string directoryPath in Directory.GetDirectories(directoryInfo.FullName, "*", SearchOption.TopDirectoryOnly))
            {
                bool containsMFMEString = directoryPath.IndexOf("mfme", StringComparison.OrdinalIgnoreCase) >= 0;
                if (containsMFMEString)
                {
                    _foundLinkedPathsContainingMFMEString.Add(directoryPath);
                }
            }
        }

        private void SearchFoundLinkedPathsContainingMFMEString()
        {
            foreach (string linkedPathContainingMFMEString in _foundLinkedPathsContainingMFMEString)
            {
                if (_matchedLatestMFMEExePath != null)
                {
                    return;
                }

                DirectoryInfo potentialPathDirectoryInfo = new DirectoryInfo(linkedPathContainingMFMEString);
                RecursiveSearchDown(potentialPathDirectoryInfo);
                RecursiveSearchUp(potentialPathDirectoryInfo, false);
            }
        }

        private string FindMFMEExePath(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }

            if (_searchedPaths.Contains(directoryPath))
            {
                return null;
            }
            _searchedPaths.Add(directoryPath);

            DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);
            foreach (FileInfo exeFileInfo in directoryInfo.GetFiles("*.exe"))
            {
                if (ExeHelper.IsLatestExe(exeFileInfo.FullName, kMfmeExeHash))
                {
                    return exeFileInfo.FullName;
                }
            }

            return null;
        }

    }
}
