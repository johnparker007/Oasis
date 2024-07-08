using Oasis.MAME;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oasis.Project
{
    [Serializable]
    public class SettingsData
    {
        [Serializable]
        public class MameData
        {
            public string RomName;
        }

        [Serializable]
        public class FruitMachineData
        {
            public MameController.PlatformType Platform;
        }

        public MameData Mame
        {
            get;
            set;
        }

        public FruitMachineData FruitMachine
        {
            get;
            set;
        }

        public SettingsData()
        {
            Mame = new MameData();
            FruitMachine = new FruitMachineData();
        }
    }
}
