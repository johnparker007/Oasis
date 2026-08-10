using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed record FabricAmberM1Configuration(M1ProjectSettings Settings) : IFabricBackendConfiguration
{
    public const uint ConfigurationMagic = 0x314D4146, ConfigurationVersion = 1;
    public const int ReelCount = 6, DipCount = 16, HopperCount = 2, ConfigurationSize = 148;

    public static FabricAmberM1Configuration FromM1(M1ProjectSettings settings) { Validate(settings); return new(settings); }
    public static void Validate(M1ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateIndexed(settings.Reels.Select(x => x.ReelIndex), ReelCount, "reels");
        ValidateIndexed(settings.Hoppers.Select(x => x.HopperIndex), HopperCount, "hoppers");
        ValidateIndexed(settings.ProgramRoms.Select(x => x.Slot), 4, "program ROMs");
        ValidateIndexed(settings.SoundRoms.Select(x => x.Slot), 4, "sound ROMs");
        if (settings.Dips.Count != DipCount) throw new ArgumentException("M1 requires exactly 16 DIP switches.");
        if (settings.PercentageKey is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(settings.PercentageKey));
        foreach (var r in settings.Reels) if (r.Steps is < 1 or > 255 || r.OptoStart is < 0 or > 255 || r.OptoEnd is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(settings.Reels), "M1 reel byte values are out of range.");
        foreach (var h in settings.Hoppers) if (h.OptoReturn is < 0 or > 255 || h.Coin is < 0 or > 255 || h.LoSwitch is < 0 or > 255 || h.HiSwitch is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(settings.Hoppers), "M1 hopper byte values are out of range.");
    }
    private static void ValidateIndexed(IEnumerable<int> indices, int count, string name) { var values = indices.ToArray(); if (values.Length != count || !values.Order().SequenceEqual(Enumerable.Range(0, count))) throw new ArgumentException($"M1 requires exactly {count} uniquely indexed {name}."); }

    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings);
        if (Marshal.SizeOf<FabricAmberM1Config>() != ConfigurationSize) throw new InvalidOperationException("Managed M1 configuration ABI is not 148 bytes.");
        var native = new FabricAmberM1Config { Magic=ConfigurationMagic, StructSize=ConfigurationSize, Version=ConfigurationVersion, ReelCount=ReelCount, PercentageKey=(byte)Settings.PercentageKey, EdcEnabled=Settings.EdcEnabled?(byte)1:(byte)0, HopperCount=HopperCount };
        var reels=(FabricAmberM1ReelConfig*)native.Reels; foreach(var r in Settings.Reels) reels[r.ReelIndex]=new(){Steps=(byte)r.Steps,OptoStart=(byte)r.OptoStart,OptoEnd=(byte)r.OptoEnd,OptoInvert=r.OptoInvert?(byte)1:(byte)0};
        for(var i=0;i<DipCount;i++) native.Dips[i]=Settings.Dips[i]?(byte)1:(byte)0;
        var hoppers=(FabricAmberM1HopperConfig*)native.Hoppers; foreach(var h in Settings.Hoppers) hoppers[h.HopperIndex]=new(){Enabled=B(h.Enabled),OptoEnable=B(h.OptoEnable),OptoReturn=(byte)h.OptoReturn,MotorEnable=B(h.MotorEnable),Coin=(byte)h.Coin,LoEnable=B(h.LoEnable),LoInvert=B(h.LoInvert),LoSwitch=(byte)h.LoSwitch,HiEnable=B(h.HiEnable),HiInvert=B(h.HiInvert),HiSwitch=(byte)h.HiSwitch,LoIndicator=B(h.LoIndicator),HiIndicator=B(h.HiIndicator),CoinsIn=h.CoinsIn,CoinsOut=h.CoinsOut,Level=h.Level,FullLevel=h.FullLevel,LoLevel=h.LoLevel,HiLevel=h.HiLevel,CoinsRefilled=h.CoinsRefilled};
        var bytes=new byte[ConfigurationSize]; fixed(byte* p=bytes) Buffer.MemoryCopy(&native,p,ConfigurationSize,ConfigurationSize); return bytes;
    }
    private static byte B(bool value)=>value?(byte)1:(byte)0;
}
