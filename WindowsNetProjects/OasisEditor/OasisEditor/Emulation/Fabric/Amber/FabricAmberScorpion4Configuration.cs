using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed record FabricAmberScorpion4Configuration(Scorpion4ProjectSettings Settings) : IFabricBackendConfiguration
{
    public const uint ConfigurationMagic = 0x34534146, ConfigurationVersion = 1;
    public const int ReelCount = 6, DipCount = 16, CoinChannelCount = 6, HopperCount = 2, ConfigurationSize = 152;
    public static FabricAmberScorpion4Configuration FromScorpion4(Scorpion4ProjectSettings settings) { Validate(settings); return new(settings); }
    public static void Validate(Scorpion4ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Indexed(settings.ProgramRoms.Select(x => x.Slot), 4, "program ROMs"); Indexed(settings.SoundRoms.Select(x => x.Slot), 4, "sound ROMs");
        Indexed(settings.Reels.Select(x => x.ReelIndex), ReelCount, "reels"); Indexed(settings.Coins.Select(x => x.ChannelIndex), CoinChannelCount, "coin channels"); Indexed(settings.Hoppers.Select(x => x.HopperIndex), HopperCount, "hoppers");
        if (settings.Dips.Count != DipCount) throw new ArgumentException("Scorpion 4 requires exactly 16 DIP switches.");
        if (settings.Stake is < 0 or > 7 || settings.Prize is < 0 or > 15 || settings.Percentage is < 0 or > 31 || settings.HopperType is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(settings), "Scorpion 4 selector is out of range.");
        foreach (var r in settings.Reels) if (r.Steps is < 1 or > 255 || r.OptoStart is < 0 or > 255 || r.OptoEnd is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(settings.Reels));
        foreach (var c in settings.Coins) if (c.Value is not (>= 0 and <= 5) and not 0xff) throw new ArgumentOutOfRangeException(nameof(settings.Coins), "Coin value must be 0..5 or 255.");
        foreach (var h in settings.Hoppers) if (h.Coin is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(settings.Hoppers));
    }
    private static void Indexed(IEnumerable<int> source, int count, string name) { var a=source.ToArray(); if(a.Length!=count || !a.Order().SequenceEqual(Enumerable.Range(0,count))) throw new ArgumentException($"Scorpion 4 requires exactly {count} uniquely indexed {name}."); }
    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings); if(Marshal.SizeOf<FabricAmberScorpion4Config>()!=ConfigurationSize) throw new InvalidOperationException("Managed Scorpion 4 configuration ABI is not 152 bytes.");
        var n=new FabricAmberScorpion4Config { Magic=ConfigurationMagic, StructSize=ConfigurationSize, Version=ConfigurationVersion, ReelCount=ReelCount, Stake=(byte)Settings.Stake, Prize=(byte)Settings.Prize, Percentage=(byte)Settings.Percentage, EdcEnabled=B(Settings.EdcEnabled), HopperType=(byte)Settings.HopperType, HopperCount=HopperCount, CoinChannelCount=CoinChannelCount };
        var reels=(FabricAmberScorpion4ReelConfig*)n.Reels; foreach(var r in Settings.Reels) reels[r.ReelIndex]=new(){Steps=(byte)r.Steps,OptoStart=(byte)r.OptoStart,OptoEnd=(byte)r.OptoEnd,OptoInvert=B(r.OptoInvert)};
        for(var i=0;i<DipCount;i++) n.Dips[i]=B(Settings.Dips[i]);
        var coins=(FabricAmberScorpion4CoinConfig*)n.Coins; foreach(var c in Settings.Coins) coins[c.ChannelIndex]=new(){Enabled=B(c.Enabled),Value=(byte)c.Value};
        var hoppers=(FabricAmberScorpion4HopperConfig*)n.Hoppers; foreach(var h in Settings.Hoppers) hoppers[h.HopperIndex]=new(){Enabled=B(h.Enabled),Coin=(byte)h.Coin,LoEnable=B(h.LoEnabled),HiEnable=B(h.HiEnabled),CoinsIn=h.CoinsIn,CoinsOut=h.CoinsOut,Level=h.Level,FullLevel=h.FullLevel,LoLevel=h.LoLevel,HiLevel=h.HiLevel,CoinsRefilled=h.CoinsRefilled};
        var bytes=new byte[ConfigurationSize]; fixed(byte* p=bytes) Buffer.MemoryCopy(&n,p,ConfigurationSize,ConfigurationSize); return bytes;
    }
    private static byte B(bool value)=>value?(byte)1:(byte)0;
}
