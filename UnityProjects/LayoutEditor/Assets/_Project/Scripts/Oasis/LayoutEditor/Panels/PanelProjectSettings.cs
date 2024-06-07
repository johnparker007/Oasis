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

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
        }

        private void RemoveListeners()
        {
        }


    }

}
