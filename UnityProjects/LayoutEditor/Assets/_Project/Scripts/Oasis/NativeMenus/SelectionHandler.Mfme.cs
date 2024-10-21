using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnMfmeExtract()
        {
        }

        public void OnMfmeRemapLamps()
        {
            Editor.Instance.UIController.ShowMfmeRemapLampsForm();
        }
    }
}
