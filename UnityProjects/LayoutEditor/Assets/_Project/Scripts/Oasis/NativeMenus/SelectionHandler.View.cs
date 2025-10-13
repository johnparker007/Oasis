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

        public void OnViewRebuildMameView()
        {
            Editor.Instance.ViewController.RebuildViewMame();
        }

        public void OnViewShowBaseViewQuads()
        {
            Editor.Instance.ViewController.SetBaseViewQuadsActive(true);
        }

        public void OnViewHideBaseViewQuads()
        {
            Editor.Instance.ViewController.SetBaseViewQuadsActive(false);
        }

        public void OnViewOutputTransformedViewQuad()
        {
            if (Editor.Instance == null)
            {
                Debug.LogWarning("The editor is not initialised; unable to output the transformed ViewQuad.");
                return;
            }

            LayoutObject layout = Editor.Instance.Project?.Layout;
            if (layout == null)
            {
                Debug.LogWarning("No layout is loaded; unable to output a transformed ViewQuad image.");
                return;
            }

            layout.OutputTransformedViewQuad();
        }
    }
}
