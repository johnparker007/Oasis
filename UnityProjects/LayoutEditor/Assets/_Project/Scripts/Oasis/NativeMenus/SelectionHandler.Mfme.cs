using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.LayoutEditor;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnMfmeExtract()
        {
        }

        public void OnMfmeRemapLamps()
        {
            Editor.Instance.TabController.ShowTab(TabController.TabTypes.LampRemapper);
        }
    }
}
