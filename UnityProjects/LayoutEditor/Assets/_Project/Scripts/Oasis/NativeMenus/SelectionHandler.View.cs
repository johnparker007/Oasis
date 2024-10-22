using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;

namespace Oasis.NativeMenus
{
    public partial class SelectionHandler : MonoBehaviour
    {
        public void OnViewDisplayTextOn()
        {
            Editor.Instance.DisplayText = true;
        }

        public void OnViewDisplayTextOff()
        {
            Editor.Instance.DisplayText = false;
        }

        public void OnViewAddMameView()
        {
            Editor.Instance.ViewController.AddViewMame();
        }
    }
}
