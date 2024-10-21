using Oasis.Layout;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Import 
{
    public class ComponentFactory
    {
        /// <summary>
        /// Creates a Component
        /// </summary>
        /// <param name="componentType">A string representing the type of component to be created</param>
        /// <returns>A Component of the desired type or an ImportParseException if not found.</returns>
        public static Oasis.Layout.Component Create(string componentType)
        {
            GameObject gameObject = new GameObject();
            Oasis.Layout.Component component;
            switch(componentType) {
                case "ComponentBackground": return (ComponentBackground)gameObject.AddComponent(typeof(ComponentBackground));
                case "ComponentLamp": return (ComponentLamp)gameObject.AddComponent(typeof(ComponentLamp));
                case "Component7Segment": return (Component7Segment)gameObject.AddComponent(typeof(Component7Segment));
                case "Component14Segment": return (Component14Segment)gameObject.AddComponent(typeof(Component14Segment));
                case "Component16Segment": return (Component16Segment)gameObject.AddComponent(typeof(Component16Segment));
                case "ComponentAlpha": return (ComponentAlpha)gameObject.AddComponent(typeof(ComponentAlpha));
                case "ComponentReel": return (ComponentReel)gameObject.AddComponent(typeof(ComponentReel));
                case "ComponentSwitch": return (ComponentSwitch)gameObject.AddComponent(typeof(ComponentSwitch));  
            }
            throw new ImportParseException("Unknown component type " + componentType);
        }

        /// <summary>
        /// Creates a Component using a Dictionary<string, object> to represent type and state.
        /// </summary>
        /// <param name="serializedState">A Dictionary<string, object> representing the type and state of the component</param>
        /// <returns>A Component of the desired type or an ImportParseException if not found.</returns>
        public static Oasis.Layout.Component Create(Dictionary<string, object> serializedState)
        {
            Oasis.Layout.Component component = ComponentFactory.Create((string) serializedState["type"]);
            component.SetRepresentation(serializedState); //TODO: Let's employ the builder pattern here...
            return component;
        }
    }
}
