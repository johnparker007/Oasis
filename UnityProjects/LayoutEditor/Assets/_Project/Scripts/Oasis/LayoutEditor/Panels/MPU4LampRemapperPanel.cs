using RuntimeInspectorNamespace;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class MPU4LampRemapperPanel : SkinnedWindow
    {
        public Image BackgroundImage;

        protected override void RefreshSkin()
        {
            BackgroundImage.color = Skin.BackgroundColor;
        }
    }

}
