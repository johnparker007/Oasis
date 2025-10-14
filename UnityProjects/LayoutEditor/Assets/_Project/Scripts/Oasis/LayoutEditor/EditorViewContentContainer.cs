using UnityEngine;

namespace Oasis.LayoutEditor
{
    [DisallowMultipleComponent]
    internal sealed class EditorViewContentContainer : MonoBehaviour
    {
        public RectTransform RectTransform => (RectTransform)transform;
    }
}
