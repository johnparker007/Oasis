using System.Runtime.InteropServices;
using Xunit;
namespace OasisEditor.Tests;
public sealed class Scorpion4SupportTests
{
 [Fact] public void SchemaAndIndependentDefaults(){Assert.Equal(8,EditorProject.CurrentSchemaVersion);Assert.True(Enum.IsDefined(FruitMachinePlatformType.Scorpion4));var a=new Scorpion4ProjectSettings();var b=new Scorpion4ProjectSettings();Assert.Equal(4,a.ProgramRoms.Count);Assert.Equal(4,a.SoundRoms.Count);Assert.Equal(6,a.Reels.Count);Assert.Equal(16,a.Dips.Count);Assert.Equal(6,a.Coins.Count);Assert.Equal(2,a.Hoppers.Count);Assert.All(a.Reels,x=>{Assert.Equal(96,x.Steps);Assert.Equal(0,x.OptoStart);Assert.Equal(0,x.OptoEnd);Assert.False(x.OptoInvert);});Assert.All(a.Coins,x=>{Assert.True(x.Enabled);Assert.Equal(0xff,x.Value);});a.Dips[0]=true;Assert.False(b.Dips[0]);Assert.DoesNotContain(typeof(Scorpion4RomSettings).GetProperties(),x=>x.Name.Contains("Address"));}
 [Fact] public void AbiLayout(){Assert.Equal(4,Marshal.SizeOf<FabricAmberScorpion4ReelConfig>());Assert.Equal(4,Marshal.SizeOf<FabricAmberScorpion4CoinConfig>());Assert.Equal(32,Marshal.SizeOf<FabricAmberScorpion4HopperConfig>());Assert.Equal(152,Marshal.SizeOf<FabricAmberScorpion4Config>());var expected=new Dictionary<string,int>{{"Magic",0},{"StructSize",4},{"Version",8},{"ReelCount",12},{"Reels",16},{"Dips",40},{"Stake",56},{"Prize",57},{"Percentage",58},{"EdcEnabled",59},{"HopperType",60},{"HopperCount",61},{"CoinChannelCount",62},{"Reserved0",63},{"Coins",64},{"Hoppers",88}};foreach(var x in expected)Assert.Equal(x.Value,Marshal.OffsetOf<FabricAmberScorpion4Config>(x.Key).ToInt32());var hopper=new Dictionary<string,int>{{"Enabled",0},{"Coin",1},{"LoEnable",2},{"HiEnable",3},{"CoinsIn",4},{"CoinsOut",8},{"Level",12},{"FullLevel",16},{"LoLevel",20},{"HiLevel",24},{"CoinsRefilled",28}};foreach(var x in hopper)Assert.Equal(x.Value,Marshal.OffsetOf<FabricAmberScorpion4HopperConfig>(x.Key).ToInt32());}
 [Fact] public void SerializesExactHeaderCoinsAndReserved(){var bytes=FabricAmberScorpion4Configuration.FromScorpion4(new()).ToNativeBytes();Assert.Equal(152,bytes.Length);Assert.Equal(0x34534146u,BitConverter.ToUInt32(bytes));Assert.Equal(152u,BitConverter.ToUInt32(bytes,4));Assert.Equal(1u,BitConverter.ToUInt32(bytes,8));Assert.Equal(6u,BitConverter.ToUInt32(bytes,12));Assert.Equal(2,bytes[61]);Assert.Equal(6,bytes[62]);Assert.Equal(0,bytes[63]);for(var i=0;i<6;i++){Assert.Equal(0xff,bytes[65+i*4]);Assert.Equal(0,bytes[66+i*4]);Assert.Equal(0,bytes[67+i*4]);}}
 [Fact] public void ResourcesAreTypedContiguousAndRejectGaps(){var s=new Scorpion4ProjectSettings();s.ProgramRoms[0].Path="p";s.SoundRoms[0].Path="s";var r=FabricEmulationBackend.BuildRomResources(s);Assert.Collection(r,x=>{Assert.Equal(FabricRomRole.Program,x.Role);Assert.Equal(0u,x.Slot);},x=>{Assert.Equal(FabricRomRole.Sound,x.Role);Assert.Equal(0u,x.Slot);});s.ProgramRoms[2].Path="gap";Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(s));}
 [Fact] public void PoliciesRemainExplicit(){Assert.Equal("bellfruit-scorpion4",FabricEmulationBackend.BellfruitScorpion4MachineIdentifier);Assert.True(AlphaCellOrder.IsAmberBackedPlatform(FruitMachinePlatformType.Scorpion4));Assert.False(PlatformReelDirectionResolver.RequiresReversal(FruitMachinePlatformType.Scorpion4));Assert.Equal(.587,InternalReelOffsetResolver.ResolveNormalizedOffset(FruitMachinePlatformType.Scorpion4,12));Assert.Equal(.911,InternalReelOffsetResolver.ResolveNormalizedOffset(FruitMachinePlatformType.Scorpion4,16));}
 [Fact] public void ViewModelEditsAutosave(){var m=new Scorpion4ProjectSettings();var saves=0;var vm=new Scorpion4ProjectSettingsViewModel(m,x=>{Assert.Same(m,x);saves++;});vm.ProgramRoms[0].Path="p";vm.Reels[0].Steps=88;vm.Dips[0].IsEnabled=true;vm.Stake=2;vm.Prize=3;vm.Percentage=15;vm.EdcEnabled=true;vm.HopperType=2;vm.Coins[0].Value=5;vm.Hoppers[0].Enabled=true;Assert.Equal(10,saves);Assert.Equal("p",m.ProgramRoms[0].Path);Assert.Equal(2,m.Stake);Assert.Equal(3,m.Prize);Assert.Equal(15,m.Percentage);Assert.Equal(0xff,m.Coins[1].Value);}
 [Fact]
 public void SelectorChoicesMatchProjectAmber()
 {
  var vm=new Scorpion4ProjectSettingsViewModel(new(),_=>{});
  Assert.Equal(8,vm.StakeChoices.Count);
  Assert.Equal(new[]{"None","5p","10p","20p","25p","30p","50p","£1"},vm.StakeChoices);
  Assert.Equal(16,vm.PrizeChoices.Count);
  Assert.Equal(new[]{"None","£3","£4","£6 cash","£6 token","£8 cash","£8 token","£10 cash","£5 cash","£15 cash","£25 cash","£25 LBO","£35","£70","£75","reserved"},vm.PrizeChoices);
  Assert.Equal("£10 cash",vm.PrizeChoices[7]);
  Assert.Equal("£5 cash",vm.PrizeChoices[8]);
  Assert.Equal(16,vm.PercentageChoices.Count);
  Assert.Equal(new[]{"None","70%","72%","74%","76%","78%","80%","82%","84%","86%","88%","90%","92%","94%","96%","98%"},vm.PercentageChoices);
  Assert.Equal("80%",vm.PercentageChoices[6]);
  Assert.Equal("90%",vm.PercentageChoices[11]);
 }
 [Theory]
 [InlineData(0,0,0,true)]
 [InlineData(7,15,15,true)]
 [InlineData(8,0,0,false)]
 [InlineData(0,16,0,false)]
 [InlineData(0,0,16,false)]
 [InlineData(0,0,31,false)]
 public void SelectorValidationMatchesProjectAmber(int stake,int prize,int percentage,bool valid)
 {
  var settings=new Scorpion4ProjectSettings{Stake=stake,Prize=prize,Percentage=percentage};
  if(valid) FabricAmberScorpion4Configuration.Validate(settings);
  else Assert.Throws<ArgumentOutOfRangeException>(()=>FabricAmberScorpion4Configuration.Validate(settings));
 }
 [Fact]
 public void SerializesSelectorIndicesUnchanged()
 {
  var settings=new Scorpion4ProjectSettings{Stake=4,Prize=8,Percentage=11};
  var bytes=FabricAmberScorpion4Configuration.FromScorpion4(settings).ToNativeBytes();
  Assert.Equal(4,bytes[56]);
  Assert.Equal(8,bytes[57]);
  Assert.Equal(11,bytes[58]);
 }
 [Fact] public void FactoryRequiresDedicatedProviderPath(){using var files=new TempProviderFiles();var factory=new EmulationBackendFactory(()=>files.Runtime,()=>files.Other,scorpion4AmberPathProvider:()=>null);var error=Assert.Throws<InvalidOperationException>(()=>factory.CreateBackend(FruitMachinePlatformType.Scorpion4));Assert.Contains("Scorpion 4",error.Message);var dedicated=new EmulationBackendFactory(()=>files.Runtime,()=>files.Other,scorpion4AmberPathProvider:()=>files.Scorpion4);Assert.IsType<FabricEmulationBackend>(dedicated.CreateBackend(FruitMachinePlatformType.Scorpion4));}
 private sealed class TempProviderFiles:IDisposable{private readonly string _dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));public TempProviderFiles(){Directory.CreateDirectory(_dir);File.WriteAllText(Runtime=Path.Combine(_dir,"FabricRuntime.dll"),"");File.WriteAllText(Other=Path.Combine(_dir,"Mpu5.dll"),"");File.WriteAllText(Scorpion4=Path.Combine(_dir,"Scorpion4.dll"),"");}public string Runtime{get;}public string Other{get;}public string Scorpion4{get;}public void Dispose()=>Directory.Delete(_dir,true);}
}
