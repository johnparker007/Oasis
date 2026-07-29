using System.Runtime.InteropServices;

namespace OasisEditor;

public sealed record FabricAmberReel(uint Index,bool Enabled,uint Steps,uint OptoStart,uint OptoEnd,bool OptoInvert);
public sealed record FabricAmberCoinChannel(uint Index,bool Enabled,uint Value,bool LockoutInvert);
public sealed record FabricAmberCoinRoute(uint Index,bool Enabled,uint CounterIn,uint CounterOut,uint PortIndex,uint CoinCode,uint Level,uint FullLevel);
public sealed record FabricAmberConfiguration(uint ReelApplyMask,IReadOnlyList<FabricAmberReel> Reels,uint CoinChannelApplyMask,
    uint CoinRouteApplyMask,IReadOnlyList<FabricAmberCoinChannel> CoinChannels,IReadOnlyList<FabricAmberCoinRoute> CoinRoutes,uint? PercentageSwitch) : IFabricBackendConfiguration
{
    public static FabricAmberConfiguration FromSystem6(System6NativeRomSettings s)
    {
        var reels=s.ReelOptos.Select(x=>new FabricAmberReel(checked((uint)x.ReelIndex),x.Enabled,checked((uint)x.Steps),checked((uint)x.OptoStart),checked((uint)x.OptoEnd),x.OptoInvert)).ToArray();
        var coins=s.Coins.Where(x=>x.Enabled).ToArray();
        return new(reels.Aggregate(0u,(m,x)=>m|1u<<(int)x.Index),reels,
            coins.Aggregate(0u,(m,x)=>m|1u<<x.Num),coins.Aggregate(0u,(m,x)=>m|1u<<x.Num),
            coins.Select(x=>new FabricAmberCoinChannel((uint)x.Num,x.CoinEnable!=0,(uint)x.CoinValue,x.LockoutInvert!=0)).ToArray(),
            coins.Select(x=>new FabricAmberCoinRoute((uint)x.Num,x.Enabled,(uint)x.CounterIn,(uint)x.CounterOut,(uint)x.PortIndex,(uint)x.Coin,(uint)x.Level,(uint)x.FullLevel)).ToArray(),
            checked((uint)Math.Clamp(s.PercentSwitchValue,0,15)));
    }
    public unsafe byte[] ToNativeBytes()
    {
        if(Reels.Count>8||CoinChannels.Count>6||CoinRoutes.Count>8)throw new ArgumentException("Amber configuration exceeds ABI capacities.");
        var n=new FabricAmberConfigurationNative{Magic=0x32424146,Size=(uint)sizeof(FabricAmberConfigurationNative),Version=1,Flags=(Reels.Count>0?1u:0)|(CoinChannels.Count>0||CoinRoutes.Count>0?2u:0)|(PercentageSwitch.HasValue?4u:0),Percentage=PercentageSwitch??0};
        n.Reels.Size=(uint)sizeof(AmberReelsNative);n.Reels.Version=1;n.Reels.Count=(uint)Reels.Count;n.Reels.ApplyMask=ReelApplyMask;n.Coins.Size=(uint)sizeof(AmberCoinsNative);n.Coins.Version=1;n.Coins.ChannelMask=CoinChannelApplyMask;n.Coins.RouteMask=CoinRouteApplyMask;
        fixed(byte* p=n.Reels.Reels)for(var i=0;i<Reels.Count;i++)((AmberReelNative*)p)[i]=new(){Index=Reels[i].Index,Enabled=Reels[i].Enabled?1u:0,Steps=Reels[i].Steps,OptoStart=Reels[i].OptoStart,OptoEnd=Reels[i].OptoEnd,OptoInvert=Reels[i].OptoInvert?1u:0};
        fixed(byte* p=n.Coins.Channels)for(var i=0;i<CoinChannels.Count;i++)((AmberCoinChannelNative*)p)[i]=new(){Index=CoinChannels[i].Index,Enabled=CoinChannels[i].Enabled?1u:0,Value=CoinChannels[i].Value,LockoutInvert=CoinChannels[i].LockoutInvert?1u:0};
        fixed(byte* p=n.Coins.Routes)for(var i=0;i<CoinRoutes.Count;i++)((AmberCoinRouteNative*)p)[i]=new(){Index=CoinRoutes[i].Index,Enabled=CoinRoutes[i].Enabled?1u:0,CounterIn=CoinRoutes[i].CounterIn,CounterOut=CoinRoutes[i].CounterOut,PortIndex=CoinRoutes[i].PortIndex,CoinCode=CoinRoutes[i].CoinCode,Level=CoinRoutes[i].Level,FullLevel=CoinRoutes[i].FullLevel};
        var bytes=new byte[sizeof(FabricAmberConfigurationNative)];fixed(byte* p=bytes)Buffer.MemoryCopy(&n,p,bytes.Length,bytes.Length);return bytes;
    }
}
