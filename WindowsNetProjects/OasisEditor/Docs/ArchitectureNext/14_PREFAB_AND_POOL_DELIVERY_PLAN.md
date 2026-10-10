# Prefab and algorithmic Pool delivery plan — proposed

Status: planning only, 2026-10-10; none of P1–P8 is started or completed. [Architecture and inspected baseline](13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md) defines the contracts. [Active roadmap](10_ACTIVE_ROADMAP.md) owns sequencing.

## Sequencing and common gates

Dependency spine: P1 -> P2 -> P3 -> P4 -> P5. P6 depends on P1–P4 and is independently reviewable after P5. P7 depends on P4; P8 depends on P4/P7 movement contracts. Prefer completing P5 before these proving cases; do not require variants for Pool. P4 is several small dependent PRs, not a language/runtime rewrite in one change.

A1–A7/A8.1–A8.4 are delivered. A8.5 single-ball implementation, trigger discoverability and development authored-key support are delivered, but actual local Pool content wiring and verification remain outstanding. P3 is the unchanged-gameplay gate; if the baseline cannot be verified locally, resolve its wiring/defects before extending gameplay. A8.6 fruit-device scripting remains deferred behind this proposed track; A8.7 becomes P7's expanded mole proving case. This reprioritisation is proposed for human review, not an instruction that work has begun. Installation/video tracks remain deferred.

All implementation phases must read AGENTS.md and current priority first; use the canonical C# 9 engine-free scripting package and current document commands, theme and incremental projection patterns. No builds/tests execute in Codex. Automated coverage below means coverage to add and run locally with Windows/.NET and Unity toolchains. No inherited suite pass is claimed.

Formats are latest-only: each format change updates writer/reader/validators/fixtures together and increments affected versions. No dual loaders, fallback syntax or automatic legacy migration layers. Preserve asset GUIDs and stable placement IDs when deliberately reauthoring current content; old-format packages may stop loading. Concrete sample reauthoring instructions and regenerate/rebuild steps accompany each format PR. Generated output/authored assets/scenes change only in later explicitly scoped implementation tasks, not this documentation task.

## P1 — Evolve Object3D authoring into ordinary Prefabs

**Prerequisite:** approve root/node/component contract and source-frame rules in the architecture. Inspect existing Object3D editor/discovery/storage paths again before implementing.

**Scope:** extend the existing asset family/document pipeline with stable root/child node IDs and explicit Transform, Model/Rendering, Collider and Rigidbody capability entries. Rename user-facing asset terminology to Prefab within that workflow; maintain one resolver/package identity. Shared Hierarchy/viewport/Inspector patterns expose component defaults, validation and undoable hierarchy edits. A single ordinary asset supports grouping and render/physics nodes. Validate one body per subtree and backend limitations.

**Exclusions:** Machine runtime changes, spawning, script syntax changes, variants, per-object behaviour, generic conversion of Cabinet/Face/Reel, kinematic gameplay. Keep existing runtime build output shape for the representable single-root subset; fail clearly for new structures the old runtime contract cannot represent. This permits focused authoring work without silently dropping child components before P2.

**Affected systems/contracts:** Object3DDocument/storage/validation, asset discovery/reference kind and Inspector/document ViewModels, package naming conventions, asset editors and previews. Increment authored asset schema only; keep runtime schema 1 until P2. Decide final manifest filename in this PR, update the one existing family end-to-end and remove obsolete discovery paths rather than introducing a second Prefab catalog. Reauthor representative single-root fixtures retaining GUID/model/defaults; existing Machine references must explicitly be updated if package manifest names change.

**Automated coverage:** round-trip new documents; stable node/component IDs through rename/reorder/undo; cycle/duplicate/unknown component validation; collider/body constraints; reference discovery and command no-op/dirty behaviour; unsupported build-projection errors.

**Local Editor/Player verification:** create/open/save/reopen a ball Prefab; add a transform-only child, edit primitive collider and undo/revert changes; verify diagnostic selection and all themes. Existing single-root Machine builds still load in Player via unchanged runtime output; child-rich build fails with context until P2. Do not pretend the Player understands the new schema yet.

**Acceptance:** exactly one evolved asset pipeline; no persisted viewport state; commands own all edits; supported defaults round-trip; existing representable physics conversion remains unchanged; unsupported structures never silently export. Update documentation from proposed to delivered only after merge and local evidence.

## P2 — Authored OasisObjects and runtime components

**Prerequisite:** P1 merged; single-root export checkpoint available.

**Scope:** evolve Machine placements to authored OasisObjects, including explicit parent/local transform and root-only allowed placement overrides. Player constructs ordinary Prefab trees, physical roots and common authored entity/component registry. Stable IDs and child node identity derive from definition IDs, not Unity names. Editor selection displays source and inherited/default/placement values.

