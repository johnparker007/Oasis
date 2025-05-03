using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.FileOperations;
using System.IO;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnFileNewProject()
        {
            // This would take the user to the Projects Hub
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileOpenProject()
        {
            // This would take the user to the Projects Hub
            Debug.LogWarning("Not yet implemented!");
        }

        public void OnFileSaveProject()
        {
            Editor.Instance.ProjectController.SaveProject();

            // Original test export->import code:
            //OasisExporter exporter = new OasisExporter(new FileSystemWrapper(), new ProjectSettingsValidator(), new LayoutValidator());
            //exporter.Export(Editor.Instance.Project, string.Format("e:\\SavedLayout2\\{0}.json", Editor.Instance.Project.Settings.Mame.RomName));
            //Oasis.Import.Importer importer = new Oasis.Import.Importer();
            //Editor.Instance.Project = importer.Import(string.Format("e:\\SavedLayout2\\{0}.json", Editor.Instance.Project.Settings.Mame.RomName));
        }

        public void OnFileImportMfme()
        {
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", null, "json", false);

            if (paths.Length > 0 && paths[0] != null && paths[0].Length > 0)
            {
                Extractor.LoadLayout(paths[0]);
            }
        }

        public void OnFileExportMAME()
        {
            // TODO handling paths - will there be a file requester to select export path, or
            // will it live in a standardised location in the Project file structure, for
            // instance: $ProjectRoot/MameExport/*
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
