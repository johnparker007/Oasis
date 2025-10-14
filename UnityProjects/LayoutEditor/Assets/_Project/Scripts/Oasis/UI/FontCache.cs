using System.Collections.Generic;
using UnityEngine;

namespace Oasis.UI
{
    public class FontCache
    {
        private Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

        public void Clear()
        {
            _fontCache.Clear();
        }

        public void TryAddFont(Font font, string name, FontStyle style)
        {
            if(ContainsFont(name, style))
            {
                return;
            }

            _fontCache.Add(GetKey(name, style), font);
        }

        public bool ContainsFont(string name, FontStyle style)
        {
            return _fontCache.ContainsKey(GetKey(name, style));
        }

        public Font GetFont(string name, FontStyle style)
        {
            return _fontCache[GetKey(name, style)];
        }

        private string GetKey(string name, FontStyle style)
        {
            return name + style.ToString();
        }
    }
}