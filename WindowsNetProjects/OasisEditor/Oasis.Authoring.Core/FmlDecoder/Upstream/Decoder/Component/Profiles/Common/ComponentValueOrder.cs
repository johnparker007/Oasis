using System;
using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// Canonical insertion order for model values and images. The model order is the
    /// baseline; a version profile may extend it, but it may not reorder the model.
    /// </summary>
    internal static class ComponentValueOrder
    {
        public static readonly IReadOnlyList<string> Empty = Array.Empty<string>();

        public static IReadOnlyList<string> Extend(
            IReadOnlyList<string> modelOrder,
            params string[] versionSpecific)
        {
            if (modelOrder is null || modelOrder.Count == 0)
            {
                return versionSpecific ?? Empty;
            }

            if (versionSpecific is null || versionSpecific.Length == 0)
            {
                return modelOrder;
            }

            List<string> combined = new(modelOrder.Count + versionSpecific.Length);
            combined.AddRange(modelOrder);
            foreach (string value in versionSpecific)
            {
                if (!combined.Contains(value, StringComparer.Ordinal))
                {
                    combined.Add(value);
                }
            }

            return combined;
        }
    }
}
