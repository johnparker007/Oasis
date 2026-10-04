# Projects, Libraries and Reuse

## Project

Project is an authoring workspace.

Machine-specific runtime/composition does not belong in Project settings.

A Project may contain multiple Machine assets.

## AssetReference

Reusable physical assets use explicit typed `AssetReference` values with current Project/Library scopes.

Resolution is centralized and build output is self-contained; Oasis Player must not depend on an authoring Library path.

## Current reusable library

Current proven reusable asset types include:

- Cabinet;
- Reel.

The active dynamic-object track should extend the same Project/Library model to:

- Object3D.

Do not invent a separate package/reference system for Object3D.

## Build rule

A Machine build follows explicit asset references and copies/flattens required runtime resources into generated output.

Broken referenced dependencies should fail clearly.

Unreferenced assets should not affect an unrelated Machine build.

## Distribution

Do not add an online registry/package manager yet.

The local Oasis Library remains sufficient until a concrete sharing/distribution workflow requires more.
