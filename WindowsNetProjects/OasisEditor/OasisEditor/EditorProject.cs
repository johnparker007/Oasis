namespace OasisEditor;

public sealed class EditorProject
{
    public required string Name { get; init; }
    public required string ProjectFilePath { get; init; }
    public required string ProjectDirectory { get; init; }
    public required string AssetsDirectory { get; init; }
    public required string MachinesDirectory { get; init; }
    public required string GeneratedDirectory { get; init; }
    public FruitMachinePlatformType FruitMachinePlatform { get; set; } = FruitMachinePlatformType.None;
    public string MameRomName { get; set; } = string.Empty;
    public bool AutomaticallyDownloadMissingRoms { get; set; } = true;
    public System6NativeRomSettings System6NativeRoms { get; set; } = new();
    public List<InputDefinitionModel> InputDefinitions { get; } = [];
}


public sealed class System6NativeRomSettings
{
    public const int DefaultReelOptoSlotCount = 8;
    public const int DefaultCoinSlotCount = 4;
    public const int DefaultPercentSwitchValue = 0;
    public List<System6ReelOptoSettings> ReelOptos { get; set; } = CreateDefaultReelOptos();
    public List<System6CoinSettings> Coins { get; set; } = CreateDefaultCoins();

    public string ProgramRom1Path { get; set; } = string.Empty;
    public string ProgramRom2Path { get; set; } = string.Empty;
    public string ProgramRom3Path { get; set; } = string.Empty;
    public string ProgramRom4Path { get; set; } = string.Empty;
    public string SoundRom1Path { get; set; } = string.Empty;
    public string SoundRom2Path { get; set; } = string.Empty;
    public string SoundRom3Path { get; set; } = string.Empty;
    public string SoundRom4Path { get; set; } = string.Empty;
    public bool FlashSwitch { get; set; }
    public int PercentSwitchValue { get; set; } = DefaultPercentSwitchValue;

    public IReadOnlyList<string> ProgramRomPaths => [ProgramRom1Path, ProgramRom2Path, ProgramRom3Path, ProgramRom4Path];
    public IReadOnlyList<string> SoundRomPaths => [SoundRom1Path, SoundRom2Path, SoundRom3Path, SoundRom4Path];

    public static List<System6ReelOptoSettings> CreateDefaultReelOptos()
    {
        var reelOptos = new List<System6ReelOptoSettings>(DefaultReelOptoSlotCount);
        for (var reelIndex = 0; reelIndex < DefaultReelOptoSlotCount; reelIndex++)
        {
            reelOptos.Add(System6ReelOptoSettings.CreateDefault(reelIndex));
        }

        return reelOptos;
    }

    public static List<System6CoinSettings> CreateDefaultCoins()
    {
        var coins = new List<System6CoinSettings>(DefaultCoinSlotCount);
        for (var coinIndex = 0; coinIndex < DefaultCoinSlotCount; coinIndex++)
        {
            coins.Add(System6CoinSettings.CreateDefault(coinIndex));
        }

        return coins;
    }
}

public sealed class System6ReelOptoSettings
{
    public const int DefaultSteps = 96;
    public const int DefaultOptoStart = 5;
    public const int DefaultOptoEnd = 7;
    public const bool DefaultEnabled = true;
    public const bool DefaultOptoInvert = false;

    public int ReelIndex { get; set; }
    public bool Enabled { get; set; } = DefaultEnabled;
    public int Steps { get; set; } = DefaultSteps;
    public int OptoStart { get; set; } = DefaultOptoStart;
    public int OptoEnd { get; set; } = DefaultOptoEnd;
    public bool OptoInvert { get; set; } = DefaultOptoInvert;

    public static System6ReelOptoSettings CreateDefault(int reelIndex) => new()
    {
        ReelIndex = reelIndex,
        Enabled = DefaultEnabled,
        Steps = DefaultSteps,
        OptoStart = DefaultOptoStart,
        OptoEnd = DefaultOptoEnd,
        OptoInvert = DefaultOptoInvert
    };
}


public sealed class System6CoinSettings
{
    public const bool DefaultEnabled = false;
    public const int DefaultCoin = 0;
    public const int DefaultCoinValue = 0;
    public const int DefaultCoinEnable = 1;
    public const int DefaultLockoutValue = 0;
    public const int DefaultLockoutInvert = 0;

    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = DefaultEnabled;
    public int Num { get; set; }
    public int Coin { get; set; } = DefaultCoin;
    public int CoinValue { get; set; } = DefaultCoinValue;
    public int CoinEnable { get; set; } = DefaultCoinEnable;
    public int LockoutValue { get; set; } = DefaultLockoutValue;
    public int LockoutInvert { get; set; } = DefaultLockoutInvert;
    public int CounterIn { get; set; }
    public int CounterOut { get; set; }
    public int PortIndex { get; set; }
    public int Level { get; set; }
    public int FullLevel { get; set; }

    public static System6CoinSettings CreateDefault(int coinIndex) => new()
    {
        Name = $"Coin {coinIndex + 1}",
        Enabled = DefaultEnabled,
        Num = coinIndex,
        Coin = DefaultCoin,
        CoinValue = DefaultCoinValue,
        CoinEnable = DefaultCoinEnable,
        LockoutValue = DefaultLockoutValue,
        LockoutInvert = DefaultLockoutInvert
    };
}
