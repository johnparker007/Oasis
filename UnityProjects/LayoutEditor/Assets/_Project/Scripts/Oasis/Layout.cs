using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Oasis.Layout;
using Oasis.LayoutEditor;

using Component = Oasis.Layout.Component;
using EditorComponent = Oasis.LayoutEditor.EditorComponent;
using Oasis.LayoutEditor.Tools;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Oasis
{

    // TODO: Implement ISerializable for LayoutObject itself
    // Figure out how to trigger the nested serialization of the components
    
    public class LayoutObject : MonoBehaviour, SerializableDictionary
    {
        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class LayoutData
        {
            public List<View> Views = new();
        }

        public void SetRepresentation(Dictionary<string, object> representation) {
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
        }

        public Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> typeWrapper = new Dictionary<string, object>();
            typeWrapper["type"] = GetType().Name;
            typeWrapper["views"] = new Dictionary<string, object>();
            foreach (View view in Data.Views) {
                ((Dictionary<string, object>)typeWrapper["views"])[view.Name] = ((SerializableDictionary)view).GetRepresentation();
            }
            return typeWrapper;
        }


        public LayoutData Data = new LayoutData();

        public UnityEvent<Component, View, bool> OnAddComponent = new();

        public View BaseView
        {
            get
            {
                return GetView(ViewController.kBaseViewName);
            }
        }

        //private bool _changed = false;
        //private bool _dirty = false;

        public bool Dirty
        {
            get;
            set;
        }

        public View AddView(string name)
        {
            GameObject viewGameObject = new GameObject();
            View view = (View)viewGameObject.AddComponent(typeof(View));
            viewGameObject.transform.SetParent(transform);

            Data.Views.Add(view);

            view.Initialise(name);

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

        public List<View> GetViews()
        {
            return Data.Views;
        }

        public Component GetComponentByGuid(string guid)
        {
            foreach(View view in Data.Views)
            {
                Component component = view.GetComponentByGuid(guid);
                if(component != null)
                {
                    return component;
                }
            }

            return null;
        }

        public void RemapLamps(string[] mfmeLampTable, string[] mameLampTable)
        {
            BaseView.RemapLamps(mfmeLampTable, mameLampTable);
        }
    }
}
