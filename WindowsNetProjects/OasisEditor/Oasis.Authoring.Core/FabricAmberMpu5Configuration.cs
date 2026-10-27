namespace OasisEditor;

public sealed record FabricAmberMpu5Configuration(Mpu5NativeRomSettings Settings) : IFabricBackendConfiguration
{
    public const uint Magic = 0x354D4146, Version = 1;
    public const uint ConfigureReels=1, ConfigureCoins=2, ConfigureOptions=4;
    public const uint OptionDips=1, OptionStake=2, OptionPrize=4, OptionPercentage=8,
        OptionCharacteriserAddress=16, OptionPicMode=32, OptionSecFitted=64, OptionHopperType=128,
        OptionReelJumper0=256, OptionReelJumper1=512;

    public static FabricAmberMpu5Configuration? FromMpu5(Mpu5NativeRomSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        return settings.ConfigureReels || settings.ConfigureCoins || settings.ConfigureMachineOptions ? new(settings) : null;
    }

    internal static void Validate(Mpu5NativeRomSettings s)
    {
        ValidateIndexed(s.Reels, 8, x=>x.ReelIndex, "reel");
        foreach(var r in s.Reels) { Range(r.Steps,1,255,"MPU5 reel steps"); Range(r.OptoStart,0,255,"MPU5 opto start"); Range(r.OptoEnd,0,255,"MPU5 opto end"); }
        ValidateIndexed(s.Coins, 6, x=>x.Channel, "coin channel");
        foreach(var c in s.Coins) Range(c.Value,0,255,"MPU5 coin value");
        if(s.ConfigureCoins && s.PulseCycles==0) throw new ArgumentOutOfRangeException(nameof(s.PulseCycles),"MPU5 coin pulse cycles must be nonzero.");
        Range((int)s.CommunicationStyle,0,3,"MPU5 communication style"); Range((int)s.PicMode,1,3,"MPU5 PIC mode");
        Range((int)s.HopperType,0,3,"MPU5 hopper type"); Range((int)s.ReelJumperProfile0,0,2,"MPU5 reel jumper profile 0"); Range((int)s.ReelJumperProfile1,0,2,"MPU5 reel jumper profile 1");
        if(s.DipSwitchBits>0xffff) throw new ArgumentOutOfRangeException(nameof(s.DipSwitchBits),"MPU5 DIP switches use only the lower 16 bits.");
        if(s.Stake>255 || s.Prize>255 || s.Percentage>15) throw new ArgumentOutOfRangeException(nameof(s),"MPU5 stake/prize must be 0..255 and percentage 0..15.");
    }
    private static void Range(int value,int min,int max,string name) { if(value<min||value>max) throw new ArgumentOutOfRangeException(name,value,$"{name} must be {min}..{max}."); }
    private static void ValidateIndexed<T>(IReadOnlyList<T> values,int capacity,Func<T,int> index,string name)
    { if(values.Count>capacity) throw new ArgumentException($"MPU5 supports at most {capacity} {name} entries."); var seen=new HashSet<int>(); foreach(var value in values){var i=index(value); if(i<0||i>=capacity) throw new ArgumentOutOfRangeException(name,i,$"MPU5 {name} index must be 0..{capacity-1}."); if(!seen.Add(i)) throw new ArgumentException($"Duplicate MPU5 {name} index {i}.");} }

    public unsafe byte[] ToNativeBytes()
    {
        Validate(Settings);
        var n=new FabricAmberMpu5ConfigurationNative { Magic=Magic, Size=(uint)sizeof(FabricAmberMpu5ConfigurationNative), Version=Version };
        n.Reels.Size=(uint)sizeof(FabricAmberMpu5ReelConfigurationNative); n.Reels.Version=1; n.Reels.Count=8;
        n.Coins.Size=(uint)sizeof(FabricAmberMpu5CoinConfigurationNative); n.Coins.Version=1; n.Coins.Count=6;
        n.Options.Size=(uint)sizeof(FabricAmberMpu5OptionsNative); n.Options.Version=1;
        if(Settings.ConfigureReels) n.Flags|=ConfigureReels;
        if(Settings.ConfigureCoins) n.Flags|=ConfigureCoins;
        if(Settings.ConfigureMachineOptions) n.Flags|=ConfigureOptions;
        foreach(var r in Settings.Reels){if(r.Apply)n.Reels.ApplyMask|=1u<<r.ReelIndex; ((FabricAmberMpu5ReelConfigNative*)n.Reels.Reels)[r.ReelIndex]=new(){ReelIndex=(uint)r.ReelIndex,Steps=(uint)r.Steps,OptoStart=(uint)r.OptoStart,OptoEnd=(uint)r.OptoEnd,OptoInvert=r.OptoInvert?1u:0};}
        n.Coins.CommunicationStyle=(uint)Settings.CommunicationStyle;n.Coins.CommunicationInvert=Settings.CommunicationInvert?1u:0;n.Coins.PulseCycles=Settings.PulseCycles;n.Coins.EdcEnabled=Settings.EdcEnabled?1u:0;
        foreach(var c in Settings.Coins){if(c.Apply)n.Coins.ApplyMask|=1u<<c.Channel; ((FabricAmberMpu5CoinChannelConfigNative*)n.Coins.Channels)[c.Channel]=new(){ChannelIndex=(uint)c.Channel,Enabled=c.Enabled?1u:0,Value=(uint)c.Value,LockoutInvert=c.LockoutInvert?1u:0};}
        n.Options.ApplyMask=OptionMask(Settings); n.Options.DipSwitchBits=Settings.DipSwitchBits;n.Options.Stake=Settings.Stake;n.Options.Prize=Settings.Prize;n.Options.Percentage=Settings.Percentage;n.Options.CharacteriserAddress=Settings.CharacteriserAddress;n.Options.PicMode=(uint)Settings.PicMode;n.Options.SecFitted=Settings.SecFitted?1u:0;n.Options.HopperType=(uint)Settings.HopperType;n.Options.ReelJumperProfile0=(uint)Settings.ReelJumperProfile0;n.Options.ReelJumperProfile1=(uint)Settings.ReelJumperProfile1;
        var bytes=new byte[sizeof(FabricAmberMpu5ConfigurationNative)]; fixed(byte* p=bytes) Buffer.MemoryCopy(&n,p,bytes.Length,bytes.Length); return bytes;
    }
    private static uint OptionMask(Mpu5NativeRomSettings s) => (s.ApplyDips?OptionDips:0)|(s.ApplyStake?OptionStake:0)|(s.ApplyPrize?OptionPrize:0)|(s.ApplyPercentage?OptionPercentage:0)|(s.ApplyCharacteriserAddress?OptionCharacteriserAddress:0)|(s.ApplyPicMode?OptionPicMode:0)|(s.ApplySecFitted?OptionSecFitted:0)|(s.ApplyHopperType?OptionHopperType:0)|(s.ApplyReelJumperProfile0?OptionReelJumper0:0)|(s.ApplyReelJumperProfile1?OptionReelJumper1:0);
}
