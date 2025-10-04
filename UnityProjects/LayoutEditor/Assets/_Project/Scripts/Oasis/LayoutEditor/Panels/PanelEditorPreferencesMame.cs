using Oasis.Download;
using Oasis.LayoutEditor.Tools;
using Oasis.MAME;
using Oasis.UI;
using Oasis.UI.Fields;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelEditorPreferencesMame : PanelBase
    {
        public FieldInt MameVersion;
        public Button ButtonInstallSelectedVersion;

        protected override void AddListeners()
        {
            MameVersion.Input.OnValueChanged += OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted += OnMameVersionValueChanged;

            ButtonInstallSelectedVersion.onClick.AddListener(OnButtonInstallSelectedVersionClick);
        }

        protected override void RemoveListeners()
        {
            MameVersion.Input.OnValueChanged -= OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted -= OnMameVersionValueChanged;

            ButtonInstallSelectedVersion.onClick.RemoveListener(OnButtonInstallSelectedVersionClick);
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
        }

        protected override void Populate()
        {
            MameVersion.Input.Text = Editor.Instance.Preferences.MameVersion.ToString();
        }

        private bool OnMameVersionValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.MameVersion = int.Parse(value);

            return true;
        }

        private void OnButtonInstallSelectedVersionClick()
        {
            System.Threading.Tasks.Task<string> task = 
                MameDownloader.Instance.DownloadAndExtractAsync(Editor.Instance.Preferences.MameVersion);
        }


    }

}
