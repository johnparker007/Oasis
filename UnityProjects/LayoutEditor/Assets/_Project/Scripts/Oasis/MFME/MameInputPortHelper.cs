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
                case MameController.PlatformType.Scorpion4:
                    return GetMamePortTagScorpion4(mfmeButtonNumber);
                default:
                    Debug.LogError("Not set up platform type " + platformType);
                    return "";
            }
        }

        // TODO just hacked these functions in from Arcade Sim converter code for the mo:
        // TOIMPROVE - refactor to single function that has array passed in or something,
        // to get rid of this duplicated copy/paste function.  Get some of this already written
        // in ArcadeSim source, under MAMELayoutInputHelper.cs
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

        public static string GetMamePortTagScorpion4(int mfmeButtonNumber)
        {
            string[] portNames =
            {
                "IN-0", "IN-1", "IN-2", "IN-3", "IN-4", "IN-5", "IN-6", "IN-7",
                "IN-8", "IN-9", "IN-10","IN-11","IN-12","IN-13","IN-14","IN-15",
                "IN-16","IN-17","IN-18","IN-19","IN-20","IN-21","IN-22","IN-23",
                "IN-24","IN-25","IN-26","IN-27","IN-28","IN-29","IN-30","IN-31",
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
