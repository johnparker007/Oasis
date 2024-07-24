using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentAlpha : EditorComponent2D
    {
        public override string HierarchyPseudoSceneName => "Alphas";
        public override string HierarchyName => "Alpha";

        private List<EditorComponent16SemicolonSegment> _segments = null;

        protected override void Awake()
        {
            base.Awake();

            _segments = new List<EditorComponent16SemicolonSegment>();
            _segments.AddRange(GetComponentsInChildren<EditorComponent16SemicolonSegment>());
        }

        protected override void Refresh()
        {
            base.Refresh();

            for (int editorSegmentIndex = 0; editorSegmentIndex < _segments.Count; ++editorSegmentIndex)
            {
                EditorComponent16SemicolonSegment segment = _segments[editorSegmentIndex];

                segment.Initialise(null);

                int segmentIndex;
                if (((ComponentAlpha)Component).Reversed)
                {
                    segmentIndex = editorSegmentIndex;
                }
                else
                {
                    segmentIndex = _segments.Count - editorSegmentIndex - 1;
                }

                segment.Setup(segmentIndex);
            }
        }

        protected override void UpdateStateFromEmulation()
        {
            // nothing to do here, the EditorComponentSegments update themselves individually
        }
    }
}

