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
        public static Layout.Component Create(string componentType)
        {
            switch (componentType)
            {
                case "ComponentBackground": return new ComponentBackground();
                case "ComponentLamp": return new ComponentLamp();
                case "Component7Segment": return new Component7Segment();
                case "Component14Segment": return new Component14Segment();
                case "Component16Segment": return new Component16Segment();
                case "ComponentAlpha": return new ComponentAlpha();
                case "ComponentReel": return new ComponentReel();
                case "ComponentSwitch": return new ComponentSwitch();

                default:
                    throw new ImportParseException("Unknown component type " + componentType);
            }
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
