using System.Collections.Generic;
using System.Linq;

namespace MfmeFmlDecoder.src.Decoder.Component.Helper
{
    public record EffectOption(int Index, string Name);
    public record CoinNoteOption(int Index, string Name, IReadOnlyList<EffectOption> Effects);

    public static class CoinNoteHierarchy
    {
    // 75 coin note option(s)
    public static readonly IReadOnlyList<CoinNoteOption> Options = new[]
    {
        new CoinNoteOption(
            Index: 0x47,
            Name: "",
            Effects: new EffectOption[]
            {
                new(Index: 0x0, Name: ""),
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x8, Name: "S1 Token"),
                new(Index: 0x6, Name: "Comparitor"),
                new(Index: 0x9, Name: "Arm Pull Slow"),
                new(Index: 0xA, Name: "Arm Pull Normal"),
                new(Index: 0xB, Name: "Arm Pull Fast")
            }
        ),
        new CoinNoteOption(
            Index: 0x0,
            Name: "5p  Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x1,
            Name: "10p Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2,
            Name: "20p Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x3,
            Name: "Tok Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x4,
            Name: "50p Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x5,
            Name: "£1 Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x6,
            Name: "£2 Binary",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x7,
            Name: "5p  MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x8,
            Name: "10p MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x9,
            Name: "20p MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xA,
            Name: "50p MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xB,
            Name: "£1 MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xC,
            Name: "£2 MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xD,
            Name: "£1 MPU5",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xE,
            Name: "£2 MPU5",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0xF,
            Name: "bit0 1",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x10,
            Name: "bit1 2",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x11,
            Name: "bit2 4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x12,
            Name: "bit3 8",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x13,
            Name: "bit4 16",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x14,
            Name: "bit5 32",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x15,
            Name: "bit6 64",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x16,
            Name: "bit7 128",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x5, Name: "S1"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x17,
            Name: "5p EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x18,
            Name: "10p EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x19,
            Name: "20p EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x1A,
            Name: "Tok EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x1B,
            Name: "50p Old EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x44,
            Name: "50p New EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x1C,
            Name: "£1 EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x1D,
            Name: "£2 EPOCH",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x1E,
            Name: "ccTalk Note 1",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x1F,
            Name: "ccTalk Note 2",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x20,
            Name: "ccTalk Note 3",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x32,
            Name: "ccTalk Note 4",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x27,
            Name: "ccTalk Coin 1",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x28,
            Name: "ccTalk Coin 2",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x29,
            Name: "ccTalk Coin 3",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2A,
            Name: "ccTalk Coin 4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2B,
            Name: "ccTalk Coin 5",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2C,
            Name: "ccTalk Coin 6",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2D,
            Name: "ccTalk Coin 7",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2E,
            Name: "ccTalk Coin 8",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x21,
            Name: "5p Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x22,
            Name: "10p Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x23,
            Name: "20p Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x24,
            Name: "50p Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x25,
            Name: "£1 Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x26,
            Name: "£2 Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic")
            }
        ),
        new CoinNoteOption(
            Index: 0x2F,
            Name: "NV4 £5 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x30,
            Name: "NV4 £10 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x31,
            Name: "NV4 £20 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x33,
            Name: "Tok Proconn",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x36,
            Name: "Tok BB",
            Effects: new EffectOption[]
            {
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token"),
                new(Index: 0x8, Name: "S1 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x39,
            Name: "Tok MPU4",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x3E,
            Name: "Tok MPU4 2",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x3A,
            Name: "Tok BFM",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x45,
            Name: "Tok BFM 2",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x3F,
            Name: "Tok MPS2",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x40,
            Name: "Tok M1A/B",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x41,
            Name: "Tok/2p SYS85",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x42,
            Name: "Tok SYS1",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x43,
            Name: "Tok Phoenix",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x2, Name: "Electronic Token"),
                new(Index: 0x4, Name: "S10 Token")
            }
        ),
        new CoinNoteOption(
            Index: 0x34,
            Name: "£1 Pluto5",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x6, Name: "Comparitor")
            }
        ),
        new CoinNoteOption(
            Index: 0x35,
            Name: "10p BB",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x5, Name: "S1")
            }
        ),
        new CoinNoteOption(
            Index: 0x37,
            Name: "50p 1SW BB",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x5, Name: "S1")
            }
        ),
        new CoinNoteOption(
            Index: 0x38,
            Name: "50p 2SW BB",
            Effects: new EffectOption[]
            {
                new(Index: 0x3, Name: "S10"),
                new(Index: 0x5, Name: "S1")
            }
        ),
        new CoinNoteOption(
            Index: 0x3B,
            Name: "JPM £5 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x3C,
            Name: "JPM £10 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x3D,
            Name: "JPM £20 Note",
            Effects: new EffectOption[]
            {
                new(Index: 0x7, Name: "Note")
            }
        ),
        new CoinNoteOption(
            Index: 0x48,
            Name: "10p Coinmaster",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10")
            }
        ),
        new CoinNoteOption(
            Index: 0x49,
            Name: "20p Coinmaster",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10")
            }
        ),
        new CoinNoteOption(
            Index: 0x4A,
            Name: "50p Coinmaster",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10")
            }
        ),
        new CoinNoteOption(
            Index: 0x4B,
            Name: "£1 Coinmaster",
            Effects: new EffectOption[]
            {
                new(Index: 0x1, Name: "Electronic"),
                new(Index: 0x3, Name: "S10")
            }
        )
    };

        public static readonly CoinNoteOption DefaultOption = new CoinNoteOption(
            Index: -1,
            Name: "",
            Effects: new EffectOption[]
            {
                new(Index: 0x00, Name: ""),
                new(Index: 0x01, Name: "Electronic"),
                new(Index: 0x02, Name: "Electronic Token"),
                new(Index: 0x03, Name: "S10"),
                new(Index: 0x04, Name: "S10 Token"),
                new(Index: 0x05, Name: "S1"),
                new(Index: 0x08, Name: "S1 Token"),
                new(Index: 0x06, Name: "Comparitor"),
                new(Index: 0x09, Name: "Arm Pull Slow"),
                new(Index: 0x0A, Name: "Arm Pull Normal"),
                new(Index: 0x0B, Name: "Arm Pull Fast")
            }
        );

        /// <summary>
        /// Returns the Coin Note name for the given index and effect index,
        /// or empty string if no matching option or effect is found.
        /// </summary>
        public static string ResolveCoinNote(int index, uint effectIndex)
        {
            var option = Options.FirstOrDefault(o => o.Index == index);
            if (option == null) return string.Empty;
            var effect = option.Effects.FirstOrDefault(e => e.Index == effectIndex);
            return effect == null ? string.Empty : option.Name;
        }

        /// <summary>
        /// Returns the Effect name for the given coin note index and effect index.
        /// If the coin/note cannot be resolved, falls back to <see cref="DefaultOption"/>.
        /// Returns empty string if the effect index is also not found in the defaults.
        /// </summary>
        public static string ResolveEffect(int coinNoteIndex, uint effectIndex)
        {
            var option = Options.FirstOrDefault(o => o.Index == coinNoteIndex) ?? DefaultOption;
            var effect = option.Effects.FirstOrDefault(e => e.Index == effectIndex);
            return effect?.Name ?? string.Empty;
        }
    }
}
