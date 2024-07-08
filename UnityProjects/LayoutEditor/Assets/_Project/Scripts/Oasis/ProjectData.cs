using Oasis.Project;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis
{
    // the data to be loaded/saved goes in this data class:
    [Serializable]
    public class ProjectData
    {
        public SettingsData Settings;

        private LayoutObject _layout = null;

        public LayoutObject Layout
        {
            get
            {
                return _layout;
            }
            set
            {
                _layout = value;
                Editor.Instance.OnLayoutSet?.Invoke(_layout);
            }
        }
    }
}
