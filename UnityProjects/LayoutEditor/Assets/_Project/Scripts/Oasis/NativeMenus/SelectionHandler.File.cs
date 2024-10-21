using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnFileNew()
        {

        }

        public void OnFileOpen()
        {

        }

        public void OnFileSave()
        {
            OasisExporter exporter = new OasisExporter(new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());
            exporter.Export(Editor.Instance.Project, string.Format("e:\\SavedLayout\\{0}.json", Editor.Instance.Project.Settings.Mame.RomName));
        }

        public void OnFileSaveAs()
        {

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

        }

        public void OnFileExit()
        {
            
        }
    }
}
