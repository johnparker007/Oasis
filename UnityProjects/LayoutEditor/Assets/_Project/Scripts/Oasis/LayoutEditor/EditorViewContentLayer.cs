using UnityEngine;

namespace Oasis.LayoutEditor
{
    public class EditorViewContentLayer : MonoBehaviour
    {
        public enum LayerTypes
        {
            Checkerboard,
            BelowBackground,
            Background,
            AboveBackground
        }

        public LayerTypes LayerType;
    }
}
