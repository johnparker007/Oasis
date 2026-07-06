using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.Model
{
    public class ComponentTagMap : Dictionary<uint, TagInfo>
    {
        public ComponentTagMap() : base() { }

        public ComponentTagMap(IDictionary<uint, TagInfo> dictionary)
            : base()
        {
            if (dictionary is null) return;
            foreach (var kvp in dictionary)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        public ComponentTagMap NestedTagBlockMap { get; private set; }

        public ComponentTagMap WithNestedTagBlock(ComponentTagMap nestedMap)
        {
            NestedTagBlockMap = nestedMap;
            return this;
        }


        private readonly Dictionary<uint, int> keyLengthsByTag = new();
        private readonly HashSet<int> distinctKeyLengths = new();

        public new void Add(uint key, TagInfo value)
        {
            Add(key, value, InferKeyLengthBytes(key));
        }

        public void Add(uint key, TagInfo value, int keyLengthBytes)
        {
            ValidateKeyLengthBytes(keyLengthBytes);

            keyLengthsByTag[key] = keyLengthBytes;
            distinctKeyLengths.Add(keyLengthBytes);
            base.Add(key, value);
        }

        public bool TryGetValue(uint key, int keyLengthBytes, out TagInfo value)
        {
            value = null;
            if (!base.TryGetValue(key, out var tagInfo)) return false;
            if (!keyLengthsByTag.TryGetValue(key, out var storedLength)) return false;
            if (storedLength != keyLengthBytes) return false;

            value = tagInfo;
            return true;
        }

        public IReadOnlyList<int> GetDistinctKeyLengthsDescending()
        {
            if (distinctKeyLengths.Count == 0)
            {
                return new[] { 1 };
            }

            return distinctKeyLengths.OrderByDescending(x => x).ToArray();
        }

        private static int InferKeyLengthBytes(uint key)
        {
            if (key <= 0xFF) return 1;
            if (key <= 0xFFFF) return 2;
            if (key <= 0xFFFFFF) return 3;
            return 4;
        }

        private static void ValidateKeyLengthBytes(int keyLengthBytes)
        {
            if (keyLengthBytes is < 1 or > 4)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(keyLengthBytes),
                    "Component tag key length must be between 1 and 4 bytes.");
            }
        }
    }
}
