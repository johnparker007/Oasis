using System.Runtime.InteropServices;
using Xunit;
namespace OasisEditor.Tests;
public sealed class M1SupportTests
{
 [Fact] public void SchemaAndDefaults(){Assert.Equal(7,EditorProject.CurrentSchemaVersion);Assert.True(Enum.IsDefined(FruitMachinePlatformType.MaygayM1));var a=new M1ProjectSettings();var b=new M1ProjectSettings();Assert.Equal(4,a.ProgramRoms.Count);Assert.Equal(4,a.SoundRoms.Count);Assert.Equal(6,a.Reels.Count);Assert.Equal(16,a.Dips.Count);Assert.Equal(2,a.Hoppers.Count);a.Dips[0]=true;Assert.False(b.Dips[0]);Assert.All(a.Reels,r=>{Assert.Equal(96,r.Steps);Assert.Equal(4,r.OptoEnd);});}
 [Fact] public void AbiLayout(){Assert.Equal(4,Marshal.SizeOf<FabricAmberM1ReelConfig>());Assert.Equal(44,Marshal.SizeOf<FabricAmberM1HopperConfig>());Assert.Equal(148,Marshal.SizeOf<FabricAmberM1Config>());Assert.Equal(16,Marshal.OffsetOf<FabricAmberM1Config>(nameof(FabricAmberM1Config.Reels)).ToInt32());Assert.Equal(40,Marshal.OffsetOf<FabricAmberM1Config>(nameof(FabricAmberM1Config.Dips)).ToInt32());Assert.Equal(56,Marshal.OffsetOf<FabricAmberM1Config>(nameof(FabricAmberM1Config.PercentageKey)).ToInt32());Assert.Equal(60,Marshal.OffsetOf<FabricAmberM1Config>(nameof(FabricAmberM1Config.Hoppers)).ToInt32());Assert.Equal(16,Marshal.OffsetOf<FabricAmberM1HopperConfig>(nameof(FabricAmberM1HopperConfig.CoinsIn)).ToInt32());Assert.Equal(40,Marshal.OffsetOf<FabricAmberM1HopperConfig>(nameof(FabricAmberM1HopperConfig.CoinsRefilled)).ToInt32());}
 [Fact] public void SerializesHeaderAndReserved(){var s=new M1ProjectSettings{PercentageKey=15,EdcEnabled=true};var bytes=FabricAmberM1Configuration.FromM1(s).ToNativeBytes();Assert.Equal(148,bytes.Length);Assert.Equal(0x314D4146u,BitConverter.ToUInt32(bytes));Assert.Equal(148u,BitConverter.ToUInt32(bytes,4));Assert.Equal(1u,BitConverter.ToUInt32(bytes,8));Assert.Equal(6u,BitConverter.ToUInt32(bytes,12));Assert.Equal(15,bytes[56]);Assert.Equal(1,bytes[57]);Assert.Equal(2,bytes[58]);Assert.Equal(0,bytes[59]);Assert.Equal(new byte[]{0,0,0},bytes[73..76]);Assert.Equal(new byte[]{0,0,0},bytes[117..120]);}
 [Fact] public void ResourcesAreContiguousAndTyped(){var s=new M1ProjectSettings();s.ProgramRoms[0].Path="p0";s.ProgramRoms[1].Path="p1";s.SoundRoms[0].Path="s0";var r=FabricEmulationBackend.BuildRomResources(s);Assert.Collection(r,x=>{Assert.Equal(FabricRomRole.Program,x.Role);Assert.Equal(0u,x.Slot);},x=>Assert.Equal(1u,x.Slot),x=>Assert.Equal(FabricRomRole.Sound,x.Role));s.ProgramRoms[1].Path="";s.ProgramRoms[2].Path="gap";Assert.Throws<InvalidOperationException>(()=>FabricEmulationBackend.BuildRomResources(s));}
 [Fact] public void VisualPolicyIsExplicit(){Assert.True(AlphaCellOrder.IsAmberBackedPlatform(FruitMachinePlatformType.MaygayM1));Assert.False(PlatformReelDirectionResolver.RequiresReversal(FruitMachinePlatformType.MaygayM1));Assert.Equal(0,InternalReelOffsetResolver.ResolveNormalizedOffset(FruitMachinePlatformType.MaygayM1,12));}
 [Fact] public void ViewModelHopperAndMachineOptionEditsAutosave()
 {
     var model = new M1ProjectSettings();
     var saves = 0;
     var viewModel = new M1ProjectSettingsViewModel(model, updated => { Assert.Same(model, updated); saves++; });

     viewModel.Hoppers[1].Coin = 5;
     viewModel.Hoppers[1].Level = 123;
     viewModel.Hoppers[1].HiIndicator = true;
     viewModel.PercentageKey = 15;
     viewModel.EdcEnabled = true;

     Assert.Equal(5, model.Hoppers[1].Coin);
     Assert.Equal(123u, model.Hoppers[1].Level);
     Assert.True(model.Hoppers[1].HiIndicator);
     Assert.Equal(15, model.PercentageKey);
     Assert.True(model.EdcEnabled);
     Assert.Equal(5, saves);
 }
 [Fact] public void ExistingMaygayPlatformEntryHasFriendlyDisplayName()
 {
     Assert.False(Enum.GetNames<FruitMachinePlatformType>().Contains("M1"));
     Assert.False(Enum.GetNames<FruitMachinePlatformType>().Contains("M1AB"));
     Assert.Equal("Maygay M1", new FruitMachinePlatformDisplayNameConverter().Convert(
         FruitMachinePlatformType.MaygayM1, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
 }
}
