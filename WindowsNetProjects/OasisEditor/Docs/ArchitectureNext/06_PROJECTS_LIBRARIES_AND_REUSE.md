# Projects, Libraries and Reuse

## Project definition

Project is an authoring workspace.

It should eventually contain/reference:

- project-local assets;
- one or more Machine assets;
- optional Installation assets;
- generated output;
- workspace/editor metadata.

Project should not be the hidden owner of one machine's ROM/platform composition.

## Common case

The common standalone workflow must remain simple.

A new Project can automatically create/select one Machine.

Users should not need to understand multi-machine concepts unless they use them.

## Reusable library

Cabinets and physical devices are often reused across many real games.

Oasis therefore needs a reusable asset library.

Initial likely library asset types:

- Cabinets;
- Reels.

Later:

- Buttons;
- CoinMechs;
- NoteAcceptors;
- Joysticks;
- Screens;
- Speakers;
- other typed devices.

## Initial implementation preference

Do not begin with a complex package manager, dependency resolver or online registry.

A simple user-local Oasis Library directory with explicit asset references is sufficient for the first implementation if it fits current project path infrastructure.

The architecture should allow later packaging/sharing, but do not implement speculative distribution infrastructure now.

## Example workflow

```text
New Project
  -> creates Machine: Bonanza

Import MFME
  -> Panel2D/provenance/game data

Choose Cabinet
  -> Library/Cabinets/JPM Vogue

Machine assigns Faces
  -> generated/authored Bonanza surfaces

Device resolution
  -> standard-reel profiles resolve to reusable JPM reel assets
```

The user should not re-import/configure Vogue's GLB for every game.

## Library versus project copy

The exact policy—direct external library references versus copied/localized project assets—must be decided in the library phase after inspecting current asset/path assumptions.

Requirements:

- explicit and deterministic references;
- build can calculate complete dependencies;
- broken library references produce clear diagnostics;
- no project-directory scanning to guess dependencies.

## Variants

Do not implement generic inheritance/variants initially.

Prefer:

- reusable base Cabinet;
- Machine-owned appearance/configuration overrides.

Only add reusable variants when real authoring examples demonstrate they are needed.
