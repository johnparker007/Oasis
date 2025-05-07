using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.FileOperations;
using System.IO;
using Oasis.LayoutEditor;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnBackgroundUpscale()
        {
            Editor.Instance.TabController.ShowTab(TabController.TabTypes.UpscaledBase);
        }

        public void OnBackgroundLocalNormalise()
        {
            Editor.Instance.TabController.ShowTab(TabController.TabTypes.LocalLightNormaliser);
        }

        public void OnBackgroundGlobalNormalise()
        {
            Debug.LogWarning("Not yet implemented!");
        }
    }
}
