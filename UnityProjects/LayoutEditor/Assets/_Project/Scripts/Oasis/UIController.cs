using Oasis.UI;
using UnityEngine;

namespace Oasis
{
    public class UIController : MonoBehaviour
    {
        public Editor LayoutEditor;
        public Canvas RootCanvas;
        public GameObject EditorCanvasGameObject;

        public RootUI RootUI
        {
            get;
            private set;
        } = null;

        private void Start()
        {
            RebuildUI();
        }

        public void RebuildUI()
        {
            RootUI = new RootUI(this);
            RootUI.Show();
        }
    }
}

