# Architecture Next Phase 5 implementation

## Result

Phase 5 introduces a user-owned local Oasis Asset Library for the two proven reusable physical asset types: Cabinet and Reel. A Machine may independently mix project and Library Cabinets/Reels. Faces and Panel2D remain project content. No Phase 6 device work is included.

## 1. Existing path/package audit

Before Phase 5, Machine `CabinetAssetPath` and `MachineReelAssignment.ReelAssetPath` were untyped project-relative strings. `ProjectAssetPathService`, Machine choice discovery, Cabinet preview composition, Reel requirement rows, build traversal, and tests assumed every referenced manifest was beneath the current project. Package-name checks additionally required `Assets/Cabinet3D/<name>` or `Assets/Reels/<name>`. Face assignments, source documents, ROMs, generated Face output, asset rename/delete, and the Assets watcher are intentionally still project-rooted.

Cabinet internal resources are different from Machine references: `model.path` and reflection visibility masks are package-relative. Build already resolves the GLB beside `asset.cabinet3d`, validates reflection-mask containment, and copies both into staging. Phase 5 preserves that behavior, so it works for either source root and never resolves Cabinet resources relative to the consuming project. Reel packages contain physical dimensions directly and have no external dependency.

Document opening previously rejected Cabinet/Reel manifests solely because their parent hierarchy was not the project hierarchy. Opening now validates the unchanged manifest and uses its package directory name, permitting canonical Library packages to be edited in place. Save remains attached to the opened path. Project rename/move/delete behavior is not broadened to Library content; there is deliberately no cross-project reference rewrite.

## 2. Library directory model

The explicit layout is `Cabinets/<package>/asset.cabinet3d` and `Reels/<package>/asset.reel` beneath one configured root. The authored Cabinet/Reel formats are unchanged. Discovery only considers these two types, validates each manifest independently, skips malformed entries, and preserves duplicate display names as separate path identities.

## 3. Library-root configuration

`EditorPreferences.AssetLibrary.RootPath` stores the machine-local root. Its default is `Documents/Oasis Library`. Preferences exposes an **Asset Library** category. The value is never written to a project, Machine, or runtime manifest.

## 4. Asset-reference representation

`AssetReference` is the deliberately small typed value `{ scope, path }`, where scope is `Project` or `Library`. Paths use `/`, are relative, and reject absolute paths, empty segments, `.` and `..`. This is not a URI, package version, registry identity, or dependency protocol.

## 5. Resolution

`AssetReferenceResolver` is the single project-vs-Library boundary. It selects the appropriate root, canonicalizes the result, and verifies containment. Consumers do not search the filesystem or persist the resolved absolute path.

## 6. Cabinet packages and dependencies

Library Cabinet discovery reads the same `asset.cabinet3d`. Machine build resolves its manifest through the resolver. Its GLB remains relative to the Cabinet package; reflection masks remain package-relative and containment checked. Build copies the model and masks into runtime staging, so the result has no Library dependency. A package whose internal path points back to its original project is invalid/missing rather than silently reaching into that project.

## 7. Reel behavior

Library Reels use the existing `asset.reel` schema. Required logical `Reel:n` roles still come only from assigned project Faces. Each Machine assignment now selects a typed project or Library reference; physical dimensions are resolved only for selected dependencies.

## 8. Machine schema

Machine schema advances directly from 2 to 3. `cabinetAssetPath` is replaced by `cabinetAsset`, and each Reel assignment's `reelAssetPath` is replaced by `reelAsset`. Both serialize `scope` and `path`. Schema 2 is unsupported; there is no fallback reader, legacy DTO, or dual format. Face assignments remain project-relative because Faces are explicitly project content. Runtime schemas are unchanged because scope is flattened away during build.

## 9. Assets and Machine UI

Machine Cabinet and Reel selectors combine validated project and Library catalogs. Labels carry `[Project]` or `[Library]`, while identity remains the typed reference rather than display name. A missing selected reference is retained and shown as `Missing: <path> [<scope>]`. Catalog refresh reconstructs choices without a Machine command and therefore does not dirty it. Cabinet/Reel manifests can also be opened directly and edit/save their canonical package.

