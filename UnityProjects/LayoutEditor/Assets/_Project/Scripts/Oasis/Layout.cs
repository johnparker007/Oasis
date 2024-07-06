using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Oasis.Layout;
using Oasis.LayoutEditor;

using Component = Oasis.Layout.Component;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;
using Oasis.LayoutEditor.Tools;

namespace Oasis
{
    public class LayoutObject : MonoBehaviour
    {
        public static readonly string kMfmeViewName = "MFME Import";

        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class LayoutData
        {
            public List<View> Views = new();
        }

        public LayoutData Data = new LayoutData();

        public UnityEvent<Component, View> OnAddComponent = new();

        public Editor LayoutEditor
        {
            get;
            set;
        }

        public View MfmeImportView
        {
            get
            {
                return GetView(kMfmeViewName);
            }
        }

        public View AddView(string name)
        {
            GameObject viewGameObject = new GameObject();
            View view = (View)viewGameObject.AddComponent(typeof(View));
            viewGameObject.transform.SetParent(transform);

            Data.Views.Add(view);

            view.Initialise(name, LayoutEditor);

            return view;
        }

        public void DeleteView(string name)
        {
            View view = GetView(name);
            DeleteView(view);
        }

        public void DeleteView(View view)
        {
            Data.Views.Remove(view);
            Destroy(view.gameObject);
        }

        public View GetView(string name)
        {
            return Data.Views.Find(x => x.Name == name);
        }

        public void RemapLamps(string[] mfmeLampTable, string[] mameLampTable)
        {
            MfmeImportView.RemapLamps(mfmeLampTable, mameLampTable);
        }
    }
}
