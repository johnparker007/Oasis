// GameConfig map for BLACKBOX.
// Editable JSON below. Optionally regenerate via tools/GenGameConfigMaps.

namespace MfmeFmlDecoder.GameConfig.Maps
{
    using MfmeFmlDecoder.GameConfig;

    internal static class BLACKBOXGameConfigMap
    {
        public const string SystemName = "BLACKBOX";

        private const string Json =
            @"{
              ""system"": ""BLACKBOX"",
              ""controls"": {
                ""Caption"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0C"",
                  ""encoding"": ""ascii-null-padded"",
                  ""readOnly"": true,
                  ""note"": ""Game Details Caption is read-only; mirrors layout Description tag 0x0C.""
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
                ""Lo Tech"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""defaultChecked"": false,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""LoTech 1 when checked""
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
                ""Motor Drive"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x79"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""B10"": 0,
                    ""B13"": 1
                  }
                },
                ""Motor Relay"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x7A"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""None"": 0,
                    ""M8"": 1
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
                ""NVRAM"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x64"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1
                  }
                },
                ""Rating"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""encoding"": ""gam-line"",
                  ""gamLine"": ""Rating <0-5>""
                },
                ""Reel Type"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x65"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""Solenoid"": 0,
                    ""Stepper"": 1
                  }
                },
                ""Refill"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x2B"",
                  ""encoding"": ""u32-le""
                },
                ""Service"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1F"",
                  ""encoding"": ""u32-le""
                },
                ""Sound Type"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x68"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""NE566"": 0,
                    ""NE555"": 1,
                    ""NE555-2"": 2
                  }
                },
                ""Tags"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""encoding"": ""gam-line"",
                  ""gamLine"": ""Tags <text>""
                },
                ""Test"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x3F"",
                  ""encoding"": ""u32-le""
                },
                ""Test / Run"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x7B"",
                  ""encoding"": ""u32-le"",
                  ""values"": {
                    ""C3"": 0,
                    ""C5"": 1,
                    ""C6"": 2
                  }
                },
                ""Test 2"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0xA9"",
                  ""encoding"": ""u32-le""
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
                  ""gamLine"": ""WIP 1 when checked""
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
                      ""sizeBytes"": 21,
                      ""encoding"": ""ascii-null-padded"",
                      ""field"": ""label""
                    },
                    ""0x95"": {
                      ""sizeBytes"": 4,
                      ""encoding"": ""vk-u8"",
                      ""field"": ""shortcut"",
                      ""note"": ""byte0=VK (0=none); byte1=mods; bytes2-3=auto label prefix (UI ignores)""
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
