using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractConfigurationMPU4: ExtractConfigurationBase
    {
        public enum VolumeControlType
        {
            Auto,
            Manual
        }

        public enum RomPagingType
        {
            _1_4_8Pages,
            _2Pages,
            _8Pages2
        }

        public enum LVDType
        {
            No,
            Yes
        }

        public enum DisplayType
        {
            Reel,
            Video
        }

        public enum LampTestType
        {
            Pass,
            Fail
        }

        public enum AlphaCableType
        {
            Normal,
            CR
        }

        public enum ModTypes
        {
            Two,
            Four
        }

        public enum CabinetStyleType
        {
            Default,
            Rio,
            Genesis
        }


        [Serializable]
        public class MeterElement
        {
            public string TypeAsString;
            public string MultiplierAsString;
            public bool In;
        }

        public static readonly int kMeterElementCount = 6;
        public static readonly int kCharacteriserLampCount = 8;


        public MeterElement[] MeterElements = new MeterElement[kMeterElementCount];
        public string[] CharacteriserLamps = new string[kCharacteriserLampCount];

        public string Stake;
        public string Prize;
        public string Percentage;

        public VolumeControlType VolumeControl;

        public string RomPagingAsString;

        public LVDType LVD;

        public DisplayType Display;

        public LampTestType LampTest;

        public string PayoutAsString;
        public string ExtenderAux1AsString;
        public string SevenSegDisplayAsString;
        public string ReelsAsString;
        public string SoundAsString;
        public string EncryptionAsString;
        public string CharacterAsString;
        public string DataPakAsString;

        public string SwitchServiceAsString;
        public string SwitchCashAsString;
        public string SwitchRefillAsString;
        public string SwitchTestAsString;
        public string SwitchTopUpAsString;

        public bool Aux1Invert;
        public bool Aux2Invert;
        public bool DoorInvert;

        public AlphaCableType AlphaCable;
        public ModTypes ModType;
        public CabinetStyleType CabinetStyle;          


        public ExtractConfigurationMPU4()
        {
            for (int meterElementIndex = 0; meterElementIndex < kMeterElementCount; ++meterElementIndex)
            {
                MeterElements[meterElementIndex] = new MeterElement();
                MeterElements[meterElementIndex].In = meterElementIndex < kMeterElementCount / 2;
            }
        }
    }

}
