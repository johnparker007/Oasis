using UnityEngine;
using DynamicPanels;
using UnityEngine.UI;
using System;
using Oasis.Layout;
using System.Linq;
using System.Collections.Generic;

namespace Oasis.LayoutEditor
{
    public class ViewController : MonoBehaviour
    {
        public const string kBaseViewName = "Base";
        public const string kMameViewName = "Mame";

        

        public PanelTab ViewMamePanelTab
        {
            get;
            private set;
        } = null;

        public static EditorView GetEditorView(string viewName)
        {
            DynamicPanelsCanvas dynamicPanelsCanvas = Editor.Instance.UIController.DynamicPanelsCanvas;

            List<EditorView> editorViews = dynamicPanelsCanvas.GetComponentsInChildren<EditorView>(true).ToList();

            EditorView editorView = editorViews.Find(x => x.ViewName == viewName);

            return editorView;
        }

        public void AddViewMame()
        {
            ViewMamePanelTab = Editor.Instance.TabController.ShowTab(TabController.TabTypes.MameView);
            Editor.Instance.Project.Layout.AddView(kMameViewName);
        }

        public void RebuildViewMame()
        {
            // TODO this is initial test code for now, just to attempt rebuild:

            View baseView = Editor.Instance.Project.Layout.GetView(kBaseViewName);
            View mameView = Editor.Instance.Project.Layout.GetView(kMameViewName);

            Debug.LogError("Before: baseView.Data.Components.Count == " + baseView.Data.Components.Count);
            Debug.LogError("Before: mameView.Data.Components.Count == " + mameView.Data.Components.Count);
            
            // TODO clear all mameView components and associated EditorComponents before rebuild
            foreach(Layout.Component component in baseView.Data.Components)
            {
                // TODO deep clone all components, maybe after remove Monobehaviour stuff

                // TOIMPROVE - this all wants refactoring into some kind of OasisRect system:
                if (!mameView.Data.ViewQuad.ContainsAnyPoint(
                    component.PointTopLeft, 
                    component.PointTopRight, 
                    component.PointBottomLeft, 
                    component.PointBottomRight))
                {
                    continue;
                }

                mameView.AddComponent(component);
            }

            // TODO target has more components than source! (prob reel overlays which
            // will be removed when background alpha baking is done)

            Debug.LogError("After: baseView.Data.Components.Count == " + baseView.Data.Components.Count);
            Debug.LogError("After: mameView.Data.Components.Count == " + mameView.Data.Components.Count);
        }

        public void SetBaseViewQuadsActive(bool active)
        {
            // TODO
        }
            

    }

}
