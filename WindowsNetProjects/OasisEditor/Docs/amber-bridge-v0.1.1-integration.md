# Amber Bridge v0.1.1 integration contract

This is Oasis's durable copy of the integration contract for the separately maintained
[`johnparker007/AmberOasisBridge`](https://github.com/johnparker007/AmberOasisBridge). The immutable baseline is tag
`amber-bridge-v0.1.1`. The headers at that tag remain authoritative if this document and the tagged source differ.
No access to that repository is required to build or test the managed wrapper.

## Deployment and exported surface

`AmberBridge.dll` and `AmberOasis.JPMSystem6.dll` are deployed beside one another. Oasis loads an absolute,
caller-supplied path to `AmberBridge.dll`; the bridge locates and loads the core DLL. Oasis must neither locate nor
load the core DLL. v0.1.1 exports exactly `AmberGetApi`, using `__cdecl`. The System 6 core ID is `jpm-system6`.

## ABI

The semantic API version is **v1**, whose exact encoded ABI value is
`AMBER_API_VERSION_1 = 0x00010000u` (65536 decimal). Oasis passes and validates `0x00010000`; decimal `1` is not a
valid Amber API version value.

The header does not specify an encoding for strings. The managed wrapper explicitly interprets all null-terminated
`const char *` values as UTF-8, with no ANSI fallback. Handles (`AmberInstance_t *`) are opaque. Integer widths
are explicit. Every callback and the sole export use `__cdecl`.

```c
#define AMBER_API_VERSION_1 0x00010000u
#define AMBER_API_VERSION_CURRENT AMBER_API_VERSION_1
typedef struct AmberInstance_t* AmberHandle;
typedef enum AmberResult {
  AMBER_OK=0, AMBER_INVALID_ARGUMENT=1, AMBER_UNSUPPORTED_VERSION=2,
  AMBER_DLL_LOAD_FAILED=3, AMBER_EXPORT_MISSING=4, AMBER_INVALID_STATE=5,
  AMBER_INSTANCE_LIMIT=6, AMBER_INITIALISE_FAILED=7, AMBER_INTERNAL_ERROR=8,
  AMBER_NO_MORE_ITEMS=9, AMBER_BUFFER_TOO_SMALL=10
} AmberResult;
typedef struct AmberBridgeInfo { uint32_t struct_size; uint32_t api_version; const char* name; const char* bridge_version; } AmberBridgeInfo;
typedef struct AmberCoreInfo { uint32_t struct_size; const char* core_id; const char* display_name; } AmberCoreInfo;
typedef struct AmberInitialiseParams { uint32_t struct_size; const char* program_roms[4]; const char* sound_roms[4]; } AmberInitialiseParams;
typedef struct AmberApiV1 {
  uint32_t struct_size; uint32_t api_version;
  AmberResult (__cdecl *GetBridgeInfo)(AmberBridgeInfo*);
  AmberResult (__cdecl *EnumerateCore)(uint32_t, AmberCoreInfo*);
  AmberResult (__cdecl *Create)(const char*, AmberHandle*);
  AmberResult (__cdecl *Destroy)(AmberHandle);
  AmberResult (__cdecl *Initialise)(AmberHandle, const AmberInitialiseParams*);
  AmberResult (__cdecl *Reset)(AmberHandle);
  AmberResult (__cdecl *Run)(AmberHandle, uint32_t, int32_t*);
  AmberResult (__cdecl *Shutdown)(AmberHandle);
  AmberResult (__cdecl *GetLastError)(AmberHandle, char*, uint32_t, uint32_t*);
} AmberApiV1;
AmberResult __cdecl AmberGetApi(uint32_t requested_version, uint32_t api_size, AmberApiV1* api);
```

Oasis requests API v1 by passing `AMBER_API_VERSION_1` (`0x00010000u`) and `sizeof(AmberApiV1)`. Success is **only** `AMBER_OK` (zero).
`GetLastError` uses its `required` output and `AMBER_BUFFER_TOO_SMALL` to support a size query followed by retrieval.

## ROM and lifecycle rules

The successful order is: load DLL, negotiate API, enumerate the required core, create, initialise, run zero or more
times (and reset where required), shutdown, destroy, unload. Only one active instance is supported per process and
all calls on it must be serialized. Reset is valid after successful initialisation and while running. Shutdown occurs
at most once and only after successful initialisation. Destroy precedes unload and is still required after failed
initialisation; failed initialisation must not be followed by shutdown.

**Program ROM count: 1–4. Sound ROM count: 0–4.** Missing **trailing** slots are null pointers, not empty
strings or dummy files. Sound ROMs are optional, so no sound ROMs means four null pointers. Path storage remains
valid through the complete `Initialise` call.

## Deliberately unavailable in v0.1.1

v0.1.1 exposes no switch inputs, output snapshots, lamps, reels, displays, audio, reel configuration, coin
configuration, percentage configuration, or persistence.

## Oasis integration status

`System6NativeBackend` now uses the managed `IAmberBridgeLibrary` lifecycle. The native-library preference passed
to the backend is an absolute path to `AmberBridge.dll`, not to `AmberOasis.JPMSystem6.dll`. The core DLL must be
colocated with the bridge; Amber Bridge selects and loads the `jpm-system6` core itself.

Startup validates the Editor's existing two-required-program-ROM rule and file existence before creating the
bridge. Non-empty program and sound ROM slots are supplied in their configured order; absent sound ROMs are an
empty collection. Successful startup performs initialise followed by the single reset historically performed after
ROM loading. The existing 1 kHz scheduler then calls bridge `Run`, while pause/resume gates that loop. Stop calls
shutdown once and disposal releases the bridge (and therefore its instance and module).

Bridge v0.1.1 cannot service the Editor's switch, output, audio, reel-configuration, coin-configuration, or
percentage-configuration operations. Output and audio polling are disabled, and optional startup configuration is
skipped rather than sent to the former JPM exports. An explicit switch request throws `NotSupportedException` with
an Amber Bridge v0.1.1 diagnostic. These operations are deferred until a future bridge API exposes them.
