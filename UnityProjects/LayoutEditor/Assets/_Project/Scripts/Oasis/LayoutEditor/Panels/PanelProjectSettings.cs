using Oasis.LayoutEditor.Tools;
using Oasis.UI.Fields;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelProjectSettings : PanelBase
    {
        public FieldString MameRomName;

        protected override void Awake()
        {
            base.Awake();

            AddListeners();
        }

        protected void Start()
        {
            Populate();
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        // JP TODO note this is all just initial testing code of how we will make our own 'BoundInputField'
        // generic processing behaviour:
        private void AddListeners()
        {
            MameRomName.InputField.onValueChanged.AddListener(OnMameRomNameValueChanged);
            MameRomName.InputField.onEndEdit.AddListener(OnMameRomNameEndEdit);
        }

        private void RemoveListeners()
        {
            MameRomName.InputField.onValueChanged.RemoveListener(OnMameRomNameValueChanged);
            MameRomName.InputField.onEndEdit.RemoveListener(OnMameRomNameEndEdit);
        }

        private void Populate()
        {
            MameRomName.InputField.text = Editor.Instance.Project.Settings.Mame.RomName;
        }

        // TODO temp - will be doing an equivalent of the 'BoundInputField' approach used
        // by the Inspector was using for UI prototyping
        private void OnMameRomNameValueChanged(string value)
        {
            Debug.Log("OnMameRomNameValueChanged: " + value);
            Editor.Instance.Project.Settings.Mame.RomName = value;
        }

        private void OnMameRomNameEndEdit(string value)
        {
            Debug.Log("OnMameRomNameEndEdit: " + value);
            Editor.Instance.Project.Settings.Mame.RomName = value;
        }

    }

}
