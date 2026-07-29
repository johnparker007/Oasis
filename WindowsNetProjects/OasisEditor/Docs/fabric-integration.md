# Fabric integration

Oasis can optionally route Impact/JPM System 6 emulation through the managed Fabric boundary. The route loads the **exact configured Fabric runtime DLL path**, separately passes the exact Amber API v2 DLL path to backend kind `amber-api-v2`, and requests machine `jpm-system6`. Fabric ABI `0x00010000` exports are dynamically resolved as Cdecl delegates.

Program and sound ROM paths are sent only as typed resources, in their existing order and in independent zero-based slots. The Amber-only configuration blob preserves reel indexes/apply mask/opto data, enabled coin channels and routes, and the raw percentage-switch value. No legacy ROM pointer list is populated.

Ownership is module → runtime → sessions. Disposal shuts down and destroys sessions before destroying the runtime and unloading the module. Snapshot arrays use caller-owned capacity negotiation and managed limits (4096 lamps, 64 reels, 64 character displays, 256 segment displays); partial attempts are never published.

The pump measures `Stopwatch` timestamp deltas and passes elapsed duration rather than fixed cycles. Pause/resume and reset establish a fresh baseline. Audio capacity is expressed in stereo frames, while only `framesWritten × channelCount` samples are submitted.

`UseFabricForAmber` defaults to false. Therefore the existing direct Amber backend remains the default; when the option is enabled both Fabric and Amber DLL paths are required. Epoch and all other platforms retain their existing routing.
