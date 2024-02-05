using Oasis.UI;
using RuntimeInspectorNamespace;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis
{
    public class UIController : MonoBehaviour
    {
        public Editor LayoutEditor;
        public GameObject EditorCanvasGameObject;
        public RuntimeHierarchy RuntimeHierarchy;

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

        public MfmeExtractForm MfmeExtractForm
        {
            get;
            private set;
        } = null;


        private void Start()
        {
            RuntimeHierarchy.ConnectedInspector.ComponentFilter += InspectorComponentFilter;

            RebuildUI();
        }

        private void Update()
        {
            //"HERE - try having a new : Form object, that is DimensionlessAppContainerForm"

            //then that can create the RootUI and do show(this).  Then the RootUI form will
            // have a parent, and continually calling SendToBack should work
            //RootUI.SendToBack();
        }

        public void RebuildUI()
        {
            RootUIParentForm = new RootUIParentForm();

            RootUI = new RootUI(this);
            //RootUI.AssignParent(RootUIParentForm);
            //RootUI.Show();
        }

        public void ShowAboutForm()
        {
            AboutForm = new AboutForm(this);
            //AboutForm.AssignParent(RootUI);
            //AboutForm.ShowDialog();
        }

        public void ShowMfmeExtractForm()
        {
            MfmeExtractForm = new MfmeExtractForm(this);
            //MfmeExtractForm.AssignParent(RootUI);
            //MfmeExtractForm.ShowDialog();
        }

        private void InspectorComponentFilter(GameObject gameObject, List<Component> components)
        {
            // JP strip out Transform as we don't want that shown on any of our LayoutEditor objects
            components.RemoveAll(x => x.GetType() == typeof(Transform));
        }
    }
}

