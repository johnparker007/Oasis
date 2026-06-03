namespace OasisEditor;

public readonly record struct MameInputCommandTarget(string Tag, string Mask);

public interface IMameInputPortResolver
{
    bool TryResolve(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, out MameInputCommandTarget target);
}

public sealed class MameInputPortResolver : IMameInputPortResolver
{
    private const int BitsPerPort = 8;

    public bool TryResolve(FruitMachinePlatformType platform, InputDefinitionModel inputDefinition, out MameInputCommandTarget target)
    {
        target = default;
        ArgumentNullException.ThrowIfNull(inputDefinition);

        if (inputDefinition.CoinInput || inputDefinition.Kind == InputDefinitionKind.Coin)
        {
            target = new MameInputCommandTarget("COINS", "1");
            return true;
        }

        if (!int.TryParse(inputDefinition.ButtonNumber, out var buttonNumber) || buttonNumber < 0)
        {
            return false;
        }

        if (!TryResolveTag(platform, buttonNumber, out var tag))
        {
            return false;
        }

        var mask = GetMask(buttonNumber);
        target = new MameInputCommandTarget(tag, mask);
        return true;
    }

    private static bool TryResolveTag(FruitMachinePlatformType platform, int mfmeButtonNumber, out string tag)
    {
        tag = string.Empty;
        var idx = mfmeButtonNumber / BitsPerPort;

        string[] names = platform switch
        {
            FruitMachinePlatformType.MPU4 => ["ORANGE1", "ORANGE2", "BLACK1", "BLACK2", "AUX1", "AUX2", "DIL1", "DIL2"],
            FruitMachinePlatformType.Impact => ["???", "???", "J10_0", "J10_1", "J10_2", "J9_0", "J9_1", "J9_2", "COIN_SENSE", "COINS"],
            FruitMachinePlatformType.Epoch => ["IN0", "IN1", "COINS", "STAKE", "REELS", "AUX", "CAB1", "CAB2", "DSW1", "DSW2"],
            FruitMachinePlatformType.Scorpion4 => BuildScorpion4(),
            _ => []
        };

        if (idx < 0 || idx >= names.Length)
        {
            return false;
        }

        tag = names[idx];
        return !string.IsNullOrWhiteSpace(tag);
    }

    private static string[] BuildScorpion4()
    {
        var names = new string[32];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = $"IN-{i}";
        }

        return names;
    }

    private static string GetMask(int mfmeButtonNumber)
    {
        var bit = mfmeButtonNumber % BitsPerPort;
        return bit switch
        {
            0 => "1",
            1 => "2",
            2 => "4",
            3 => "8",
            4 => "16",
            5 => "32",
            6 => "64",
            _ => "128"
        };
    }
}
