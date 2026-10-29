using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component.Helper
{
    /// <summary>
    /// Resolves the pre-20.x coin-note id/effect pair into the canonical 20.1 names.
    /// Legacy layouts store one note id (base tag 0x1A) and imply the effect.
    /// </summary>
    internal static class LegacyCoinNoteResolver
    {
        public static bool IsCanonical(int coinNoteId)
        {
            return coinNoteId == 0x43
                || coinNoteId == 0x44
                || coinNoteId == 0x45
                || coinNoteId == 0x49
                || coinNoteId == 0x4A;
        }

        public static uint ResolveEffectId(ref int coinNoteId)
        {
            switch (coinNoteId)
            {
                case 0x43: coinNoteId = 0x47; return 1;
                case 0x44: coinNoteId = 0x47; return 5;
                case 0x45: coinNoteId = 0x47; return 6;
                case 0x46:
                case 0x47:
                case 0x48:
                    coinNoteId -= 3;
                    return 0;
                case 0x49: coinNoteId = 0x47; return 3;
                case 0x4A: coinNoteId = 0x47; return 4;
                default: return 0;
            }
        }

        /// <summary>
        /// v6 token coin notes default to the token effect. Proconn token layouts
        /// default to S10 Token; every other token family defaults to Electronic Token.
        /// </summary>
        public static uint ResolveTokenDefaultEffect(int coinNoteId, bool bbRule = false)
        {
            CoinNoteOption option = CoinNoteHierarchy.Options
                .FirstOrDefault(o => o.Index == coinNoteId);
            if (option is null)
            {
                return 0;
            }

            if (option.Name is null)
            {
                return ResolveDefaultEffect(coinNoteId);
            }

            if (bbRule
                && option.Name.StartsWith("Tok", System.StringComparison.Ordinal)
                && option.Name.EndsWith(" BB", System.StringComparison.Ordinal))
            {
                string bbName = "S10 Token";
                EffectOption bbEffect = option.Effects.FirstOrDefault(e => string.Equals(e.Name, bbName, System.StringComparison.Ordinal));
                if (bbEffect is not null)
                {
                    return (uint)bbEffect.Index;
                }
            }

            if (!option.Name.StartsWith("Tok", System.StringComparison.Ordinal))
            {
                return ResolveDefaultEffect(coinNoteId);
            }

            string preferredName = string.Equals(option.Name, "Tok Proconn", System.StringComparison.Ordinal)
                ? "S10 Token"
                : "Electronic Token";
            EffectOption preferred = option.Effects.FirstOrDefault(e => string.Equals(e.Name, preferredName, System.StringComparison.Ordinal));
            return preferred is null ? ResolveDefaultEffect(coinNoteId) : (uint)preferred.Index;
        }

        public static uint ResolveDefaultEffect(int coinNoteId)
        {
            CoinNoteOption option = CoinNoteHierarchy.Options
                .FirstOrDefault(o => o.Index == coinNoteId);
            if (option is null)
            {
                return 0;
            }

            EffectOption effect = option.Effects.FirstOrDefault(e => e.Index != 0)
                ?? option.Effects.FirstOrDefault();
            return effect is null ? 0 : (uint)effect.Index;
        }
    }
}
