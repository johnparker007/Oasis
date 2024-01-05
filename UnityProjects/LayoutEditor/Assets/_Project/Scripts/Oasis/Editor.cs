using SFB;
using System.Collections;
using UnityEngine;
using Oasis.MFME;
using Oasis.LayoutEditor;
using Oasis.MAME;
using UnityEngine.Events;

namespace Oasis
{
    public class Editor : MonoBehaviour
    {
        public UIController UIController;
        public MameController MameController;

        public EditorComponentBackground EditorComponentBackgroundPrefab;
        public EditorComponentLamp EditorComponentLampPrefab;
        public EditorComponentReel EditorComponentReelPrefab;
        public EditorComponent7Segment EditorComponentSevenSegmentPrefab;
        public EditorComponentAlpha EditorComponentAlphaPrefab;
        // not sure will need this:
        public EditorComponent16SemicolonSegment EditorComponent16SemicolonSegmentPrefab;

        public Zoom Zoom;

        public UnityEvent<LayoutObject> OnLayoutSet = new UnityEvent<LayoutObject>();

        public LayoutObject Layout
        {
            get
            {
                return _layout;
            }
            set
            {
                _layout = value;
                OnLayoutSet?.Invoke(_layout);
            }
        }

        public ExtractImporter ExtractImporter
        {
            get;
            private set;
        } = null;

        private LayoutObject _layout = null;


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

        private void OnImportComplete()
        {
            UIController.RebuildUI();
        }

    }
}
