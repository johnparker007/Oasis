using Oasis.UI.Fields;
using RuntimeInspectorNamespace;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelMPU4LampRemapper : PanelBase
    {
        public FieldMPU4LampColumns SourceLampColumns;
        public FieldMPU4LampColumns TargetLampColumns;
        public Button PopulateSourceButton;
        public Button PopulateTargetButton;
        public Button RemapLampsButton;

        protected override void Awake()
        {
            base.Awake();

            AddListeners();
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            PopulateSourceButton.onClick.AddListener(OnPopulateSourceButtonClick);
            PopulateTargetButton.onClick.AddListener(OnPopulateTargetButtonClick);
            RemapLampsButton.onClick.AddListener(OnRemapLampsButtonClick);
        }

        private void RemoveListeners()
        {
            PopulateSourceButton.onClick.RemoveListener(OnPopulateSourceButtonClick);
            PopulateTargetButton.onClick.RemoveListener(OnPopulateTargetButtonClick);
            RemapLampsButton.onClick.RemoveListener(OnRemapLampsButtonClick);
        }

        private void OnPopulateSourceButtonClick()
        {
            Debug.LogError("PopulateSource - Not yet implemented");
        }

        private void OnPopulateTargetButtonClick()
        {
            Debug.LogError("PopulateTarget - Not yet implemented");
        }

        private void OnRemapLampsButtonClick()
        {
            Debug.LogError("OnRemapLampsButtonClick");
        }

    }

}
