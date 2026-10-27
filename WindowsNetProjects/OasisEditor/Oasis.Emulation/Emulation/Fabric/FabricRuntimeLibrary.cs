using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OasisEditor;

public sealed unsafe class FabricRuntimeLibrary : IFabricRuntimeLibrary
{
    private readonly object _gate=new(); private readonly string _path; private nint _module,_runtime; private FabricNativeExports? _exports; private int _sessions; private bool _disposeRequested;
    public FabricRuntimeLibrary(string exactPath)
    {
        if (string.IsNullOrWhiteSpace(exactPath)||!Path.IsPathFullyQualified(exactPath)) throw new ArgumentException("Fabric runtime DLL path must be absolute.",nameof(exactPath));
        if (!File.Exists(exactPath)) throw new FileNotFoundException("Fabric runtime DLL was not found.",exactPath); _path=exactPath;
        try { _module=NativeLibrary.Load(exactPath); _exports=new(_module,exactPath); var result=_exports.CreateRuntimeFn(FabricAbi.Version,out _runtime); if(result!=FabricResult.Ok) throw Error(result,"FabricCreateRuntime",_runtime,false); }
        catch { Cleanup(); throw; }
    }
    public IFabricMachineSession CreateSession(FabricLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Validate(request); lock(_gate) { ObjectDisposedException.ThrowIf(_disposeRequested,this); }
        var allocations=new List<nint>(); try
        {
            var resources=request.RomResources.Select(r=>new FabricRomResourceNative { Size=(uint)sizeof(FabricRomResourceNative),Version=FabricAbi.Version,Role=(uint)r.Role,Slot=r.Slot,Path=AllocUtf8(r.Path,allocations)}).ToArray();
            var resourceBytes=resources.Length*sizeof(FabricRomResourceNative); var resourcePtr=resourceBytes==0?0:Marshal.AllocHGlobal(resourceBytes); if(resourcePtr!=0){allocations.Add(resourcePtr); fixed(FabricRomResourceNative* p=resources) Buffer.MemoryCopy(p,(void*)resourcePtr,resourceBytes,resourceBytes);}
            var config=request.Configuration?.ToNativeBytes()??[]; var configPtr=config.Length==0?0:Marshal.AllocHGlobal(config.Length); if(configPtr!=0){allocations.Add(configPtr); Marshal.Copy(config,0,configPtr,config.Length);}
            var native=new FabricLaunchRequestNative { Size=(uint)sizeof(FabricLaunchRequestNative),Version=FabricAbi.Version,Resources=resourcePtr,ResourceCount=(uint)resources.Length,Configuration=configPtr,ConfigurationSize=(uint)config.Length };
            byte* backendKind = native.BackendKind;
            byte* machineIdentifier = native.MachineIdentifier;
            byte* backendPath = native.BackendPath;
            WriteFixed(request.BackendKind, backendKind, FabricAbi.IdentifierCapacity);
            WriteFixed(request.MachineIdentifier, machineIdentifier, FabricAbi.IdentifierCapacity);
            WriteFixed(request.BackendPath, backendPath, FabricAbi.PathCapacity);
            var result=_exports!.CreateSessionFn(_runtime,&native,out var session); if(result!=FabricResult.Ok) throw Error(result,"FabricCreateSession",_runtime,false);
            FabricMachineSession? managedSession = null;
            try
            {
                managedSession = FabricMachineSession.Create(session, _exports, SessionDisposed);
                lock (_gate)
                    _sessions++;
                managedSession.ActivateOwnership();
                session = 0; // Ownership has transferred to the successfully registered wrapper.
                return managedSession;
            }
            finally
            {
                if (session != 0)
                    _exports.DestroySessionFn(session);
            }
        } finally { foreach(var p in allocations.AsEnumerable().Reverse()) Marshal.FreeHGlobal(p); }
    }
    private static nint AllocUtf8(string s,List<nint> owned){var bytes=Encoding.UTF8.GetBytes(s+'\0');var p=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,p,bytes.Length);owned.Add(p);return p;}
    internal static void WriteFixed(string value,byte* destination,int capacity){var bytes=Encoding.UTF8.GetBytes(value);if(bytes.Length>=capacity)throw new ArgumentException($"UTF-8 value requires {bytes.Length+1} bytes but capacity is {capacity}.");new Span<byte>(destination,capacity).Clear();bytes.CopyTo(new Span<byte>(destination,capacity));}
    private static void Validate(FabricLaunchRequest r){if(string.IsNullOrWhiteSpace(r.BackendKind)||string.IsNullOrWhiteSpace(r.MachineIdentifier))throw new ArgumentException("Backend kind and machine identifier are required.");ValidateFile(r.BackendPath,"backend");foreach(var x in r.RomResources)ValidateFile(x.Path,"ROM");foreach(var g in r.RomResources.GroupBy(x=>x.Role)){var slots=g.Select(x=>x.Slot).Order().ToArray();if(g.Key is FabricRomRole.Program or FabricRomRole.Sound && slots.Length>4)throw new ArgumentException("Amber supports at most four ROMs per role.");if(!slots.SequenceEqual(Enumerable.Range(0,slots.Length).Select(x=>(uint)x)))throw new ArgumentException($"{g.Key} ROM slots must be unique and contiguous from zero.");}}
    private static void ValidateFile(string p,string kind){if(string.IsNullOrWhiteSpace(p)||!Path.IsPathFullyQualified(p))throw new ArgumentException($"Fabric {kind} path must be absolute: '{p}'.");if(!File.Exists(p))throw new FileNotFoundException($"Fabric {kind} file was not found.",p);}
    internal static FabricException Error(FabricResult r,string op,nint h,bool session,FabricNativeExports? exports=null){string? text=null;var e=exports; if(e is not null&&h!=0){var fn=session?e.SessionError:e.RuntimeError;fn(h,null,0,out var n);if(n is >0 and <=65536){var b=new byte[n];fixed(byte* p=b)if(fn(h,p,n,out _)==FabricResult.Ok)text=Encoding.UTF8.GetString(b.AsSpan(0,Array.IndexOf(b,(byte)0) is var z&&z>=0?z:b.Length));}}return new(r,op,text);}
    private FabricException Error(FabricResult r,string op,nint h,bool session)=>Error(r,op,h,session,_exports);
    private void SessionDisposed(){lock(_gate){_sessions--;if(_disposeRequested&&_sessions==0)Cleanup();}}
    public void Dispose(){lock(_gate){if(_disposeRequested)return;_disposeRequested=true;if(_sessions==0)Cleanup();}}
    private void Cleanup(){try{if(_runtime!=0)_exports?.DestroyRuntimeFn(_runtime);}catch{}finally{_runtime=0;_exports=null;if(_module!=0){NativeLibrary.Free(_module);_module=0;}}}
}
