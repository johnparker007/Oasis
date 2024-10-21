using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.FileOperations;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnFileNew()
        {
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileOpen()
        {
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileSave()
        {
            OasisExporter exporter = new OasisExporter(new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());
            exporter.Export(Editor.Instance.Project, string.Format("e:\\SavedLayout\\{0}.json", Editor.Instance.Project.Settings.Mame.RomName));
        }

        public void OnFileSaveAs()
        {
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileImportMfme()
        {
            //string[] paths = StandaloneFileBrowser.OpenFolderPanel("MFME Extract folder", null, false);
            //ExtensionFilter extensionFilter = new ExtensionFilter("JSON files", "json");

            string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", null, "json", false);

            if (paths.Length > 0 && paths[0] != null && paths[0].Length > 0)
            {
                Extractor.LoadLayout(paths[0]);
            }
        }

        public void OnFileExportMAME()
        {
            MameExporter exporter = new MameExporter(new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());
            exporter.Export(Editor.Instance.Project, "e:\\exported.lay");
        }

        public void OnFileClose()
        {
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileExit()
        {
            // TODO - block exit if project has unsaved changes
            // also - logic to be placed in a project closing handler, which will trigger same modal dialog
            // if there are unsaved changes on opening new project, creating new project, closing project etc.
            Application.Quit();
        }
    }
}
