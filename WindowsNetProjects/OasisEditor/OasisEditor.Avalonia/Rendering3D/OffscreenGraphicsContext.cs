using System.Runtime.InteropServices;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Egl;

namespace OasisEditor.Avalonia.Rendering3D;

/// <summary>A real offscreen GPU context. Creates no native window and restores the caller's current context.</summary>
internal sealed class OffscreenGraphicsContext : IDisposable
{
    private nint _module;
    private EglDisplay? _display;
    private EglContext? _eglContext;
    private nint _cglContext;
    private CglSetCurrent? _cglSetCurrent;
    private CglGetCurrent? _cglGetCurrent;
    private CglDestroy? _cglDestroy;
    public GlInterface Gl { get; private set; } = null!;
    public bool IsGles { get; private set; }
    public string Description => Gl.Renderer + " / " + Gl.Version;

    public OffscreenGraphicsContext()
    {
        try
        {
            if (OperatingSystem.IsMacOS()) InitializeCgl();
            else InitializeEgl();
        }
        catch { Dispose(); throw; }
    }
    private void InitializeEgl()
    {
        var windows = OperatingSystem.IsWindows();
        if (windows)
        {
            var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            var candidate = Path.Combine(AppContext.BaseDirectory, "av_libglesv2.dll");
            if (!File.Exists(candidate)) candidate = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-" + architecture, "native", "av_libglesv2.dll");
            _module = NativeLibrary.Load(candidate);
        }
        else _module = NativeLibrary.Load("libEGL.so.1");
        var egl = new EglInterface(name =>
        {
            var symbol = windows && name.StartsWith("egl", StringComparison.Ordinal) ? "EGL_" + name[3..] : name;
            return NativeLibrary.TryGetExport(_module, symbol, out var address) ? address : 0;
        });
        EglDisplayCreationOptions Options(int? platform) => new()
        {
            Egl = egl, PlatformType = platform, PlatformDisplay = 0,
            PlatformDisplayAttrs = windows ? [0x3203, 0x3208, 0x3038] : [0x3038],
            GlVersions = [new(GlProfileType.OpenGLES, 3, 0)]
        };
        if (windows) _display = new EglDisplay(Options(0x3202)); // ANGLE / D3D11, supplied by Avalonia.
        else
        {
            try { _display = new EglDisplay(Options(0x31DD)); } // EGL_PLATFORM_SURFACELESS_MESA
            catch (Exception e) when (e is not OutOfMemoryException) { _display = new EglDisplay(Options(null)); }
        }
        _eglContext = _display.CreateContext(new EglContextOptions());
        Gl = _eglContext.GlInterface;
        IsGles = true;
    }
    private void InitializeCgl()
    {
        _module = NativeLibrary.Load("/System/Library/Frameworks/OpenGL.framework/OpenGL");
        var choose = Load<CglChoose>("CGLChoosePixelFormat");
        var create = Load<CglCreate>("CGLCreateContext");
        var destroyFormat = Load<CglDestroy>("CGLDestroyPixelFormat");
        _cglSetCurrent = Load<CglSetCurrent>("CGLSetCurrentContext");
        _cglGetCurrent = Load<CglGetCurrent>("CGLGetCurrentContext");
        _cglDestroy = Load<CglDestroy>("CGLDestroyContext");
        // Core profile with an offline-capable renderer; framebuffer rendering needs no Cocoa window.
        var error = choose([99, 0x3200, 73, 96, 0], out var format, out _);
        if (error != 0 || format == 0) throw new InvalidOperationException($"CGL pixel format creation failed ({error}).");
        try { error = create(format, 0, out _cglContext); }
        finally { destroyFormat(format); }
        if (error != 0 || _cglContext == 0) throw new InvalidOperationException($"CGL context creation failed ({error}).");
        using (MakeCurrent())
            Gl = new GlInterface(new GlVersion(GlProfileType.OpenGL, 3, 2), name => NativeLibrary.TryGetExport(_module, name, out var address) ? address : 0);
    }
    private T Load<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));
    public IDisposable MakeCurrent()
    {
        if (_eglContext is not null) return _eglContext.MakeCurrent();
        if (_cglContext == 0 || _cglSetCurrent is null || _cglGetCurrent is null) throw new ObjectDisposedException(nameof(OffscreenGraphicsContext));
        var previous = _cglGetCurrent();
        var error = _cglSetCurrent(_cglContext);
        if (error != 0) throw new InvalidOperationException($"Making the CGL context current failed ({error}).");
        return new ContextLease(() => _cglSetCurrent(previous));
    }
    public void Dispose()
    {
        _eglContext?.Dispose(); _eglContext = null;
        _display?.Dispose(); _display = null;
        if (_cglContext != 0) { _cglDestroy?.Invoke(_cglContext); _cglContext = 0; }
        if (_module != 0) { NativeLibrary.Free(_module); _module = 0; }
    }
    private sealed class ContextLease(Action restore) : IDisposable { public void Dispose() => restore(); }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CglChoose(int[] attributes, out nint format, out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CglCreate(nint format, nint share, out nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CglSetCurrent(nint context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint CglGetCurrent();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CglDestroy(nint handle);
}
