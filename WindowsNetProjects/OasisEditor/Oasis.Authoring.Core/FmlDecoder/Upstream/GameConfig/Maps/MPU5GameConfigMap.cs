// GameConfig map for MPU5.
// Editable JSON below. Optionally regenerate via tools/GenGameConfigMaps.

namespace MfmeFmlDecoder.GameConfig.Maps
{
    using MfmeFmlDecoder.GameConfig;

    internal static class MPU5GameConfigMap
    {
        public const string SystemName = "MPU5";

        private const string Json =
            @"{
              ""system"": ""MPU5"",
              ""controls"": {
                ""%"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""gamLines"": [
                    ""Percentage <index>"",
                    ""SetPercent <numeric-percent>""
                  ],
                  ""values"": {
                    ""70%"": 0,
                    ""72%"": 1,
                    ""74%"": 2,
                    ""76%"": 3,
                    ""78%"": 4,
                    ""80%"": 5,
                    ""82%"": 6,
                    ""84%"": 7,
                    ""86%"": 8,
                    ""88%"": 9,
                    ""90%"": 10,
                    ""92%"": 11,
                    ""94%"": 12,
                    ""96%"": 13,
                    ""98%"": 14
                  },
                  ""note"": ""Stored in GAM (Percentage index + SetPercent). FML 0x46 dirty flips seen during probes are not the % value.""
                },
                ""Cabinet Style"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x8F"",
                  ""values"": {
                    ""Default"": 0,
                    ""Rio"": 1,
                    ""Genesis"": 2
                  }
                },
                ""Caption"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0C"",
                  ""encoding"": ""ascii-null-padded"",
                  ""readOnly"": true,
                  ""note"": ""Game Details Caption is read-only; mirrors layout Description tag 0x0C.""
                },
                ""Cash"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x20"",
                  ""encoding"": ""u32-le"",
                  ""emptyUi"": 255
                },
                ""Coin Mech"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x19"",
                  ""values"": {
                    ""Parallel"": 0,
                    ""Binary"": 1
                  }
                },
                ""DataPak"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""gamLine"": ""Protocol <index>"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1,
                    ""LoopBack"": 2
                  },
                  ""note"": ""Stored in GAM as Protocol. Line omitted means No (index 0). FML 0x46 flips are not DataPak.""
                },
                ""DIP"": {
                  ""kind"": ""dip-banks"",
                  ""storage"": ""gam"",
                  ""probed"": true,
                  ""banks"": {
                    ""DIP1"": {
                      ""gamLine"": ""DIP 1"",
                      ""bits"": 8
                    },
                    ""DIP2"": {
                      ""gamLine"": ""DIP 2"",
                      ""bits"": 8
                    }
                  },
                  ""stringLayout"": {
                    ""order"": ""lsb-first"",
                    ""index0"": ""UI switch 1 (LSB)"",
                    ""index7"": ""UI switch 8 (MSB)"",
                    ""on"": ""1"",
                    ""off"": ""0""
                  },
                  ""probeResults"": [
                    {
                      ""bank"": 1,
                      ""switchNumber"": 1,
                      ""gamChanged"": true,
                      ""dip1Before"": ""00000000"",
                      ""dip1After"": ""10000000"",
                      ""dip2Before"": ""00000000"",
                      ""dip2After"": ""00000000""
                    },
                    {
                      ""bank"": 1,
                      ""switchNumber"": 8,
                      ""gamChanged"": true,
                      ""dip1Before"": ""00000000"",
                      ""dip1After"": ""00000001"",
                      ""dip2Before"": ""00000000"",
                      ""dip2After"": ""00000000""
                    },
                    {
                      ""bank"": 2,
                      ""switchNumber"": 1,
                      ""gamChanged"": true,
                      ""dip1Before"": ""00000000"",
                      ""dip1After"": ""00000000"",
                      ""dip2Before"": ""00000000"",
                      ""dip2After"": ""10000000""
                    }
                  ]
                },
                ""EffectsButtons"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": true,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""Effects <bitmask>"",
                  ""mask"": 8
                },
                ""EffectsCoins"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": true,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""Effects <bitmask>"",
                  ""mask"": 16
                },
                ""EffectsMeters"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": true,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""Effects <bitmask>"",
                  ""mask"": 2
                },
                ""EffectsReels"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": true,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""Effects <bitmask>"",
                  ""mask"": 1
                },
                ""EffectsTriacs"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": true,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""Effects <bitmask>"",
                  ""mask"": 4
                },
                ""Hopper Type"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1C"",
                  ""values"": {
                    ""Compact"": 0,
                    ""Universal"": 1,
                    ""Twin"": 2,
                    ""New"": 3,
                    ""Serial"": 4,
                    ""Compact 2"": 5,
                    ""Twin 2"": 6,
                    ""None"": 7
                  }
                },
                ""J2"": {
                  ""kind"": ""radio"",
                  ""storage"": ""volatile"",
                  ""fixedValue"": ""IRQ3"",
                  ""fixedRaw"": 0,
                  ""values"": {
                    ""IRQ3"": 0,
                    ""IRQ5"": 1
                  },
                  ""note"": ""MFME never persists J2; save/reload always leaves IRQ3. Emitted as fixed IRQ3.""
                },
                ""Lamp Test"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x48"",
                  ""values"": {
                    ""Pass"": 0,
                    ""Fail"": 1
                  }
                },
                ""Lo Tech"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": false,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""LoTech <0|1>""
                },
                ""Lock"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x56"",
                  ""encoding"": ""u32-le"",
                  ""emptyUi"": 255
                },
                ""Meter Effects"": {
                  ""kind"": ""effect-grid"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x6D"",
                  ""rows"": 16,
                  ""rowStride"": 8,
                  ""fields"": {
                    ""on"": {
                      ""byteOffset"": 0,
                      ""encoding"": ""u32-le""
                    },
                    ""off"": {
                      ""byteOffset"": 4,
                      ""encoding"": ""u32-le""
                    }
                  },
                  ""options"": [
                    """",
                    ""Meter""
                  ],
                  ""confirmedEffectCodes"": {
                    ""Meter"": 13
                  }
                },
                ""Meters"": {
                  ""kind"": ""meter-grid"",
                  ""storage"": ""fml"",
                  ""tag"": ""0xA0"",
                  ""sizeBytes"": 512,
                  ""meterTriacBlock"": {
                    ""byteOffset"": 0,
                    ""rowCount"": 16,
                    ""rowStride"": 16,
                    ""rowLayout"": {
                      ""meterIn"": 0,
                      ""meterOut"": 4,
                      ""triacIn"": 8,
                      ""triacOut"": 12
                    },
                    ""indexing"": ""row i (0-based) holds Meter (i+1) and Triac (i+1)""
                  },
                  ""secBlock"": {
                    ""byteOffset"": 256,
                    ""entryCount"": 32,
                    ""entryStride"": 8,
                    ""entryLayout"": {
                      ""in"": 0,
                      ""out"": 4
                    },
                    ""indexing"": ""entry i (0-based) holds Sec (i+1)""
                  },
                  ""absoluteOffsets"": {
                    ""Meter N In"": ""(N-1)*16 + 0"",
                    ""Meter N Out"": ""(N-1)*16 + 4"",
                    ""Triac N In"": ""(N-1)*16 + 8"",
                    ""Triac N Out"": ""(N-1)*16 + 12"",
                    ""Sec S In"": ""256 + (S-1)*8 + 0"",
                    ""Sec S Out"": ""256 + (S-1)*8 + 4""
                  },
                  ""inMultiplierValues"": {
                    """": 0,
                    ""1"": 1,
                    ""2"": 2,
                    ""4"": 3,
                    ""5"": 4,
                    ""10"": 5,
                    ""25"": 6,
                    ""50"": 7
                  },
                  ""outMultiplierValues"": {
                    """": 0,
                    ""1"": 1,
                    ""2"": 2,
                    ""4"": 3,
                    ""5"": 4,
                    ""10"": 5,
                    ""25"": 6,
                    ""50"": 7,
                    ""-1"": 8,
                    ""-2"": 9,
                    ""-4"": 10,
                    ""-5"": 11,
                    ""-10"": 12
                  },
                  ""meterNames"": {
                    """": 0,
                    ""Meter 1"": 1,
                    ""Meter 2"": 2,
                    ""Meter 3"": 3,
                    ""Meter 4"": 4,
                    ""Meter 5"": 5,
                    ""Meter 6"": 6,
                    ""Meter 7"": 7,
                    ""Meter 8"": 8,
                    ""Meter 9"": 9,
                    ""Meter 10"": 10,
                    ""Meter 11"": 11,
                    ""Meter 12"": 12,
                    ""Meter 13"": 13,
                    ""Meter 14"": 14,
                    ""Meter 15"": 15,
                    ""Meter 16"": 16,
                    ""Triac 1"": 17,
                    ""Triac 2"": 18,
                    ""Triac 3"": 19,
                    ""Triac 4"": 20,
                    ""Triac 5"": 21,
                    ""Triac 6"": 22,
                    ""Triac 7"": 23,
                    ""Triac 8"": 24,
                    ""Triac 9"": 25,
                    ""Triac 10"": 26,
                    ""Triac 11"": 27,
                    ""Triac 12"": 28,
                    ""Triac 13"": 29,
                    ""Triac 14"": 30,
                    ""Triac 15"": 31,
                    ""Triac 16"": 32,
                    ""Sec 1"": 33,
                    ""Sec 2"": 34,
                    ""Sec 3"": 35,
                    ""Sec 4"": 36,
                    ""Sec 5"": 37,
                    ""Sec 6"": 38,
                    ""Sec 7"": 39,
                    ""Sec 8"": 40,
                    ""Sec 9"": 41,
                    ""Sec 10"": 42,
                    ""Sec 11"": 43,
                    ""Sec 12"": 44,
                    ""Sec 13"": 45,
                    ""Sec 14"": 46,
                    ""Sec 15"": 47,
                    ""Sec 16"": 48,
                    ""Sec 17"": 49,
                    ""Sec 18"": 50,
                    ""Sec 19"": 51,
                    ""Sec 20"": 52,
                    ""Sec 21"": 53,
                    ""Sec 22"": 54,
                    ""Sec 23"": 55,
                    ""Sec 24"": 56,
                    ""Sec 25"": 57,
                    ""Sec 26"": 58,
                    ""Sec 27"": 59,
                    ""Sec 28"": 60,
                    ""Sec 29"": 61,
                    ""Sec 30"": 62,
                    ""Sec 31"": 63,
                    ""Sec 32"": 64
                  },
                  ""unsetValue"": 0,
                  ""unsetMeans"": ""blank multiplier / not configured"",
                  ""note"": ""Unset slot = u32 0 (blank multiplier). Channel is positional; UI editor rows are not stored. Tag 0xA0 always 512 bytes when present.""
                },
                ""MUX5E"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x33"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1
                  }
                },
                ""Name In Manager"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x2C"",
                  ""encoding"": ""ascii-null-padded"",
                  ""sizeBytes"": 101,
                  ""alsoWritesGam"": ""Game <text>""
                },
                ""Network Id"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x75"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""None"": 0,
                    ""Master"": 1,
                    ""Slave 2"": 2,
                    ""Slave 3"": 3,
                    ""Slave 4"": 4,
                    ""Slave 5"": 5,
                    ""Slave 6"": 6,
                    ""Slave 7"": 7,
                    ""Slave 8"": 8,
                    ""Slave 9"": 9,
                    ""Slave 10"": 10,
                    ""Slave 11"": 11,
                    ""Community1"": 12,
                    ""Community2"": 13,
                    ""Community3"": 14,
                    ""Community4"": 15,
                    ""Community5"": 16,
                    ""Community6"": 17
                  },
                  ""alsoMirrorsGamLine"": ""LinkType <index>"",
                  ""note"": ""FML tag 0x75 (not 0x46). GAM also mirrors as LinkType.""
                },
                ""NV4 Note"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x31"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1
                  }
                },
                ""PAC Drive Assignments"": {
                  ""kind"": ""record-list"",
                  ""storage"": ""fml"",
                  ""recordTags"": {
                    ""0x93"": {
                      ""sizeBytes"": 16,
                      ""encoding"": ""4xu32-le"",
                      ""fields"": {
                        ""output"": 0,
                        ""outputType"": 4,
                        ""value1"": 8,
                        ""value2"": 12
                      }
                    },
                    ""0x94"": {
                      ""sizeBytes"": 20,
                      ""encoding"": ""ascii-null-padded"",
                      ""field"": ""label""
                    },
                    ""0x95"": {
                      ""sizeBytes"": 4,
                      ""encoding"": ""u32-le"",
                      ""field"": ""shortcut""
                    }
                  },
                  ""outputTypeValues"": {
                    ""Not Assigned"": 0,
                    ""Lamp"": 1,
                    ""Triac"": 2,
                    ""Meter"": 3,
                    ""Hopper 1"": 4,
                    ""Hopper 2"": 5,
                    ""Inhibit"": 6,
                    ""Triac -> Hopper 1"": 7,
                    ""Triac -> Hopper 2"": 8,
                    ""Hopper Opto 1"": 9,
                    ""Hopper Opto 2"": 10,
                    ""Always On"": 11,
                    ""Always Off"": 12,
                    ""Lamp -> Hopper 1"": 13,
                    ""Lamp -> Hopper 2"": 14
                  },
                  ""confirmedProbe"": {
                    ""output"": 1,
                    ""outputType"": ""Lamp""
                  }
                },
                ""PIC"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x46"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""1"": 0,
                    ""2"": 1,
                    ""3"": 2
                  },
                  ""note"": ""Isolation probe confirmed: PIC 1/2/3 persist as 0x46 u32 0/1/2. Earlier 'dirty flag' reads of 0x46 were PIC 1↔2.""
                },
                ""PIC Code"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x47"",
                  ""encoding"": ""ascii-4""
                },
                ""Prize"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""gamLine"": ""Jackpot <index>"",
                  ""values"": {
                    ""£5"": 0,
                    ""£8T"": 1,
                    ""£8"": 2,
                    ""£10"": 3,
                    ""£15"": 4,
                    ""£25"": 5,
                    ""£35"": 6,
                    ""£70"": 7,
                    ""£3"": 8,
                    ""£4"": 9,
                    ""£6"": 10,
                    ""£6T"": 11,
                    ""£25LBO"": 12,
                    ""£100"": 13
                  },
                  ""note"": ""Stored in GAM as Jackpot. FML 0x46 dirty flips seen during probes are not Prize.""
                },
                ""RAM Size"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0xAA"",
                  ""values"": {
                    ""64K"": 0,
                    ""128K"": 1
                  }
                },
                ""Rating"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""encoding"": ""gam-line"",
                  ""gamLine"": ""Rating <text>""
                },
                ""Reel Jumpers 1"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4F"",
                  ""values"": {
                    ""Old1"": 0,
                    ""V1a"": 1,
                    ""V1b"": 5
                  },
                  ""structure"": ""reel-jumpers-v1b"",
                  ""note"": ""Old1/V1a → J1–J8 checkboxes. V1b → 5 dual-ternary dropdowns (replaces checkboxes).""
                },
                ""Reel Jumpers 1 J1"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 0,
                  ""bit"": 3,
                  ""mask"": 8,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J2"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 4,
                  ""bit"": 3,
                  ""mask"": 8,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J3"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 0,
                  ""bit"": 4,
                  ""mask"": 16,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J4"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 4,
                  ""bit"": 4,
                  ""mask"": 16,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J5"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 0,
                  ""bit"": 6,
                  ""mask"": 64,
                  ""note"": ""Visible in Old1/V1a only. (V1b slot bits are 3–7 contiguous; Old mode skips bit 5.)""
                },
                ""Reel Jumpers 1 J6"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 4,
                  ""bit"": 6,
                  ""mask"": 64,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J7"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 0,
                  ""bit"": 7,
                  ""mask"": 128,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 1 J8"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x27"",
                  ""byteIndex"": 4,
                  ""bit"": 7,
                  ""mask"": 128,
                  ""note"": ""Visible in Old1/V1a only.""
                },
                ""Reel Jumpers 2"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4F"",
                  ""values"": {
                    ""Old"": 0,
                    ""V1a"": 2,
                    ""V1b"": 10
                  },
                  ""structure"": ""reel-jumpers-v1b"",
                  ""note"": ""Old/V1a → J1–J8 on 0x4E. V1b → 5 combos (shares 0x50 with RJ1).""
                },
                ""Reel Jumpers 2 J1"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 0,
                  ""bit"": 3,
                  ""mask"": 8,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J2"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 4,
                  ""bit"": 3,
                  ""mask"": 8,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J3"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 0,
                  ""bit"": 4,
                  ""mask"": 16,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J4"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 4,
                  ""bit"": 4,
                  ""mask"": 16,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J5"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 0,
                  ""bit"": 6,
                  ""mask"": 64,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J6"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 4,
                  ""bit"": 6,
                  ""mask"": 64,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J7"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 0,
                  ""bit"": 7,
                  ""mask"": 128,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Reel Jumpers 2 J8"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x4E"",
                  ""byteIndex"": 4,
                  ""bit"": 7,
                  ""mask"": 128,
                  ""note"": ""Visible in Old/V1a only. Not used by V1b combos.""
                },
                ""Refill"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x2B"",
                  ""encoding"": ""u32-le"",
                  ""emptyUi"": 255
                },
                ""SEC"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1E"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1
                  }
                },
                ""Service"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1F"",
                  ""encoding"": ""u32-le"",
                  ""emptyUi"": 255
                },
                ""Showtime/Mux2"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x92"",
                  ""values"": {
                    ""Enabled"": 0,
                    ""Disabled"": 1
                  }
                },
                ""Stake"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""gamLine"": ""Stake <index>"",
                  ""values"": {
                    ""5p"": 0,
                    ""10p"": 1,
                    ""20p"": 2,
                    ""25p"": 3,
                    ""30p"": 4,
                    ""50p"": 5,
                    ""£1"": 6
                  },
                  ""note"": ""Stored in GAM as Stake. FML 0x46 dirty flips seen during probes are not Stake.""
                },
                ""Tags"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""encoding"": ""gam-line"",
                  ""gamLine"": ""Tags <text>""
                },
                ""Test 2"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x49"",
                  ""encoding"": ""u32-le"",
                  ""emptyUi"": 255
                },
                ""Top Up"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""alsoWrites"": ""0xA4"",
                  ""encoding"": ""mirrored-u32-le"",
                  ""emptyUi"": 255,
                  ""tags"": [
                    ""0x71"",
                    ""0xA4""
                  ]
                },
                ""Triac Effects"": {
                  ""kind"": ""effect-grid"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x6A"",
                  ""rows"": 16,
                  ""rowStride"": 8,
                  ""fields"": {
                    ""on"": {
                      ""byteOffset"": 0,
                      ""encoding"": ""u32-le""
                    },
                    ""off"": {
                      ""byteOffset"": 4,
                      ""encoding"": ""u32-le""
                    }
                  },
                  ""options"": [
                    """",
                    ""2p Slide"",
                    ""10p Slide"",
                    ""20p Slide"",
                    ""50p Slide"",
                    ""£1 Slide"",
                    ""Misc Slide"",
                    ""Token Slide"",
                    ""Meter"",
                    ""Lockout On"",
                    ""Lockout Off"",
                    ""Sol/Wiper On"",
                    ""Sol/Wiper Off"",
                    ""Motor On"",
                    ""Solenoid On"",
                    ""Solenoid Off""
                  ],
                  ""confirmedEffectCodes"": {
                    """": 0,
                    ""2p Slide"": 42,
                    ""10p Slide"": 5,
                    ""20p Slide"": 18,
                    ""50p Slide"": 19,
                    ""£1 Slide"": 34,
                    ""Misc Slide"": 35,
                    ""Token Slide"": 6,
                    ""Meter"": 13,
                    ""Lockout On"": 16,
                    ""Lockout Off"": 17,
                    ""Sol/Wiper On"": 20,
                    ""Sol/Wiper Off"": 21,
                    ""Motor On"": 1,
                    ""Solenoid On"": 2,
                    ""Solenoid Off"": 3
                  }
                },
                ""Version"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0D"",
                  ""encoding"": ""ascii-null-padded"",
                  ""alsoWritesGam"": ""Version <text>""
                },
                ""WIP"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": false,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""WIP <0|1>""
                }
              },
              ""imageAudited"": true,
              ""auditedTabs"": [
                ""Effects"",
                ""PAC Drive""
              ]
            }";

        public static GameConfigMap Create() =>
            GameConfigMap.FromJson(SystemName, "embedded:" + SystemName, Json);
    }
}
