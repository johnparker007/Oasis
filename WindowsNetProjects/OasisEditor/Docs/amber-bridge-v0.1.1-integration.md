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

All strings are null-terminated `const char *` values and handles (`AmberInstance_t *`) are opaque. Integer widths
are explicit. Every callback and the sole export use `__cdecl`.

```c
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

Oasis requests API version 1 and passes `sizeof(AmberApiV1)`. Success is **only** `AMBER_OK` (zero).
`GetLastError` uses its `required` output and `AMBER_BUFFER_TOO_SMALL` to support a size query followed by retrieval.

## ROM and lifecycle rules

The successful order is: load DLL, negotiate API, enumerate the required core, create, initialise, run zero or more
times (and reset where required), shutdown, destroy, unload. Only one active instance is supported per process and
all calls on it must be serialized. Reset is valid after successful initialisation and while running. Shutdown occurs
at most once and only after successful initialisation. Destroy precedes unload and is still required after failed
initialisation; failed initialisation must not be followed by shutdown.

There are at most four program and four sound ROM paths. Missing **trailing** slots are null pointers, not empty
strings or dummy files. Sound ROMs are optional, so no sound ROMs means four null pointers. Path storage remains
valid through the complete `Initialise` call.

## Deliberately unavailable in v0.1.1

v0.1.1 exposes no switch inputs, output snapshots, lamps, reels, displays, audio, reel configuration, coin
configuration, percentage configuration, or persistence.
