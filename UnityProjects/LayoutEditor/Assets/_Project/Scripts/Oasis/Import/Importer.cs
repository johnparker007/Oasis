using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oasis.FileOperations;
using System.IO;
using Oasis.Layout;
using UnityEngine;

namespace Oasis.Import 
{
    public class Importer
    {
        public LayoutObject ParseLayout(string json)
        {
            GameObject layoutGameObject = new GameObject("Layout");
            LayoutObject layout =  layoutGameObject.AddComponent<LayoutObject>();
            layout.transform.parent = Editor.Instance.transform;
            JToken token = JToken.Parse(json);
            if ((string) token["type"] != "LayoutObject") {
                throw new ImportParseException("JSON does not represent a LayoutObject");
            }
            // Iterate over the views and import them
            //result.Data.Views.Clear();
            foreach(JProperty viewKey in token["views"]) {
                JToken currentView = token["views"][viewKey.Name];
                ParseView(viewKey.Name, currentView, layout);
            }
            return layout;
        }

        public View ParseView(string key, JToken token, LayoutObject layout)
        {
            if ((string) token["type"] != "View") {
                throw new ImportParseException("JSON does not represent a View");
            }
            View view = layout.AddView(key);
            foreach(JProperty componentKey in token["items"]) {
                JToken currentComponent = token["items"][componentKey.Name];
                view.AddComponent(ParseComponent(componentKey.Name, currentComponent));
            }
            return view;
        }

        public Oasis.Layout.Component ParseComponent(string key, JToken token)
        {
            Oasis.Layout.Component result;
            string componentType = (string) token["type"];
            Dictionary<string, object> componentData = new Dictionary<string, object>(){};
            foreach(JProperty entry in token) {
                switch(entry.Value.Type) {
                    case JTokenType.Null: componentData.Add((string) entry.Name, null); break;
                    case JTokenType.String: componentData.Add((string) entry.Name, ((string)entry.Value)); break;
                    case JTokenType.Integer: componentData.Add((string) entry.Name, ((int)entry.Value)); break;
                    case JTokenType.Float: componentData.Add((string) entry.Name, ((float)entry.Value)); break;
                    case JTokenType.Boolean: componentData.Add((string) entry.Name, ((bool)entry.Value)); break;
                    default: componentData.Add((string) entry.Name, ((JToken)entry.Value)); break;
                }
            }
            return ComponentFactory.Create(
                componentData
            );
        }

        public LayoutObject Import(string importPath) 
        {
            /**
            *
                "api_version": "v1.0",
                "project_settings": {
                    "ROM_Name": "sc4clash",
                    "FruitMachine_Platform": "Scorpion4"
                }
            */
            using (StreamReader r = new StreamReader(importPath))
            {
                string json = r.ReadToEnd();
                LayoutObject layout = ParseLayout(json);
                return layout;
            }
            return null;
        }
    }
}