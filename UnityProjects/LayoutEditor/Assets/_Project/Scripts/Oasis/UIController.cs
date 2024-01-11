using Oasis.UI;
using UnityEngine;

namespace Oasis
{
    public class UIController : MonoBehaviour
    {
        public Editor LayoutEditor;
        public Canvas RootCanvas;
        public GameObject EditorCanvasGameObject;

        public RootUIParentForm RootUIParentForm
        {
            get;
            private set;
        } = null;

        public RootUI RootUI
        {
            get;
            private set;
        } = null;

        public AboutForm AboutForm
        {
            get;
            private set;
        } = null;

        private void Start()
        {
            RebuildUI();
        }

        public void Update()
        {
            //"HERE - try having a new : Form object, that is DimensionlessAppContainerForm"

            //then that can create the RootUI and do show(this).  Then the RootUI form will
            // have a parent, and continually calling SendToBack should work
            RootUI.SendToBack();
        }

        public void RebuildUI()
        {
            RootUIParentForm = new RootUIParentForm();

            RootUI = new RootUI(this);
            RootUI.AssignParent(RootUIParentForm);
            RootUI.Show();
        }

        public void ShowAboutForm()
        {
            // for now, seem to need to create a new form each time we want to 
            // show it, to be checked in case this is wrong...
            AboutForm = new AboutForm(this);
            AboutForm.AssignParent(RootUI);
            AboutForm.ShowDialog();
        }
    }
}

