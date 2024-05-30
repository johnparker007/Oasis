using Oasis.LayoutEditor.Tools;
using Oasis.UI.Fields;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelMPU4LampRemapper : PanelBase
    {
        public UIController UIController;

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
            string[] sourceLampColumnsText = new string[Mpu4LampRemapper.kLampTableSize];
            string[] targetLampColumnsText = new string[Mpu4LampRemapper.kLampTableSize];
            for (int lampColumnIndex = 0; lampColumnIndex < Mpu4LampRemapper.kLampTableSize; ++lampColumnIndex)
            {
                sourceLampColumnsText[lampColumnIndex] = SourceLampColumns.InputFields[lampColumnIndex].text;
                targetLampColumnsText[lampColumnIndex] = TargetLampColumns.InputFields[lampColumnIndex].text;
            }

            UIController.LayoutEditor.Layout.RemapLamps(sourceLampColumnsText, targetLampColumnsText);
        }

    }

}
