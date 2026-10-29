using MfmeFmlDecoder.Decoder;
using System.Collections.Generic;
using System.Linq;
using MfmeFmlDecoder.src.Model.Component;

namespace MfmeFmlDecoder.src.Decoder.Component.Behaviours
{
    /// <summary>
    /// v19.8 writes container borders after their contents. 20.1 writes the background
    /// first, then borders in reverse file order, then the remaining elements.
    /// This is a structural ordering difference, not a value difference.
    /// </summary>
    internal static class LegacyBorderOrderBehaviour
    {
        public static void Apply(IList<BaseComponent> components, ComponentOrderingMode mode)
        {
            if (mode != ComponentOrderingMode.BordersLastReversed || components is null || components.Count == 0)
            {
                return;
            }

            List<BaseComponent> backgrounds = components.Where(c => c is Background).ToList();
            List<BaseComponent> borders = components.OfType<Border>().Reverse<BaseComponent>().ToList();
            List<BaseComponent> rest = components
                .Where(c => c is not Background && c is not Border)
                .ToList();

            components.Clear();
            foreach (BaseComponent component in backgrounds)
            {
                components.Add(component);
            }

            foreach (BaseComponent component in borders)
            {
                components.Add(component);
            }

            foreach (BaseComponent component in rest)
            {
                components.Add(component);
            }
        }
    }
}
