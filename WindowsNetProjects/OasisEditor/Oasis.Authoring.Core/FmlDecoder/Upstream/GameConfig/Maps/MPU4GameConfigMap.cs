// GameConfig map for MPU4.
// Editable JSON below. Optionally regenerate via tools/GenGameConfigMaps.

namespace MfmeFmlDecoder.GameConfig.Maps
{
    using MfmeFmlDecoder.GameConfig;

    internal static class MPU4GameConfigMap
    {
        public const string SystemName = "MPU4";

        private const string Json =
            @"{
              ""system"": ""MPU4"",
              ""controls"": {
                ""%"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""values"": {
                    """": 0,
                    ""70%"": 1,
                    ""72%"": 2,
                    ""74%"": 3,
                    ""76%"": 4,
                    ""78%"": 5,
                    ""80%"": 6,
                    ""82%"": 7,
                    ""84%"": 8,
                    ""86%"": 9,
                    ""88%"": 10,
                    ""90%"": 11,
                    ""92%"": 12,
                    ""94%"": 13,
                    ""96%"": 14,
                    ""98%"": 15
                  },
                  ""gamLine"": ""Percentage <index>""
                },
                ""7 Seg Display"": {
                  ""storage"": ""fml"",
                  ""tag"": ""0x09"",
                  ""values"": {
                    ""Barcrest"": 0,
                    ""Extended"": 1,
                    ""Extended 2"": 2,
                    ""Extended 3"": 3,
                    ""SWP"": 4,
                    ""Extended 4"": 5,
                    ""Extended 5"": 6,
                    ""Extended 6"": 7
                  },
                  ""kind"": ""dropdown""
                },
                ""Alpha Cable"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x70"",
                  ""values"": {
                    ""Normal"": 0,
                    ""C <-> R"": 1
                  }
                },
                ""Aux1 Invert"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x17"",
                  ""byteIndex"": 0,
                  ""bit"": 1,
                  ""mask"": 2
                },
                ""Aux2 Invert"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x17"",
                  ""byteIndex"": 0,
                  ""bit"": 0,
                  ""mask"": 1
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
                  ""encoding"": ""u32-le""
                },
                ""Character"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0F"",
                  ""values"": {
                    ""Barcrest"": 0,
                    ""BWB"": 1,
                    ""BWB Early"": 2,
                    ""Coinworld"": 3
                  }
                },
                ""DataPak"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1,
                    ""LoopBack"": 2
                  },
                  ""gamLine"": ""Protocol <index>""
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
                ""Door Invert"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1A"",
                  ""byteIndex"": 0,
                  ""bit"": 7,
                  ""mask"": 128
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
                ""Encryption"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x53"",
                  ""values"": {
                    ""Normal"": 0,
                    ""Crystal"": 1,
                    ""Crystal 2"": 2,
                    ""None"": 3
                  }
                },
                ""Extender Aux1"": {
                  ""storage"": ""fml"",
                  ""tag"": ""0x0A"",
                  ""values"": {
                    ""None"": 0,
                    ""Small"": 1,
                    ""Large 1"": 2,
                    ""Large 2"": 3,
                    ""Large 3"": 4,
                    ""Sml BWB"": 5,
                    ""Crystal"": 6
                  },
                  ""kind"": ""dropdown""
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
                  ""byteIndex"": null,
                  ""bit"": null,
                  ""defaultChecked"": false,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""LoTech 1 when checked""
                },
                ""LVD"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x16"",
                  ""values"": {
                    ""No"": 0,
                    ""Yes"": 1
                  }
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
                ""Mod Type"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x80"",
                  ""values"": {
                    ""2"": 0,
                    ""4"": 1
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
                ""Payout"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x07"",
                  ""values"": {
                    ""Tube"": 0,
                    ""Hopper 1"": 1,
                    ""Hopper 2"": 2,
                    ""Hopper 3"": 3,
                    ""Hopper 4"": 4,
                    ""Hopper 5"": 5,
                    ""Global Twin"": 6,
                    ""Prize"": 7,
                    ""Hopper 6"": 8,
                    ""Hopper 7"": 9,
                    ""Hopper 8"": 10,
                    ""Crystal Srl"": 11,
                    ""Prize 2"": 12,
                    ""Crystal 2"": 13
                  }
                },
                ""Prize"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""values"": {
                    """": 0,
                    ""£3"": 1,
                    ""£4"": 2,
                    ""£5"": 3,
                    ""£6"": 4,
                    ""£6T"": 5,
                    ""£8"": 6,
                    ""£8T"": 7,
                    ""£10"": 8,
                    ""£15"": 9,
                    ""£25"": 10,
                    ""£25LBO"": 11,
                    ""£35"": 12,
                    ""£70"": 13,
                    ""£100"": 14
                  },
                  ""gamLine"": ""Jackpot <index>""
                },
                ""Rating"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""gamLine"": ""Rating""
                },
                ""Reels"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x06"",
                  ""values"": {
                    ""3/4"": 0,
                    ""5a"": 1,
                    ""6"": 2,
                    ""5b"": 3,
                    ""5c"": 4,
                    ""7"": 5,
                    ""6b"": 6,
                    ""3/4/5"": 7
                  }
                },
                ""Refill"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x2B"",
                  ""encoding"": ""u32-le"",
                  ""emptyValue"": 255
                },
                ""ROM Paging"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x3B"",
                  ""values"": {
                    ""1/4/8 pages"": 0,
                    ""2 pages"": 1,
                    ""8 pages 2"": 2
                  }
                },
                ""Service"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x1F"",
                  ""encoding"": ""u32-le""
                },
                ""Sound"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0E"",
                  ""values"": {
                    ""Sample"": 0,
                    ""Yamaha"": 4,
                    ""Crystal"": 5,
                    ""AY Only"": 6,
                    ""Serial"": 7,
                    ""Coinworld"": 8,
                    ""Sample Only"": 9
                  }
                },
                ""Stake"": {
                  ""kind"": ""dropdown"",
                  ""storage"": ""gam"",
                  ""values"": {
                    """": 0,
                    ""5p"": 1,
                    ""10p"": 2,
                    ""20p"": 3,
                    ""25p"": 4,
                    ""30p"": 5,
                    ""50p"": 6,
                    ""£1"": 7
                  },
                  ""gamLine"": ""Stake <index>""
                },
                ""Tags"": {
                  ""kind"": ""text"",
                  ""storage"": ""gam"",
                  ""gamLine"": ""Tags""
                },
                ""Test"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x3F"",
                  ""encoding"": ""u32-le"",
                  ""emptyValue"": 255
                },
                ""Top Up"": {
                  ""kind"": ""switch-number"",
                  ""storage"": ""fml"",
                  ""encoding"": ""mirrored-u32-le"",
                  ""emptyValue"": 255,
                  ""alsoWrites"": [
                    ""0xA4""
                  ],
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
                ""Type"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x28"",
                  ""values"": {
                    ""Reel"": 0,
                    ""Video"": 1
                  }
                },
                ""Version"": {
                  ""kind"": ""text"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x0D"",
                  ""encoding"": ""ascii-null-padded"",
                  ""alsoWritesGam"": ""Version <text>""
                },
                ""Volume Control"": {
                  ""kind"": ""radio"",
                  ""storage"": ""fml"",
                  ""tag"": ""0x8A"",
                  ""values"": {
                    ""Auto"": 0,
                    ""Manual"": 1
                  }
                },
                ""WIP"": {
                  ""kind"": ""checkbox"",
                  ""storage"": ""gam"",
                  ""byteIndex"": null,
                  ""bit"": null,
                  ""defaultChecked"": false,
                  ""values"": {
                    ""unchecked"": 0,
                    ""checked"": 1
                  },
                  ""gamLine"": ""WIP 1 when checked""
                }
              }
            }";

        public static GameConfigMap Create() =>
            GameConfigMap.FromJson(SystemName, "embedded:" + SystemName, Json);
    }
}