**Exclusions:** dynamic spawning/destruction, new script types/syntax, variants, runtime reparenting, nested bodies/joints. Existing ObjectRef command boundary temporarily resolves authored root IDs against the new registry; this is an implementation bridge removed at P4, not a serialized compatibility loader.

**Affected systems/contracts:** MachineDocument and composition commands/Inspector/viewport; build dependency walker/runtime DTOs; RuntimeBuildLoader, Object3DRuntimeLoader evolution, RuntimeMachine registry and relay identities; physics adapter. Increment Machine authored/runtime and reusable runtime definition versions together, retaining latest-only readers. Export validated nodes/components/placements and backend capability requirements; reject conflicting GUID contents. Reauthor fixtures/example setup retaining ball01 and anchor IDs. Keep legacy script syntax until P4.

**Automated coverage:** GUID deduplication/collision; source-relative geometry and parent transforms; root-only overrides; ID stability; unsupported shapes/modes; common trigger identity resolution across child colliders; complete registration before initialization/started; partial-load cleanup, unload and fresh reload.

**Local Editor/Player verification:** edit source defaults, inspect inherited placed values, undo placement/parent changes, rebuild and load. Compare live physical root/model/collider corrections to the baseline, including X/Z up-axis. Verify semantic Cabinet triggers and authored development keys still come from generated data.

**Acceptance:** authored trees construct without duplicate bodies/registries; root Rigidbody remains authoritative; reference validation and dependency diagnostics agree with Player; normal and failing loads clean correctly. No spawn API introduced incidentally.

## P3 — Current single-ball Pool migration checkpoint

**Prerequisite:** P2 plus locally wired baseline Pool package; P1 representable-default fixture comparison retained.

**Scope:** use the new Prefab/OasisObject authored structures for ball01 with identical behaviour: rackBall01/traySlot01, six coordinate-named pockets, rerack/newGame, active state and both velocity clears. Preserve Machine-owned script and development R/N authoring/build/load path. This phase changes terminology/content shape, not gameplay or script capabilities.

**Exclusions:** fifteen balls, cue ball, functions, spawning, variants, scoring or shot controls.

**Affected systems/contracts:** canonical Pool example/setup guide and reference/integration fixtures; reauthor local Pool assets explicitly using current schema, preserving IDs/model dimensions and existing Cabinet GLB. No new schema beyond P2. Do not replace exported semantic geometry with a duplicated trigger manifest.

**Automated coverage:** run/update existing PoolBehaviorTests, PoolBehaviorReferenceTests and Unity PoolBehaviorIntegrationTests for new placements; retain duplicate pocket callback guard, live root/Rigidbody movement, clear linear/angular velocities, both reset inputs and unload/fault regression coverage. Add assertions for source Prefab identity and inherited collider dimensions.

**Local Editor/Player verification:** use trigger inventory to compare actual GLB IDs to script; author R/N in Input Map, save/build/load, pocket ball once, duplicate callback, rerack/newGame repeatedly, focus loss, development Inspector override and reload restoration. Inspect no stale bindings/state after reload; verify Emulation behaviour remains unaffected. Record actual locally run suites/manual outcome.

**Acceptance:** working single-ball flow is demonstrated on the evolved structures with unchanged gameplay and generated-data-only loading. Do not proceed to P5 on documentation/sample inference alone.

## P4 — Typed refs, spawning, bounded functions/collections and focused queries

**Prerequisite:** P3; approve lifecycle, limits, query spaces and terminology migration.

Deliver as ordered reviewable subphases:

1. **P4a dependency and identity boundary:** Machine named Prefab declarations, spawn-only packaging/preload, session/generation handles, authored root/child resolution. Change ObjectRef/object literals/commands and event/compiler/validator fixtures to proposed OasisObjectRef/oasis syntax atomically. Keep A8.4 startup/fault/unload behaviour.
2. **P4b lifecycle host operations:** value-returning host result contract, instantiate/destroy/is_alive/reset and atomic registry construction/disposal; budgets and stale callback suppression. Prove a single spawned ball before collections.
3. **P4c language additions:** typed empty Lists/Optional, bounded append/count/contains, acyclic typed functions/return, shared instruction/call accounting; reuse existing for/range/arithmetic rather than rewriting them. Runtime range bounds remain compile-time constants.
4. **P4d frame/geometry/movement:** sqrt, Vec3/frame/pose helpers, read pose, collider sphere/shape queries, named surface plane/rectangle and physics-step kinematic target behind the portable adapter. Add explicit control ownership admission and finite/range validation.