The existing Assets pane remains the project mutation surface. Phase 5 does not expose Library rename/delete there: those operations would imply unsafe cross-project rewriting. Library browsing for composition is provided by the selectors, and direct opening uses the existing document workflow.

## 10. Authoring workflow

The intentionally single workflow is package authoring/editing in place: create the standard Cabinet/Reel package folders under the configured root (or explicitly copy a self-contained existing package there), then open their manifests with the existing editor and save normally. Opening never imports or silently copies them into a project. A Cabinet package must include its relative GLB and any relative reflection masks.

## 11. Build flattening and dependency closure

Build resolves only the Machine-selected Cabinet, its assigned project Faces, and the Reel assignments required by those Faces. Library Cabinet geometry/resources and resolved Reel physical data are copied/flattened through the existing staging pipeline. Runtime manifests contain only generated-relative paths and physical values, never authoring scope or the Library root. Consequently malformed unused Library content cannot fail a build.

## 12. Missing and moved assets

The authored typed reference is retained when resolution fails. Selectors display it as missing. Required missing/invalid Cabinets or Reels fail with Machine/type/reference context and the configured Library root where useful. No filesystem-wide recovery search occurs. Moving a Library package therefore makes existing references missing; restoring the same relative package restores them.

## 13. Portability

Only Library-relative paths are serialized. Pointing Preferences at a second root with equivalent relative packages resolves the same unchanged Machine. Machine serialization tests assert that no configured absolute root appears.

## 14. Deliberate exclusions

No online/package manager, versions, locks, download, global ID registry, profiles/default inference, Library Faces/Panel2D, Buttons, button mounts, CoinMech, NoteAcceptor, generic Device hierarchy, or Player runtime Library support was added. Existing Face button/input behavior is untouched.

## 15. Automated coverage

Focused coverage exercises normalization/rejection, project and Library round-trip, independent roots/portability, missing roots, valid/malformed catalog discovery, and duplicate display names. Existing Machine/build/Face tests were updated to schema 3 typed project references and continue to cover dependency-rooted build, project-local composition, Reel requirements/pruning, Cabinet resources, and runtime flattening.

Per repository instructions the Windows/WPF test suite was not executed in the container.

## 16. Manual verification checklist

1. Configure a local Oasis Library root in Preferences.
2. Create/place `Cabinets/JPM Vogue/asset.cabinet3d`.
3. Verify its relative GLB and reflection resources are inside that package.
4. Create/place `Reels/JPM Standard Reel/asset.reel`.
5. Create/place `Reels/JPM Small Reel/asset.reel`.
6. Start a fresh Oasis project.
7. Create/open its Machine.
8. Select `JPM Vogue [Library]`.
9. Import/generate game-specific Panel2D/Faces normally.
10. Assign project Faces to Cabinet targets.
11. Verify required `Reel:n` rows appear.
12. Select the Library Standard/Small Reels.
13. Save and reopen the Machine.
14. Preview/build in Oasis Player.
15. Verify Cabinet, Faces, reel geometry, lamps, direction, offsets, and gameplay.
16. Confirm generated output has no Library-root/runtime dependency.
17. Close the project.
18. Change the configured root to equivalent Library contents elsewhere.
19. Reopen the project.
20. Verify references resolve without Machine modification.
21. Remove a referenced Library Reel and verify the missing selection/diagnostic remains.
22. Restore it and verify resolution recovers.
23. Add an unrelated malformed Library Reel and verify build still succeeds.
24. Verify project-local Cabinet/Reel selections still work.
25. Verify Face button/input behavior is unchanged.

## 17. Repository-driven deviations

The Assets pane's mutation model is tightly project-rooted (watcher, selection, rename and delete all share one root). Rather than scatter Library exceptions through those destructive operations, Phase 5 keeps it as the project browser; Library assets appear in composition selectors and can be opened directly for in-place editing. Creation is an explicit package-folder/copy workflow rather than a second New-Asset dialog. This keeps canonical editing safe and makes reuse/build real without introducing the larger multi-root workspace/index required for global rename/delete.
