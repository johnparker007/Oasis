using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oasis.FileOperations;
using System.IO;
using Oasis.Layout;
using UnityEngine;
using Oasis.MAME;

namespace Oasis.Import 
{
    public class Importer
    {
        public ProjectData ParseProject(string json)
        {
            GameObject layoutGameObject = new GameObject("Layout");
            LayoutObject layout =  layoutGameObject.AddComponent<LayoutObject>();
            layout.transform.parent = Editor.Instance.transform;
            JToken token = JToken.Parse(json);
            if ((string) token["type"] != "LayoutObject") {
                throw new ImportParseException("JSON does not represent a LayoutObject");
            }
            // Iterate over the views and import them
            foreach(JProperty viewKey in token["views"]) {
                JToken currentView = token["views"][viewKey.Name];
                ParseView(viewKey.Name, currentView, layout);
            }
            // Parse project specific fields and pass these back also.
            ProjectData project = new ProjectData();
            project.Layout = layout;
            project.Settings.Mame.RomName = (string) token["project_settings"]["ROM_Name"];
            project.Settings.FruitMachine.Platform = (MameController.PlatformType) Enum.Parse(typeof(MameController.PlatformType), (string) token["project_settings"]["FruitMachine_Platform"], true);
            return project;
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
                    case JTokenType.Array: // Array will be mapped to List<string>
                    JArray items = (JArray) entry.Value;
                    List<string> itemList = new List<string>{};
                    for (int i = 0; i < items.Count; i++)
                    {
                        itemList.Add((string)items[i]);
                    }
                    componentData.Add((string) entry.Name, itemList);
                    break;
                    default: componentData.Add((string) entry.Name, ((JToken)entry.Value)); break;
                }
            }
            return ComponentFactory.Create(
                componentData
            );
        }

        public ProjectData Import(string importPath) 
        {
            /**
            *
                "api_version": "v1.0",
            */
            using (StreamReader r = new StreamReader(importPath))
            {
                string json = r.ReadToEnd();
                ProjectData project = ParseProject(json);
                return project;
            }
            return null;
        }
    }
}