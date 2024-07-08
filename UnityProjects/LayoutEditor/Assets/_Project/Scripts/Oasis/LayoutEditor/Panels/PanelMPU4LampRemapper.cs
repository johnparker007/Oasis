using Oasis.LayoutEditor.Tools;
using Oasis.UI.Fields;
using UnityEngine;
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
            string[] lampColumnData = Editor.Instance.MameMpu4ChrSourceCodeLookup.GetLampColumnData(
                Editor.Instance.MameController.DebugMameRomName);

            if (lampColumnData != null)
            {
                for (int lampColumnIndex = 0; lampColumnIndex < Mpu4LampRemapper.kLampTableSize; ++lampColumnIndex)
                {
                    TargetLampColumns.InputFields[lampColumnIndex].text = lampColumnData[lampColumnIndex];
                }
            }
            else
            {
                // TODO popup / message: 'chr lamp data not found for romname'
            }
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

            Editor.Instance.Layout.RemapLamps(sourceLampColumnsText, targetLampColumnsText);
        }

    }

}
