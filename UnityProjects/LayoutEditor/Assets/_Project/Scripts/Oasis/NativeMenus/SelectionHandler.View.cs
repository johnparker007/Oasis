using SFB;
using UnityEngine;
using Oasis;
using Oasis.Export;
using Oasis.MAME;
using Oasis.Layout;
using Oasis.LayoutEditor.Panels;

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

        public void OnViewAddViewQuad()
        {
            if (Editor.Instance == null)
            {
                Debug.LogWarning("The editor is not initialised; unable to add a ViewQuad.");
                return;
            }

            LayoutObject layout = Editor.Instance.Project?.Layout;
            if (layout == null)
            {
                Debug.LogWarning("No layout is loaded; unable to add a ViewQuad.");
                return;
            }

            PanelHierarchy hierarchy = Editor.Instance.HierarchyPanel;
            View targetView = hierarchy != null ? hierarchy.GetActiveView() : null;
            if (targetView == null)
            {
                targetView = layout.BaseView;
            }

            if (targetView == null)
            {
                Debug.LogWarning("Unable to determine the active view; unable to add a ViewQuad.");
                return;
            }

            string viewName = string.IsNullOrWhiteSpace(targetView.Name) ? "<unnamed view>" : targetView.Name;

            if (layout.TryAddViewQuad(targetView, out ViewQuad createdViewQuad))
            {
                hierarchy?.HighlightViewQuad(targetView, createdViewQuad);
                return;
            }

            string existingName = targetView.ActiveViewQuad?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(existingName))
            {
                Debug.LogWarning($"The view '{viewName}' already has a ViewQuad named '{existingName}'.");
            }
            else
            {
                Debug.LogWarning($"Unable to add a ViewQuad to the view '{viewName}'.");
            }
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
