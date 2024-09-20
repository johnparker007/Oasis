using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Oasis.UI
{
    public class TmpFontAssetCache
    {
        private Dictionary<Font, TMP_FontAsset> _fontAssetCache = new Dictionary<Font, TMP_FontAsset>();

        public void Clear()
        {
            _fontAssetCache.Clear();
        }

        public void TryAddFontAsset(Font font, TMP_FontAsset fontAsset)
        {
            if(ContainsFontAsset(font))
            {
                return;
            }

            _fontAssetCache.Add(font, fontAsset);
        }

        public bool ContainsFontAsset(Font font)
        {
            return _fontAssetCache.ContainsKey(font);
        }

        public TMP_FontAsset GetFontAsset(Font font)
        {
            return _fontAssetCache[font];
        }
    }
}