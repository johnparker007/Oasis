using System.Collections.Generic;
using MfmeFmlDecoder.Model;

namespace MfmeFmlDecoder.src.Decoder.Component.Profiles
{
    /// <summary>
    /// A version-specific delta over a component tag map. Overrides can replace a tag
    /// definition, add a tag, or remove a tag without copying the whole map.
    /// </summary>
    internal sealed record TagMapOverrides(
        IReadOnlyDictionary<uint, TagInfo> Replace = null,
        IReadOnlySet<uint> Remove = null)
    {
        public static readonly TagMapOverrides Empty =
            new(new Dictionary<uint, TagInfo>(), new HashSet<uint>());

        public ComponentTagMap Apply(ComponentTagMap source)
        {
            ComponentTagMap result = new(source ?? new ComponentTagMap());

            if (Remove is not null)
            {
                foreach (uint tag in Remove)
                {
                    result.Remove(tag);
                }
            }

            if (Replace is not null)
            {
                foreach (var kvp in Replace)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
    }
}