**Scope/exclusions:** minimum example capabilities only; no arbitrary engine APIs, general maps/while/recursion, random, per-object scripts, variant inheritance or device migration. Prototype API signatures in pure compiler/host tests before committing parser syntax; illustrative architecture syntax must be revised to final signatures.

**Affected systems/contracts:** canonical Oasis.Scripting AST/parser/type validation/interpreter/host result and tests; Editor Machine dependencies/reference validator/build; Player loader/registry/relays/physics/timer ownership. P4a increments Machine authored/runtime versions for dependency tables and updated script contract; P4d adds surface declarations and increments affected versions again. Lifecycle runtime-only values require no persisted live-handle data. Migrate canonical script/fixtures in the syntax PR, with a clear latest-only authoring recipe. Definition revisions only when capability contracts change.

**Automated coverage:** spawn-only dependencies and missing aliases; failed allocation cleanup; child handle invalidation; stale/double-destroy/wrong-session faults; authored reset versus destroy and spawn-time reset; no reuse/retargeting on reload; construction event suppression; each limit at boundary/overrun; typed result validation, function cycles/signatures/return/finite math; collection type/bounds/value semantics; retained instruction exhaustion; surface frame transforms and unsupported geometry; kinematic target vs teleport; competing control claims rejected.

**Local Editor/Player verification:** create a tiny Machine that spawns one ball on started, queries its diameter, moves it, destroys it and reloads. Package an unplaced Prefab; confirm Player works without Editor files. Deliberately exceed a cap and use a stale handle: one contextual fault, retained earlier effects, no leaks or repeated dispatch. Verify initialization fault cleans load; handler fault retains scene. Exercise kinematic body on a test surface without changing production Unity scenes in this phase unless explicitly scoped.

**Acceptance:** both authored and spawned refs use one model; script owns no engine/file handles; commands are atomic and budgeted; tests cover lifecycle/fault behaviour. P3 single-ball still passes with renamed syntax and no gameplay change. Small spawned-ball fixture passes before P5.

## P5 — Algorithmic fifteen-ball rack and separate cue

**Prerequisite:** all P4 parts, verified P3 checkpoint, measured ball geometry and reviewed rack/playing-surface frames.

**Scope:** canonical script creates exactly fifteen repeated ordinary Prefab instances using five calculated rows plus one cue instance. rackOrigin, collectionTray and cueStart replace enumerated rack/tray anchors for this example. Derive spacing from effective collider diameter with explicit clearance; prove rack lies within declared surface. Collect numbered balls once into calculated tray slots; duplicate callbacks are idempotent. Cue pocketing returns cue to its separate start. Rerack repositions existing entities/clears motion/collection; newGame destroys and recreates them.

**Exclusions:** variants, full Pool rules, score, multiplayer, aiming, shot/cue animation and random numbering. Different ball appearances can use declared ordinary assets; no inheritance prerequisite.

**Affected systems/contracts:** canonical example/setup guide, script fixtures and local authored dependency/frame/surface setup. No language/schema changes expected; gaps return to a narrow P4 follow-up instead of adding Pool-specific host commands. Keep P3 single-ball fixture as a regression checkpoint even if canonical example advances.

**Automated coverage:** exact count 15+1, unique refs, five-row centring and sqrt(3)/2 separation, radius/diameter and transformed rack bounds; 15 collection slots/no overlap; duplicates and cue separation; rerack preserves IDs/clears both velocities; repeated newGame live counts stable and old handles invalid; repeated load/unload releases imports/resources; budget worst-case for reset and trigger handlers.

**Local Editor/Player verification:** rotated/translated rack and tray frames; realistic radius/source scale; settle rack without initial overlaps, pocket several balls, cue recovery, full collection, repeated R/N and reload. Inspect no contact instability caused by calculated initial poses. Record Unity-dependent physics limits; no cross-backend trajectory claim.

**Acceptance:** generated package alone yields 16 live balls, no fifteen authored rack anchors, correct collection/reset behaviour and no growing registry/resource usage across repeated games. No variants or per-ball scripts.

## P6 — Constrained Prefab variants

**Prerequisite:** ordinary Prefabs/runtime stable through P4; separately approve direct-base-only policy. Recommended after P5, but not on its dependency path.

**Scope:** one ordinary Prefab base plus explicit property overrides, provenance/inherited Inspector styling, Revert/undo, base invalidation/propagation and flattening for builds.

**Exclusions:** variant chains, component addition/removal, hierarchy edits, nested composition and multiple inheritance.

**Affected systems/contracts:** evolved Prefab document/reference resolver, Inspector commands, dependency traversal and validation. Increment authored asset schema; runtime may remain unchanged when flattening produces the existing ordinary contract. Update fixtures; latest-only authoring requires deliberate re-save/rebuild, not a second variant Player loader.

