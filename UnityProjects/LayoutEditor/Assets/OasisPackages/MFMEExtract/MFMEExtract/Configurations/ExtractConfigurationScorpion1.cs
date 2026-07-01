using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MFMEExtract
{
    [Serializable]
    public class ExtractConfigurationScorpion1: ExtractConfigurationBase
    {
        [Serializable]
        public class MeterElement
        {
            public string TypeAsString;
            public string MultiplierAsString;
            public bool In;
        }

        public enum SampledSoundType
        {
            NEC,
            OKI,
            Global
        }

        public static readonly int kMeterElementCount = 6;

        public MeterElement[] MeterElements = new MeterElement[kMeterElementCount];

        public string Stake;
        public string Prize;
        public string Percentage;

        public string EncryptionAsString;

        public string SwitchServiceAsString;
        public string SwitchCashAsString;
        public string SwitchRefillAsString;
        public string SwitchTestAsString;
        public string SwitchPaySense1AsString;
        public string SwitchPaySense2AsString;
        public string SwitchPaySense3AsString;
        public string SwitchPaySense4AsString;
        public string SwitchDMBusyAsString;

        public string DataPakAsString;

        public SampledSoundType SampledSound;


        public ExtractConfigurationScorpion1()
        {
            for (int meterElementIndex = 0; meterElementIndex < kMeterElementCount; ++meterElementIndex)
            {
                MeterElements[meterElementIndex] = new MeterElement();
                MeterElements[meterElementIndex].In = meterElementIndex < kMeterElementCount / 2;
            }
        }
    }

}
