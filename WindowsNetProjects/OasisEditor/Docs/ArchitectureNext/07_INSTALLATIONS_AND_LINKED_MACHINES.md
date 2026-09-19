# Installations and Linked Machines

## Definition

Installation is an optional composition of Machine instances and shared physical assemblies.

It exists above Machine.

## Machine invariant

Each Machine represents one independently running playable unit.

Examples:

- one slave cabinet in Party Time;
- one linked driving cabinet;
- one standalone fruit machine;
- one video cabinet.

## Installation examples

### Linked fruit-machine bank

```text
Party Time Deluxe Installation
  MachineInstance 1 -> PartyTimeSlave.machine
  MachineInstance 2 -> PartyTimeSlave.machine
  MachineInstance 3 -> PartyTimeSlave.machine
  MachineInstance 4 -> PartyTimeSlave.machine
  Shared topper / display assembly
  Link topology
  Physical layout
```

If the slave units are identical, use multiple instances of one Machine asset with small instance-specific settings rather than duplicating full Machine assets.

### Linked driving setup

```text
Driving Installation
  Cabinet 1 -> Daytona.machine
  Cabinet 2 -> Daytona.machine
  Cabinet 3 -> Daytona.machine
  Cabinet 4 -> Daytona.machine
  Network/link topology
  transforms/spacing
```

## Link ownership

Individual Machines should expose link endpoints/capabilities.

Installation owns how instances are connected.

Avoid Machine A containing direct references to Machine B.

Possible conceptual structures:

- point-to-point endpoint links;
- named shared network/bus with member endpoints.

Exact schema should be designed when a concrete linked runtime is implemented.

## Shared physical units

Some linked attractions include a large topper/jackpot/display unit.

If it has its own independent runtime/emulation, model it as a Machine.

If it is purely/shared physical presentation, it may eventually be a shared Assembly owned by Installation.

Do not force all physical structures to be Machines.

A generic Assembly asset should not be introduced until a concrete shared-unit implementation requires it.

## Build

Standalone:

```text
Build Machine
```

Linked:

```text
Build Installation
  -> Machine instances and dependencies
  -> shared assemblies
  -> physical layout
  -> link topology
```

Unity Oasis Player can then place both standalone Machine builds and Installation builds in the same arcade scene.

## UX

Do not expose Installation complexity in the default standalone workflow.

Installation editing appears only when the user creates/imports a linked setup.
