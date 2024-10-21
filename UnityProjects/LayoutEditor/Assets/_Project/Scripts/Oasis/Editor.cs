using SFB;
using System.Collections;
using UnityEngine;
using Oasis.MFME;
using Oasis.LayoutEditor;
using Oasis.Layout;
using Oasis.MAME;
using UnityEngine.Events;
using Oasis.Project;
using System.Collections.Generic;
using Oasis.Export;
using Oasis.UI;
using HSVPicker;

namespace Oasis
{
    public class Editor : MonoBehaviour
    {
        public UIController UIController;
        public MameController MameController;
        public SelectionController SelectionController;
        public InspectorController InspectorController;
        public FontManager FontManager;
        public ColorPicker ColorPicker;
        public MameMpu4ChrSourceCodeLookup MameMpu4ChrSourceCodeLookup;

        public EditorComponentBackground EditorComponentBackgroundPrefab;
        public EditorComponentLamp EditorComponentLampPrefab;
        public EditorComponentReel EditorComponentReelPrefab;
        public EditorComponent7Segment EditorComponentSevenSegmentPrefab;
        public EditorComponentAlpha EditorComponentAlphaPrefab;
        public EditorComponentAlpha14 EditorComponentAlpha14Prefab;
        public EditorComponentOverlay EditorComponentOverlayPrefab;

        public EditorPanel EditorPanelViewBase;
        public EditorPanel EditorPanelFull;

        public ProjectData Project
        {
            get;
            set;
        }

        public bool DisplayText
        {
            get
            {
                return _displayText;
            }
            set
            {
                _displayText = value;
                OnDisplayTextSet?.Invoke(_displayText);
            }
        }

        public UnityEvent<LayoutObject> OnLayoutSet = new UnityEvent<LayoutObject>();
        public UnityEvent<bool> OnDisplayTextSet = new UnityEvent<bool>();
        public UnityEvent<EditorView> OnEditorViewEnabled = new UnityEvent<EditorView>();
        public UnityEvent<EditorView> OnEditorViewDisabled = new UnityEvent<EditorView>();


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

        private bool _displayText = false;

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

            ColorPicker.gameObject.SetActive(false);
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

            // JP Placeholder until Components are not derived from Monobehaviour:
            OnLayoutSet.AddListener(OnLayoutSetCallback);
        }

        private void RemoveListeners()
        {
            ExtractImporter.OnImportComplete.RemoveListener(OnImportComplete);
        }

        // JP Placeholder until Component base no longer derived from Monobehaviour:
        private void OnLayoutAddComponent(Layout.Component component, View view)
        {
            // fake call constructor, until we have standard c# constructor/destructor
            component.ConstructorPlaceholder();
        }

        // JP Placeholder until Component base no longer derived from Monobehaviour:
        private void OnLayoutSetCallback(LayoutObject layout)
        {
            if (layout != null)
            {
                layout.OnAddComponent.AddListener(OnLayoutAddComponent);
            }
            // TODO else remove listener if set?  Also in OnDestroy if set?
        }

        private void OnImportComplete()
        {
        }
    }
}
