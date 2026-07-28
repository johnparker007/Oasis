using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Xunit;

namespace OasisEditor.Tests;

public sealed class AmberBridgeLibraryTests
{
    [Fact]
    public void NegotiatesInformationEnumeratesAndCreates()
    {
        using var native = new FakeBridge();
        using var bridge = new AmberBridgeLibrary(native);
        Assert.Equal((1u, "Amber", "0.1.1"), (bridge.BridgeDetails.ApiVersion, bridge.BridgeDetails.Name, bridge.BridgeDetails.BridgeVersion));
        Assert.Equal((1u, (uint)Marshal.SizeOf<AmberApiV1Native>()), (native.RequestedVersion, native.RequestedSize));
        Assert.Contains("Create:jpm-system6", native.Calls);
    }

    [Theory]
    [InlineData((int)AmberResult.UnsupportedVersion, false)]
    [InlineData((int)AmberResult.Ok, true)]
    public void RejectsNegotiationFailureOrWrongReturnedVersion(int resultCode, bool wrongVersion)
    {
        using var native = new FakeBridge { GetApiResult = (AmberResult)resultCode, ReturnedVersion = wrongVersion ? 2u : 1u };
        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));
        Assert.Contains("AmberGetApi", error.Operation);
        Assert.True(native.Disposed);
    }

    [Fact]
    public void RejectsMissingRequiredFunctionPointer()
    {
        using var native = new FakeBridge { OmitRun = true };
        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));
        Assert.Equal((int)AmberResult.ExportMissing, error.ResultCode);
    }

    [Fact]
    public void ReportsMissingRequiredCore()
    {
        using var native = new FakeBridge { CoreId = "another-core" };
        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));
        Assert.Equal((int)AmberResult.NoMoreItems, error.ResultCode);
    }

    [Fact]
    public void CreateFailureContainsLastErrorDetails()
    {
        using var native = new FakeBridge { CreateResult = AmberResult.InstanceLimit, ErrorText = "one instance only" };
        var error = Assert.Throws<AmberBridgeException>(() => new AmberBridgeLibrary(native));
        Assert.Equal("Create", error.Operation);
        Assert.Equal("one instance only", error.BridgeError);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void InitialiseMarshalsRomsAndNullTrailingSlots(int count)
    {
        using var native = new FakeBridge(); var allocator = new CountingAllocator(); using var bridge = new AmberBridgeLibrary(native, allocator);
        var programs = Enumerable.Range(1, count).Select(i => $"p{i}.rom").ToArray();
        bridge.Initialise(programs);
        Assert.Equal(programs, native.ProgramPaths.Take(count));
        Assert.All(native.ProgramPaths.Skip(count), Assert.Null);
        Assert.All(native.SoundPaths, Assert.Null);
        Assert.Equal(allocator.Allocations, allocator.Frees); // core ID and ROM paths are released deterministically
        Assert.Equal(count + 1, allocator.Frees);
    }

    [Fact]
    public void InitialiseFailureDestroysWithoutShutdownAndReleasesPaths()
    {
        using var native = new FakeBridge { InitialiseResult = AmberResult.InitialiseFailed };
        var bridge = new AmberBridgeLibrary(native);
        Assert.Throws<AmberBridgeException>(() => bridge.Initialise(["program.rom"]));

        bridge.Dispose();
        Assert.DoesNotContain("Shutdown", native.Calls);
        Assert.True(native.Calls.IndexOf("Destroy") < native.Calls.IndexOf("Unload"));
    }

    [Fact]
    public void RepeatedRunReturnsReportedCyclesAndResetMaps()
    {
        using var native = new FakeBridge { CyclesRun = -17 }; using var bridge = new AmberBridgeLibrary(native);
        bridge.Initialise(["p.rom"]);
        Assert.Equal(-17, bridge.Run(100)); Assert.Equal(-17, bridge.Run(200)); bridge.Reset();
        Assert.Equal(2, native.Calls.Count(x => x.StartsWith("Run:"))); Assert.Contains("Reset", native.Calls);
    }

    [Fact]
    public void DisposalIsIdempotentAndOrdersShutdownDestroyUnload()
    {
        var native = new FakeBridge(); var bridge = new AmberBridgeLibrary(native); bridge.Initialise(["p.rom"]);
        bridge.Shutdown(); bridge.Dispose(); bridge.Dispose();
        Assert.Equal(1, native.Calls.Count(x => x == "Shutdown"));
        Assert.Equal(new[] { "Shutdown", "Destroy", "Unload" }, native.Calls.Where(x => x is "Shutdown" or "Destroy" or "Unload"));
        Assert.Throws<ObjectDisposedException>(() => bridge.Run(1));
        native.Dispose();
    }

    [Fact]
    public void DisposeBeforeInitialiseSkipsShutdown()
    {
        var native = new FakeBridge(); var bridge = new AmberBridgeLibrary(native); bridge.Dispose();
        Assert.Equal(new[] { "Destroy", "Unload" }, native.Calls.Where(x => x is "Shutdown" or "Destroy" or "Unload")); native.Dispose();
    }

    [Fact]
    public async Task InstanceOperationsAreSerialized()
    {
        using var native = new FakeBridge { DelayRun = true }; using var bridge = new AmberBridgeLibrary(native); bridge.Initialise(["p.rom"]);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => bridge.Run(1))));
        Assert.Equal(1, native.MaximumConcurrentRuns);
    }
}

