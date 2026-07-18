# Task 01: Oasis Editor Machine Build Export

## Scope

Implement the Oasis Editor side of the Phase 1 vertical slice in:

```text
WindowsNetProjects/OasisEditor
```

Read first:

1. `WindowsNetProjects/OasisEditor/AGENTS.md`
2. `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md`
3. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/PHASE_01_CONTEXT.md`
4. this file

Do not scan unrelated Markdown or archived plans.

## Goal

Add an explicit machine build/export operation that writes a self-contained runtime build directory for Oasis Player.

For Phase 1, the build contains one machine and one cabinet GLB with versioned manifests.

## Existing Architecture to Reuse

Inspect and extend existing patterns rather than creating parallel systems. Relevant areas are expected to include:

- project path and generated-output services
- Cabinet3D asset manifests and model-path handling
- machine/document references
- existing Face runtime export conventions
- Editor command/menu infrastructure
- JSON serialization conventions and tests

Use targeted inspection to locate the exact current types.

## Output Location

Build output is disposable/generated content. It must not be written under authored `Assets/` packages.

Choose a deterministic location under the current project's generated/build area, consistent with existing project path conventions. Avoid a hidden temporary directory and avoid writing into the Unity project.

The operation should expose or return the final build root path so the later Editor launch command can pass it to Oasis Player.

## Required Output

Produce an entry-point machine manifest and cabinet package equivalent to:

```text
<BuildDirectory>/
    machine.runtime.json
    cabinet/
        cabinet.runtime.json
        cabinet.glb
```

Requirements:

- manifests have explicit schema identifiers and versions
- references between build files are relative, not machine-specific absolute paths
- stored separators are portable and deterministic
- the referenced source cabinet GLB is copied into the build
- stale files from an earlier build cannot silently survive and affect the result
- the export either succeeds as a coherent build or reports failure without presenting a partial build as valid

A temporary staging directory followed by replacement of the final build directory is preferred where practical.

## Manifest Data

The exact DTO names and JSON casing should follow existing project conventions.

The machine runtime manifest must contain at least:

- schema/type identifier
- schema version
- stable machine identity where available
- machine display name where available
- relative path to the cabinet runtime manifest

The cabinet runtime manifest must contain at least:

- schema/type identifier
- schema version
- stable Cabinet3D identity where available
- relative path to `cabinet.glb`
- model scale/orientation correction data already represented by the Cabinet3D asset, where applicable

Do not add Face target bindings in Phase 1.

Do not expose Editor-internal absolute source paths in runtime manifests.

## Validation

Fail with clear actionable errors when:

- there is no current project or machine suitable for building
- no Cabinet3D asset is assigned
- the Cabinet3D manifest is invalid
- the referenced GLB does not exist
- the output directory cannot be created or replaced
- serialization or file copying fails

Errors should identify the relevant asset or path without dumping an unhandled exception to the user.

## Editor UI Entry Point

Add a minimal explicit menu or command for building the current machine/project for Oasis Player.

Use existing command/menu patterns. Keep business logic out of WPF code-behind.

Phase 1 does not require launching Oasis Player from the Editor yet unless the existing architecture makes a clean, very small follow-on obvious. The required deliverable is the build operation and discoverable output path.

If launch support is deferred, keep the build service API suitable for a later command that will:

1. build
2. locate the Player executable
3. launch it with `--mode machine-preview --build <path>`

## Tests

Add or update focused tests for:

- deterministic output paths
- manifest serialization
- relative path generation
- cabinet GLB copying
- missing cabinet/model validation
- stale-output replacement or cleanup
- preservation of Cabinet3D scale/orientation values

Do not attempt to build or run tests in Codex. John will run them locally.

## Non-Goals

Do not implement:

- Unity Player code
- Face runtime export changes unless strictly required to avoid coupling
- custom material conversion
- arcade manifests
- multiple machines
- zip packaging
- remote publishing
- emulator content

## Completion Report

At completion, report:

- files changed
- build layout produced
- manifest fields and versioning chosen
- menu/command entry point added
- assumptions made about machine-to-cabinet references
- tests added or changed
- exact local verification steps John should perform
- any issue that should be resolved before starting the Player task