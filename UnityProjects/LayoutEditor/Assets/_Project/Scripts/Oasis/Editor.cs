using Oasis.MFME.Extract;
using SFB;
using System.Collections;
using UnityEngine;
using MFMEExtract;
using Oasis.Layout;
using Oasis.MFME;
using Oasis.LayoutEditor;
using Oasis.MAME;

namespace Oasis
{
    public class Editor : MonoBehaviour
    {
        public UIController UIController;
        public MFMEExtractImporter MFMEExtractImporter; // TODO can prob get rid of this class
        public MameController MameController;

        public EditorComponentBackground EditorComponentBackgroundPrefab;
        public EditorComponentLamp EditorComponentLampPrefab;

        public Zoom Zoom;

        public LayoutObject Layout
        {
            get;
            set;
        } = null;

        public ExtractImporter ExtractImporter
        {
            get;
            private set;
        } = null;


        private void Awake()
        {
            ExtractImporter = new ExtractImporter(this);
        }

        private void Start()
        {
            AddListeners();
        }

        private void Update()
        {

        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            StartCoroutine(AddListenersCoroutine());
        }

        private IEnumerator AddListenersCoroutine()
        {
            yield return new WaitUntil(() =>
                UIController != null
                && UIController.RootUI != null
                && UIController.RootUI.ViewModelMenu != null);

            ExtractImporter.OnImportComplete.AddListener(OnImportComplete);
        }

        private void RemoveListeners()
        {
            ExtractImporter.OnImportComplete.RemoveListener(OnImportComplete);
        }

        public void OnFileImportClick()
        {
            //string[] paths = StandaloneFileBrowser.OpenFolderPanel("MFME Extract folder", null, false);
            //ExtensionFilter extensionFilter = new ExtensionFilter("JSON files", "json");
            
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", null, "json", false);

            if (paths.Length > 0 && paths[0] != null && paths[0].Length > 0)
            {
                Extractor.LoadLayout(paths[0]);
            }
        }

        public void OnEmulationStartClick()
        {
            MameController.StartMame();
        }

        public void OnEmulationStopClick()
        {
            MameController.StopMame();
        }

        public void OnEmulationPauseClick()
        {
            MameController.PauseMame();
        }

        public void OnEmulationResetClick()
        {
            MameController.ResetMame();
        }

        private void OnImportComplete()
        {
            UIController.RebuildUI();
        }

    }
}