internal sealed class FakeBridge : IAmberBridgeModule
{
    private readonly List<Delegate> _delegates = [];
    private readonly List<IntPtr> _strings = [];
    private int _activeRuns, _maxRuns;
    internal FakeBridge() { GetApi = Root<AmberGetApiDelegate>(GetApiImpl); }
    internal AmberGetApiDelegate GetApi { get; }
    internal AmberResult GetApiResult { get; set; } = AmberResult.Ok;
    internal uint ReturnedVersion { get; set; } = 1;
    internal bool OmitRun { get; set; }
    internal string CoreId { get; set; } = AmberBridgeLibrary.System6CoreId;
    internal AmberResult CreateResult { get; set; } = AmberResult.Ok;
    internal AmberResult InitialiseResult { get; set; } = AmberResult.Ok;
    internal string ErrorText { get; set; } = "native detail";
    internal int CyclesRun { get; set; } = 123;
    internal bool DelayRun { get; set; }
    internal uint RequestedVersion { get; private set; }
    internal uint RequestedSize { get; private set; }
    internal bool Disposed { get; private set; }
    internal List<string> Calls { get; } = [];
    internal string?[] ProgramPaths { get; private set; } = new string?[4];
    internal string?[] SoundPaths { get; private set; } = new string?[4];
    internal int MaximumConcurrentRuns => _maxRuns;
    public AmberGetApiDelegate BindAmberGetApi() => GetApi;

    private AmberResult GetApiImpl(uint version, uint size, ref AmberApiV1Native api)
    {
        RequestedVersion=version; RequestedSize=size; if (GetApiResult != AmberResult.Ok) return GetApiResult;
        api.StructSize=(uint)Marshal.SizeOf<AmberApiV1Native>(); api.ApiVersion=ReturnedVersion;
        api.GetBridgeInfo=Ptr<GetBridgeInfoDelegate>(Info); api.EnumerateCore=Ptr<EnumerateCoreDelegate>(Enumerate);
        api.Create=Ptr<CreateDelegate>(Create); api.Destroy=Ptr<HandleDelegate>(Destroy); api.Initialise=Ptr<InitialiseDelegate>(Initialise);
        api.Reset=Ptr<HandleDelegate>(Reset); api.Run=OmitRun ? IntPtr.Zero : Ptr<RunDelegate>(Run); api.Shutdown=Ptr<HandleDelegate>(Shutdown); api.GetLastError=Ptr<GetLastErrorDelegate>(LastError); return AmberResult.Ok;
    }
    private AmberResult Info(ref AmberBridgeInfoNative i) { i.StructSize=(uint)Marshal.SizeOf<AmberBridgeInfoNative>(); i.ApiVersion=1; i.Name=String("Amber"); i.BridgeVersion=String("0.1.1"); return AmberResult.Ok; }
    private AmberResult Enumerate(uint index, ref AmberCoreInfoNative i) { if(index>0)return AmberResult.NoMoreItems; i.StructSize=(uint)Marshal.SizeOf<AmberCoreInfoNative>(); i.CoreId=String(CoreId); i.DisplayName=String("System 6"); return AmberResult.Ok; }
    private AmberResult Create(IntPtr id, out IntPtr h) { Calls.Add("Create:"+Marshal.PtrToStringUTF8(id)); h=CreateResult==AmberResult.Ok?(IntPtr)42:IntPtr.Zero; return CreateResult; }
    private AmberResult Destroy(IntPtr h) { Calls.Add("Destroy"); return AmberResult.Ok; }
    private AmberResult Initialise(IntPtr h, ref AmberInitialiseParamsNative p) { var a=new[]{p.Program0,p.Program1,p.Program2,p.Program3}; var b=new[]{p.Sound0,p.Sound1,p.Sound2,p.Sound3}; ProgramPaths=a.Select(Read).ToArray(); SoundPaths=b.Select(Read).ToArray(); Calls.Add("Initialise"); return InitialiseResult; }
    private AmberResult Reset(IntPtr h) { Calls.Add("Reset"); return AmberResult.Ok; }
    private AmberResult Run(IntPtr h,uint cycles,out int ran) { var n=Interlocked.Increment(ref _activeRuns); _maxRuns=Math.Max(_maxRuns,n); if(DelayRun)Thread.Sleep(15); Calls.Add("Run:"+cycles); ran=CyclesRun; Interlocked.Decrement(ref _activeRuns); return AmberResult.Ok; }
    private AmberResult Shutdown(IntPtr h) { Calls.Add("Shutdown"); return AmberResult.Ok; }
    private AmberResult LastError(IntPtr h,IntPtr buffer,uint capacity,out uint required) { var bytes=System.Text.Encoding.UTF8.GetBytes(ErrorText+'\0'); required=(uint)bytes.Length; if(buffer==IntPtr.Zero||capacity<required)return AmberResult.BufferTooSmall; Marshal.Copy(bytes,0,buffer,bytes.Length); return AmberResult.Ok; }
    private T Root<T>(T d) where T:Delegate { _delegates.Add(d); return d; }
    private IntPtr Ptr<T>(T d) where T:Delegate => Marshal.GetFunctionPointerForDelegate(Root(d));
    private IntPtr String(string s) { var p=Marshal.StringToCoTaskMemUTF8(s); _strings.Add(p); return p; }
    private static string? Read(IntPtr p)=>p==IntPtr.Zero?null:Marshal.PtrToStringUTF8(p);
    public void Dispose() { if(Disposed)return; Calls.Add("Unload"); foreach(var p in _strings)Marshal.FreeCoTaskMem(p); _strings.Clear(); Disposed=true; }
}

internal sealed class CountingAllocator : IAmberStringAllocator
{
    internal int Allocations { get; private set; }
    internal int Frees { get; private set; }
    public IntPtr Allocate(string value) { Allocations++; return Marshal.StringToCoTaskMemUTF8(value); }
    public void Free(IntPtr pointer) { Frees++; Marshal.FreeCoTaskMem(pointer); }
}
