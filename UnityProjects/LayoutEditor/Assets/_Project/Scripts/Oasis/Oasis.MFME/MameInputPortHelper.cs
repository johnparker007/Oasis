using Oasis.MAME;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.MFME
{
    public static class MameInputPortHelper
    {
        private const int kBitsPerPort = 8;

        public static string GetMamePortTag(int mfmeButtonNumber, MameController.PlatformType platformType)
        {
            switch(platformType)
            {
                case MameController.PlatformType.MPU4:
                    return GetMamePortTagMpu4(mfmeButtonNumber);
                case MameController.PlatformType.Impact:
                    return GetMamePortTagImpact(mfmeButtonNumber);
                default:
                    Debug.LogError("Not set up platform type " + platformType);
                    return "";
            }
        }

        // TODO just hacked these functions in from Arcade Sim converter code for the mo:

        public static string GetMamePortTagMpu4(int mfmeButtonNumber)
        {
            string[] portNames =
            {
                "ORANGE1",
                "ORANGE2",
                "BLACK1",
                "BLACK2",
                "AUX1",
                "AUX2",
                "DIL1",
                "DIL2",
            };

            int portNameIndex = mfmeButtonNumber / kBitsPerPort;

            return portNames[portNameIndex];
        }

        public static string GetMamePortTagImpact(int mfmeButtonNumber)
        {
            string[] portNames =
            {
                "???", // don't know
                "???", // don't know
                "J10_0", // guess
                "J10_1", // guess
                "J10_2",
                "J9_0",
                "J9_1", // guess
                "J9_2",
                "COIN_SENSE", // semi guess
                "COINS"
            };

            int portNameIndex = mfmeButtonNumber / kBitsPerPort;

            return portNames[portNameIndex];
        }

        // TODO check: can/should these be hex rather than dec?
        // 
        public static string GetMAMEPortInputMaskName(int mfmeButtonNumber)
        {
            int portInputNumber = mfmeButtonNumber % kBitsPerPort;

            string[] portInputMaskNames =
            {
                "1",
                "2",
                "4",
                "8",
                "16",
                "32",
                "64",
                "128",
            };
            string mask = portInputMaskNames[portInputNumber];

            return mask;
        }
    }
}
