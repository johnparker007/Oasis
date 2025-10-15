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

        private BaseViewQuadOverlay _baseViewQuadOverlay = null;

        

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
            View viewMame = Editor.Instance.Project.Layout.AddView(kMameViewName);

            // for now initialise MAME view quad as the size of the Base view background
            // TOIMPROVE - potentially... can the Base view not have a background, or are we
            // always forcing that as a required component on a new Base view?
            View viewBase = Editor.Instance.Project.Layout.BaseView;

            ComponentBackground baseBackground =
                (ComponentBackground)viewBase.Data.Components.Find(x => x.GetType() == typeof(ComponentBackground));

            viewMame.SetViewQuadRectangle(
                baseBackground.Position.y,
                baseBackground.Position.x,
                baseBackground.Position.y + baseBackground.Size.y,
                baseBackground.Position.x + baseBackground.Size.x);
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
                // TOIMPROVE - this all wants refactoring into some kind of OasisRect system:
                if (!mameView.Data.ViewQuad.ContainsAnyPoint(
                    component.PointTopLeft, 
                    component.PointTopRight, 
                    component.PointBottomLeft, 
                    component.PointBottomRight))
                {
                    continue;
                }

                Layout.Component componentClone = component.Clone();

                // TOIMPROVE put this in here for now, as not even sure if this is the best way to go about this:
                if(componentClone.Position.x < 0)
                {
                    componentClone.Size = new Vector2Int(componentClone.Size.x + componentClone.Position.x, componentClone.Size.y);
                    componentClone.Position = new Vector2Int(0, componentClone.Position.y);
                }

                if (componentClone.Position.y < 0)
                {
                    componentClone.Size = new Vector2Int(componentClone.Size.x, componentClone.Size.y + componentClone.Position.y);
                    componentClone.Position = new Vector2Int(componentClone.Position.x, 0);
                }

                mameView.AddComponent(componentClone);
            }

            // TODO target has more components than source! (prob reel overlays which
            // will be removed when background alpha baking is done)

            Debug.LogError("After: baseView.Data.Components.Count == " + baseView.Data.Components.Count);
            Debug.LogError("After: mameView.Data.Components.Count == " + mameView.Data.Components.Count);
        }

        public void SetBaseViewQuadsActive(bool active)
        {
            if (active)
            {
                BaseViewQuadOverlay overlay = GetOrCreateBaseViewQuadOverlay();
                if (overlay != null)
                {
                    overlay.SetActive(true);
                }
            }
            else if (_baseViewQuadOverlay != null)
            {
                _baseViewQuadOverlay.SetActive(false);
            }
        }

        private BaseViewQuadOverlay GetOrCreateBaseViewQuadOverlay()
        {
            if (_baseViewQuadOverlay != null)
            {
                return _baseViewQuadOverlay;
            }

            View baseView = Editor.Instance.Project.Layout.BaseView;
            if (baseView == null)
            {
                return null;
            }

            EditorView editorView = baseView.EditorView;
            if (editorView == null || editorView.Content == null)
            {
                return null;
            }

            RectTransform contentRect = editorView.Content.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                return null;
            }

            GameObject overlayObject = new GameObject("BaseViewQuadOverlay", typeof(RectTransform), typeof(BaseViewQuadOverlay));
            overlayObject.transform.SetParent(contentRect, false);

            BaseViewQuadOverlay overlay = overlayObject.GetComponent<BaseViewQuadOverlay>();

            EditorPanel editorPanel = editorView.GetComponentInParent<EditorPanel>();
            Zoom zoom = editorPanel != null ? editorPanel.Zoom : null;

            overlay.Initialize(baseView, contentRect, zoom);

            _baseViewQuadOverlay = overlay;

            Editor.Instance.InspectorController.RegisterViewQuadOverlay(overlay);

            return _baseViewQuadOverlay;
        }


    }

}
