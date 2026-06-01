# Protocol And Lua Bridge

## Goal

Create a structured debugger protocol on top of the existing Oasis stdin/stdout bridge.

## Protocol prefixes

Responses:

```text
@OASIS_DEBUG
```

Events:

```text
@OASIS_DEBUG_EVENT
```

## Request shape

```json
{
  "id": 1,
  "op": "bp.set",
  "cpu": ":maincpu"
}
```

## Response shape

```json
{
  "id": 1,
  "ok": true,
  "result": {}
}
```

## Event shape

```json
{
  "event": "stopped",
  "cpu": ":maincpu",
  "pc": 4660
}
```

## Lua modules

Create:

```text
oasis/system/commands/debug.lua
oasis/system/debugger/debugger_router.lua
oasis/system/debugger/debugger_protocol.lua
oasis/system/debugger/debugger_state.lua
```

## Initial operations

```text
ping
status
cpus
run
break
step
```

## Design rules

- Never return free-form debugger text when structured data is possible.
- Keep request ids round-tripped.
- Keep protocol transport independent from debugger implementation.
- Treat stdout as a protocol stream rather than console text.
