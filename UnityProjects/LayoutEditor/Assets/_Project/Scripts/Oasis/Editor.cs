using SFB;
using System.Collections;
using UnityEngine;
using Oasis.MFME;
using Oasis.LayoutEditor;
using Oasis.MAME;
using UnityEngine.Events;
using Oasis.Project;


namespace Oasis
{
    public class Editor : MonoBehaviour
    {
        public UIController UIController;
        public MameController MameController;
        public MameMpu4ChrSourceCodeLookup MameMpu4ChrSourceCodeLookup;

        public EditorComponentBackground EditorComponentBackgroundPrefab;
        public EditorComponentLamp EditorComponentLampPrefab;
        public EditorComponentReel EditorComponentReelPrefab;
        public EditorComponent7Segment EditorComponentSevenSegmentPrefab;
        public EditorComponentAlpha EditorComponentAlphaPrefab;
        public EditorComponentOverlay EditorComponentOverlayPrefab;

        // not sure will need this:
        public EditorComponent16SemicolonSegment EditorComponent16SemicolonSegmentPrefab;

        public EditorPanel EditorPanelMFMEImport;
        public EditorPanel EditorPanelFull;

        public ProjectData Project
        {
            get;
            set;
        }


        public UnityEvent<LayoutObject> OnLayoutSet = new UnityEvent<LayoutObject>();

        public static Editor Instance
        {
            get;
            private set;
        } = null;


        public ExtractImporter ExtractImporter
        {
            get;
            private set;
        } = null;


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(Instance);
            }
            else if (this != Instance)
            {
                Destroy(this);
                return;
            }

            ExtractImporter = new ExtractImporter();

            Project = new ProjectData();
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
            yield return new WaitUntil(() => UIController != null);

            ExtractImporter.OnImportComplete.AddListener(OnImportComplete);

// JP HACK:
OnFileImportClick();
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

        public void OnFileExportClick()
        {
            Debug.LogError("TODO OnFileExportClick");
        }

        public void OnEmulationStartClick()
        {
            MameController.StartMame(false);
        }

        public void OnEmulationExitClick()
        {
            MameController.ExitMame();
        }

        public void OnEmulationPauseClick()
        {
            MameController.Pause();
        }

        public void OnEmulationResumeClick()
        {
            MameController.Resume();
        }

        public void OnEmulationSoftResetClick()
        {
            MameController.SoftReset();
        }

        public void OnEmulationHardResetClick()
        {
            MameController.HardReset();
        }

        public void OnEmulationThrottledClick()
        {
            MameController.SetThrottled(true);
        }

        public void OnEmulationUnthrottledClick()
        {
            MameController.SetThrottled(false);
        }

        public void OnEmulationStateLoadClick()
        {
            MameController.StateLoad();
        }

        public void OnEmulationStateSaveClick()
        {
            MameController.StateSave();
        }

        public void OnEmulationStateSaveAndExitClick()
        {
            MameController.StateSaveAndExit();
        }

        public void OnEmulationStartAndStateLoadClick()
        {
            MameController.StartMame(true);
        }

        public void OnMfmeExtractClick()
        {
        }

        public void OnMfmeRemapLampsClick()
        {
            UIController.ShowMfmeRemapLampsForm();
        }

        public void OnHelpAboutClick()
        {
        }

        private void OnImportComplete()
        {
        }

    }
}
