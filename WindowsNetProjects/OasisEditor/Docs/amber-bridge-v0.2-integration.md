# Amber Bridge v0.2 integration

Oasis requires Amber Bridge API v2. Production negotiation makes one
`AmberGetApi` call with encoded version `0x00020000` and an x64 table size of
144 bytes. The returned table must report that exact version and size, and all
v1-prefix and v2-appended function pointers must be present. There is no API v1
retry or non-interactive compatibility mode.

`AmberBridgeInfo.api_version` is bridge product metadata and is not used as the
negotiated function-table version. The authoritative negotiated version is
`AmberApiV2.api_version`; this permits the bridge to retain the documented
backwards-compatible metadata value.

During current development, supporting API v1 would provide no useful
interactive System 6 behaviour and would increase complexity. Oasis therefore
requires API v2 and fails clearly when it is unavailable.

An unsupported bridge reports an actionable error containing the requested
version (`0x00020000`) and required table size (144). The existing deployment
continues to colocate `AmberBridge.dll` and `AmberOasis.JPMSystem6.dll`; Oasis
loads only the bridge DLL and never directly binds the JPM adapter.

## Current implementation status

The managed function-table prefix, exact negotiation, metadata compatibility
policy, and result-code extension are implemented. Semantic wrappers and the
interactive System 6 routing for capabilities, switches, snapshots,
configuration, and audio remain deferred until the production v0.2 native
header is available in this repository, so their aggregate layouts and field
conversion rules can be reproduced rather than guessed.
