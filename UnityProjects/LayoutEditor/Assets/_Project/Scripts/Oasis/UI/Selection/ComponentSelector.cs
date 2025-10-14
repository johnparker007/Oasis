using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI.Selection
{
    public class ComponentSelector : MonoBehaviour
    {
        private SelectionManager _selectionManager = null;

        private void Awake()
        {
            _selectionManager = GetComponent<SelectionManager>();
        }

        private void Update()
        {
            if (!_selectionManager.IsSelecting)
            {
                return;
            }

            // TOIMPROVE this will change when we get rid of the old RuntimeHierarchy/pseudo-scene stuff and
            // roll our own.  Then we'll be able to get the list of all EditorComponents in the view.
            List<Layout.Component> components = Editor.Instance.Project.Layout.BaseView.Data.Components;
            foreach(Layout.Component component in components)
            {
                bool insideSelectionRectangle = IsEditorComponentInsideSelectionRectangle(component);
            }
        }

        private bool IsEditorComponentInsideSelectionRectangle(Layout.Component component)
        {
return false;
//            "HERE - need to move some of this stuff perhaps into SelectionManager for getting the general 'rect'
            //                Vector2 min = Vector2.Min(start, end);
            //Vector2 max = Vector2.Max(start, end);

            //RectTransform canvasRect = _selectionManager.Canvas.transform as RectTransform;
            //RectTransformUtility.ScreenPointToLocalPointInRectangle(
            //    canvasRect, min, _selectionManager.Canvas.worldCamera, out Vector2 localMin);
            //RectTransformUtility.ScreenPointToLocalPointInRectangle(
            //    canvasRect, max, _selectionManager.Canvas.worldCamera, out Vector2 localMax);

            //Vector2 size = localMax - localMin;
            //Vector2 topLeft = localMin;
        }
    }
}
