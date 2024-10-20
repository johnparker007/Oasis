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
    public class PanelEditorPreferences : PanelBase
    {
        public FieldString ServerAddress;

        protected override void AddListeners()
        {
        }

        protected override void RemoveListeners()
        {
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
            // TODO - Editor preferences

            //MameRomName.Input.Text = Editor.Instance.Project.Settings.Mame.RomName;
        }

        // TODO temp - will be doing an equivalent of the 'BoundInputField' approach used
        // by the Inspector was using for UI prototyping


    }

}