**Automated coverage:** override round-trip/revert; unoverridden base propagation; deleted property/component errors; missing base/cycles/wrong asset kind; GUID collisions; build flattening/deduplication; source editing dirty/undo isolation.

**Local Editor/Player verification:** edit ball colour/mass within allowed properties, inspect base/override styling, revert, change base and rebuild. Missing base fails contextually and preserves last successful build.

**Acceptance:** supported override values match Editor and Player, runtime contains no inheritance graph, Pool ordinary assets remain sufficient.

## P7 — Mole proving case (expanded A8.7)

**Prerequisite:** P4 queries/movement/timers and common identity model; P5 completion recommended, P6 unnecessary.

**Scope:** repeated reusable moles, Machine-owned timed up/down motion, typed hit detection/input and exclusive script control. Use kinematic motion when moving colliders must contact other bodies. Timers reuse A7; no per-mole scripts.

**Exclusions:** emulation devices, production input redesign, animation framework, random API and complete scoring/product gameplay.

**Affected systems/contracts/migration:** new focused example and fixtures, existing adapters only; author ordinary Prefabs, anchors and named logical inputs using current formats. No new schema expected; separately review proven contract gaps rather than broadening secretly.

**Automated coverage:** timer replace/stop/order, hit identity from child colliders, stale target suppression, bounded repeated entities, reset/unload timer cleanup and conflicting control denial.

**Local Editor/Player verification:** several moles rise/fall independently, hit active/hidden targets, reset/reload during movement and timers, inspect no teleported-through contacts when kinematic movement is requested.

**Acceptance:** proves reusable motion/hit/timer behaviour across several entities without Pool-specific APIs or per-object script ownership. A8.7 is only marked delivered after evidence.

## P8 — Moving shelf and dynamic-coin proving case

**Prerequisite:** P4 kinematic adapter/control claims, P7 motion evidence; approve measured limits for many coins.

**Scope:** one kinematic shelf driven at physics-step cadence interacting with many dynamically spawned coins; explicit gravity/collider defaults, collection/removal and bounded live population. Use system controller ownership for fixed-step shelf motion or an explicitly supported script fixed-step event in a separately reviewed small contract PR; ordinary timers must not masquerade as physics ticks.

**Exclusions:** full coin-pusher economy/gameplay, shared physics implementation, arbitrary Unity settings, pooling API or batched logical-device migration.

**Affected systems/contracts/migration:** proving example, physics scheduling adapter and capability profile/limits; ordinary Prefab dependencies and current Machine format. Add a fixed-step script event only if system-owned deterministic shelf trajectory cannot prove intended script control; update canonical event signatures, shared budget and startup/unload tests together without changing existing event order.

**Automated coverage:** last target wins per step, dynamic-body rejection, contact-enabled shelf motion, cap admission before allocation, stale coins/cleanup, exclusive controller claims, fixed-step driver dispose/reload and bounded event work.

**Local Editor/Player verification:** realistic shelf stroke/speed, many coins contacting it, repeated spawn/collection and reset; inspect tunnelling, sleeping, contact stability, frame-rate versus fixed-step behaviour and memory. Record backend settings/capability dependencies and measured capacity.

**Acceptance:** shelf moves through physics-aware targets and pushes coins; entity count/resources remain bounded; no competition between script/emulation/system controllers. Performance limits are measured, not inferred from Pool.

## First-phase implementation prompt (do not execute as part of this task)

> Implement P1 only in Oasis. Read WindowsNetProjects/OasisEditor/AGENTS.md, 00_CURRENT_PRIORITY.md, Docs/ArchitectureNext/13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md and P1/common gates in 14_PREFAB_AND_POOL_DELIVERY_PLAN.md. Reinspect latest main's existing Object3D document, discovery, storage, editor/Inspector, command and runtime build projection paths. Evolve that single asset family into ordinary Prefab authoring with stable inline node/component IDs and explicitly supported Transform, Model/Rendering, Collider and Rigidbody defaults. Preserve asset GUID identity; use latest-only schema policy and update validation/fixtures together. Reuse Hierarchy, viewport, selection, document commands and semantic themes. Keep current representable single-root runtime projection intact and reject new child/component structures that cannot be represented until P2. Do not add a parallel Prefab catalog, Machine runtime integration, spawning, new script syntax, variants, object-owned scripts or generic Cabinet/Face/Reel replacement. Add the P1 automated coverage without running builds/tests in Codex. Provide Windows Editor/Unity local verification instructions, document format reauthoring/rebuild consequences, and report changed files and static validation. If inspected dependencies require a scope adjustment, explain it before broadening; do not implement P2 incidentally. Update delivered status only when actually merged/verified, never from this proposal alone.
