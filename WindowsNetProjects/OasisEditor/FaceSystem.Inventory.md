# Face System Inventory (Phase 0)

## Scope

This inventory covers only Phase 0 from `FACE_SYSTEM_ARCHITECTURE_PLAN.md`.

No Face document type, Face document model, Face views, renderer implementations, or save/load handlers were added. The goal is to identify the current seams that a future Face implementation should use, plus the smallest refactors that should happen before implementation.

Areas inspected:

- root planning and guidance Markdown files in `WindowsNetProjects/OasisEditor`;
- current document type/tab/workspace architecture;
- Panel2D live document model and storage DTO conversion;
- runtime state and MAME runtime adapters;
- Panel2D Play View and input routing;
- input definition persistence and visual linking;
- Skia Panel2D rendering path;
- document save/load behavior;
- inspector, hierarchy, selection, and document-scoped command integration.

## Executive summary

Face can integrate cleanly, but it should not be implemented by copying the Panel2D stack wholesale.

The current architecture already has useful reusable primitives:

- document tabs with document-scoped command histories;
- per-document runtime-state stores;
- stable persisted Panel2D `ObjectId` values;
- Panel2D change notifications with update fanout metadata;
- Skia viewport transform, throttled invalidation patterns, and render-element dispatch patterns;
- input routing services that are mostly independent of WPF controls;
- save/load seams that have recently become reusable through automation services.

The primary readiness gaps are naming and ownership seams:

- `PanelRuntimeState` is data-oriented and renderer-neutral in content, but it is named and keyed as Panel2D state, lives per open document tab, and runtime adapters discover objects by scanning Panel2D documents.
- runtime object identity is currently implicit. Lamps/reels/displays are matched by Panel2D `DisplayNumber`, but runtime state is stored by Panel2D `ObjectId`. This is stable for Panel2D elements, but not yet stable enough as a project-level machine-object reference system for Face.
- `DocumentTabViewModel` is strongly Panel2D-shaped. It owns `PanelLayoutJson`, `Panel2DDocumentModel`, Panel2D element caches, Panel2D selection, Panel2D viewport fields, and `PanelRuntimeState` directly.
- `ActiveDocumentContextService`, hierarchy, and inspector are Panel2D-selection-specific. They can be extended, but a Face implementation should first introduce a small document-neutral selection/document content seam.

Recommended readiness path:

1. introduce a lightweight project-level machine object/reference vocabulary before Face elements are persisted;
2. rename/refactor `PanelRuntimeState` toward `MachineRuntimeState` without changing behavior, and introduce `IMachineRuntimeState` only if a concrete boundary needs it;
3. split `DocumentTabViewModel` content into per-document content models or adapters instead of adding Face fields directly to it;
4. reuse Panel2D viewport math and command patterns, but extract the WPF-specific pieces before sharing them with Face;
5. add Face to document type/open/save only in Phase 2 after those seams exist.

## Machine objects vs visual elements

The long-term architecture should explicitly distinguish logical **Machine Objects** from document-specific **Visual Elements**.

Machine Objects are the logical/runtime things that exist once for the machine:

- `MachineLamp`;
- `MachineReel`;
- `MachineAlphaDisplay`;
- `MachineSevenSegmentDisplay`;
- `MachineInput`.

Visual Elements are authoring or rendering representations of those machine objects:

- Panel2D lamp elements;
- Panel2D reel elements;
- Panel2D display elements;
- Face lamp windows;
- Face display windows;
- Face button elements;
- future Unity-rendered representations.

The intended relationship is:

```text
Machine Object
    <- referenced by Panel2D
    <- referenced by Face
    <- referenced by future Unity renderer
```

The intended relationship is not:

```text
Face
    -> permanently references Panel2D elements as its primary runtime identity system
```

Panel2D element references are still useful as a migration/import path and as optional provenance links, especially for Face-generation workflows. They should not become the permanent primary runtime identity system for Face. Face should eventually reference machine objects directly, with `LinkedPanel2DElementId` remaining optional source/authoring context.

## Current document architecture

### Current shape

`EditorDocumentType` currently contains `Generic`, `ProjectOverview`, `Panel2D`, `Cabinet3D`, and `Machine`. `EditorDocument.CreateFromFile` maps `.panel2d`, `.cabinet3d`, and `.machine` extensions to those types and falls back to `Generic` for unknown extensions.

`DocumentWorkspaceViewModel` owns the open tab collection, selected document, shell command service for tab mutations, per-document runtime state store, and the current Panel2D creation service. It creates Panel2D tabs through `IPanel2DDocumentCreationService`, opens existing files through `EditorDocument.CreateFromFile`, and carries optional `panelLayoutJson` into `DocumentTabViewModel`.

`DocumentTabViewModel` is the main per-open-document state holder. It wraps an `EditorDocument`, owns a document-scoped `CommandService`, stores a `DocumentId`, stores Panel2D layout JSON, deserializes it into `Panel2DDocumentModel`, maintains Panel2D element lookup caches, tracks Panel2D pan/zoom, tracks Panel2D hierarchy selection, and exposes a `PanelRuntimeState`.

### Reuse directly

- The document type enum/extension dispatch is the obvious Phase 2 plug-in point for `.face` or `.oasisface`.
- `DocumentWorkspaceViewModel.OpenOrSelectDocument`, tab mutation commands, and document-scoped command history are reusable for Face.
- `EditorDocument` title/type/path/dirty abstractions are reusable as document metadata.
- `IPanel2DDocumentCreationService` shows the pattern for a future `IFaceDocumentCreationService`, but the interface should not be generalized until Face skeleton work begins.

### Minimal refactors recommended before Face implementation

- Add a document-content abstraction rather than extending `DocumentTabViewModel` with more Face-specific fields. Candidate shape: `IDocumentContentState` or typed content adapters for `Panel2DDocumentModel` and future `FaceDocumentModel`.
- Add a document-type registry/helper for extension metadata, default extension, save dialog filter entry, open parser, and serializer. Today those decisions are split across `EditorDocument.CreateFromFile`, `BuildOpenDocumentData`, `BuildDocumentContent`, and `PromptSavePath`.
- Keep document-scoped command histories unchanged. Face edit commands should follow the same command-service pattern instead of sharing undo stacks across tabs.

## Panel2D document model inventory

### Current shape

`Panel2DDocumentModel` is small: title, summary, and ordered `PanelElementModel` list.

`PanelElementModel` has stable `ObjectId`, common transform/visibility/lock fields, kind-specific native fields for assets, display numbers, segment-display attributes, lamp/text colors, reel options, and import-source metadata.

`Panel2DDocumentStorage` handles schema versioning, element kind parsing/serialization, storage-model normalization, duplicate object-id validation, full document serialization, and layout-only serialization for the live tab state.

### Reuse directly

- Panel2D `ObjectId` can be used as a stable `LinkedPanel2DElementId` in a future Face document because persisted Panel2D storage validates uniqueness and generates missing IDs during normalization.
- Common transform fields (`X`, `Y`, `Width`, `Height`, `IsVisible`, `IsLocked`, `Name`) can guide Face element schema design.
- `PanelElementImportSourceModel` is useful evidence for source provenance and may inform Face generation workflows.
- Clone-and-replace mutation style and validation helpers should be reused for Face element mutations.

### Do not reuse directly

- Do not make Face elements a new `PanelElementKind`; Face is a separate document type with physical/presentation concerns.
- Do not store Face runtime links by copying Panel2D `DisplayNumber` semantics directly. Face should reference project-level machine objects or explicit Panel2D element IDs.

### Minimal refactors recommended before Face implementation

- Introduce typed reference fields separate from Panel2D element IDs. A Face element should be able to store both `LinkedPanel2DElementId` and `LinkedMachineObjectId`/typed machine-object reference.
- Keep Panel2D storage untouched for Phase 0. If Phase 1 adds machine references, prefer additive fields and compatibility defaults.

## Runtime state architecture inventory

### Current shape

`PanelRuntimeState` stores lamp intensities, reel positions, temporary reel offsets, segment masks/brightness, VFD dot-matrix dots, and lamp-test state in dictionaries keyed by object ID.

`PanelRuntimeStateStore` stores one `PanelRuntimeState` per open document `Guid`.

MAME adapters apply incoming runtime data by scanning all open documents, building mappings from runtime numbers to Panel2D element object IDs, writing values into each document's `RuntimeState`, and notifying affected Panel2D object IDs.

### Is runtime state renderer-independent enough?

Partially.

The runtime values themselves are renderer-independent enough for Face consumption: lamp intensity, reel position, segment masks/brightness, and dot-matrix dots are not WPF or Skia types. They also do not depend on Panel2D geometry.

However, the ownership and keying are not machine-level yet:

- state is stored per open document tab, not per loaded machine/project;
- dictionary keys are Panel2D element `ObjectId` values, not explicit machine object IDs;
- runtime adapters resolve MAME lamp/reel/display IDs by inspecting Panel2D documents and then write state to Panel2D object IDs;
- update notifications are named `PanelVisualStateChanged` and target Panel2D elements.

This means a future Face view could observe the same values only if Face elements point back to Panel2D object IDs, or if adapters write duplicate values into Face object IDs. Both options violate the Face plan's “do not duplicate runtime objects” rule.

### Minimal refactors recommended before Face implementation

- Prefer a simple rename/refactor path toward `MachineRuntimeState` before introducing a new interface. The current state container is concrete, in-memory, and already renderer-neutral in value types; the main problem is ownership/keying/naming, not multiple runtime-state implementations.
- Add an interface such as `IMachineRuntimeState` only when there is a concrete consumer-driven need, such as testing code that must fake state, supporting multiple state backends, or deliberately hiding mutation APIs from read-only renderers. Today an interface would mostly add ceremony without fixing the identity problem.
- Introduce a machine-object mapping layer from runtime source IDs to stable machine references, then map Panel2D elements and future Face elements to those references. Example references: `lamp:17`, `reel:2`, `display:alpha:0`, `display:sevenSegment:12`, `input:<id>`.
- Keep the current per-document state behavior until a project-level machine runtime store is available, but avoid baking it into Face. Face should consume state through a resolver/service, not directly through `DocumentTabViewModel.RuntimeState`.

### `IMachineRuntimeState` reassessment

An interface is not required as the first Phase 1 cleanup unless Phase 1 introduces a specific boundary that needs it. The immediate problem is that runtime values are named and keyed through Panel2D concepts and are stored per open document. Renaming/refactoring the concrete type toward `MachineRuntimeState`, while preserving behavior, would address the current terminology leak more directly than adding an interface.

A future interface would solve a different problem: separating read-only renderer consumption from mutation-capable runtime adapters, substituting fake runtime state in tests, or allowing a non-MAME backend. Those are valid future needs, but they do not by themselves establish stable machine-object identity. Phase 1 should therefore prioritize reference/key cleanup and a concrete `MachineRuntimeState` direction; add `IMachineRuntimeState` only if a clear call site needs abstraction during that work.

## Machine object IDs / references inventory

### Current stable IDs

- Panel2D elements have persisted string `ObjectId` values and storage validation requires them to be unique within the panel document.
- MFME import generates GUID-like `ObjectId` values for imported elements.
- Input definitions have required string `Id` values.
- Input definitions can link to a visual by `Guid? LinkedVisualElementId`, which is parsed from Panel2D object IDs when possible.

### Current implicit runtime IDs

- Lamps use `DisplayNumber` as the imported/MAME lamp number and are mapped to Panel2D object IDs at runtime.
- Reels use `DisplayNumber` as the imported/MAME reel number and are mapped to Panel2D object IDs at runtime.
- Segment displays use `DisplayNumber` as a base cell/display number and derive cell masks/brightness into Panel2D object IDs.
- Inputs use persisted input IDs plus MAME tag/mask data; pointer routing currently links an input to a Panel2D visual `Guid`.

### Runtime identity/reference table

| Runtime object type | Runtime source identifier | Current storage identifier | Current mapping path | Recommended future machine reference |
| --- | --- | --- | --- | --- |
| Lamps | MAME/MFME lamp number, currently represented on Panel2D elements as `DisplayNumber` | Panel2D element `ObjectId` for runtime-state dictionaries; Panel2D `DisplayNumber` persists the source number | MAME lamp update -> adapter groups Panel2D lamp elements by `DisplayNumber` -> writes intensity by Panel2D `ObjectId` -> notifies Panel2D visual IDs | `MachineLamp` reference, e.g. `lamp:17`, with Panel2D lamp elements and Face lamp windows both pointing to it |
| Reels | MAME/MFME reel number, currently represented on Panel2D reel elements as `DisplayNumber` | Panel2D element `ObjectId` for runtime-state dictionaries; Panel2D `DisplayNumber` persists the source number | MAME reel update -> adapter groups Panel2D reel elements by `DisplayNumber` -> resolves effective reel position using Panel2D reel settings -> writes position by Panel2D `ObjectId` | `MachineReel` reference, e.g. `reel:2`, with visual-specific reel geometry/settings kept on visual elements where appropriate |
| Alpha displays | MAME display/cell base identifier, currently represented on Panel2D alpha elements as `DisplayNumber` | Panel2D alpha element `ObjectId` for masks/brightness arrays; `DisplayNumber` persists the base cell/source index | MAME segment/VFD updates -> segment adapter walks Panel2D alpha elements -> derives cell masks/brightness from `DisplayNumber` and display type -> writes arrays by Panel2D `ObjectId` | `MachineAlphaDisplay` reference, e.g. `display:alpha:0`, with display cell layout/render geometry owned by each visual representation |
| Seven-segment displays | MAME digit/cell identifier, currently represented on Panel2D seven-segment elements as `DisplayNumber` | Panel2D seven-segment element `ObjectId` for segment masks/brightness; `DisplayNumber` persists the source digit/cell | MAME segment updates -> segment adapter walks Panel2D seven-segment elements -> maps source digit/cell masks from `DisplayNumber` -> writes masks by Panel2D `ObjectId` | `MachineSevenSegmentDisplay` reference, e.g. `display:sevenSegment:12`, with renderer-specific geometry separate from identity |
| Inputs | Project input definition `Id`, plus MAME `MamePortTag`/`MameMask`; imported button number/shortcut metadata where available | `InputDefinitionModel.Id` in project `input_definitions`; optional `LinkedVisualElementId` GUID points at one Panel2D visual | Keyboard path: shortcut -> input ID -> MAME command. Pointer path: Panel2D visual GUID -> input definition -> MAME command | `MachineInput` reference, preferably the existing input ID or `input:<id>`, with visual hit areas in Panel2D/Face linking by input ID or document+element reference |

This table is the key Phase 1 safety check: runtime source identifiers already exist, but most visual state is currently keyed by Panel2D element IDs. The future reference layer should preserve current imported/source identifiers while moving runtime identity above any one visual document.

### Are machine object IDs stable enough?

Panel2D object IDs are stable enough for linking back to a specific Panel2D element. They are not sufficient as the primary machine object reference system for Face.

Reason: the same logical runtime object may appear in both Panel2D and Face. If runtime state is keyed by Panel2D element object ID, then Face either has to reference Panel2D elements forever or duplicate runtime-state entries under Face object IDs. The Face plan explicitly wants shared runtime objects, so Face needs a logical reference layer above visual/document elements.

### Minimal refactors recommended before Face implementation

- Add a small machine reference model before persisting Face links. It can be string-backed initially to avoid a large migration.
- Use existing imported runtime numbers as default reference sources where available.
- Keep Panel2D `ObjectId` as `LinkedPanel2DElementId` only, not as the universal machine runtime object ID.
- Consider migrating `InputDefinitionModel.LinkedVisualElementId` from `Guid?` to a string-backed visual reference or adding a parallel string field. Panel2D `ObjectId` is a string, and Face element IDs are likely to be strings too; requiring `Guid` will unnecessarily constrain generated/imported IDs.

## Future ownership note: Machine Definition

A likely long-term project structure is:

```text
Project
    Machine Definition
    Panel2D Documents
    Face Documents
```

In that future architecture, **Machine Definition** becomes the owner of logical machine/runtime objects:

- lamps;
- reels;
- alpha and seven-segment displays;
- inputs.

Panel2D and Face documents then become visual/document representations that reference those owned machine objects. The future Unity renderer should consume the same machine-object identities through Face/export data rather than treating Panel2D as the permanent source of truth.

Do not implement this ownership change now. This is only a direction marker so Phase 1 reference cleanup does not accidentally cement Panel2D documents as the permanent owner of machine identity.

## Play View architecture inventory

### Current shape

`PlayView` is a WPF `UserControl` with a Skia surface. It only renders when the selected document is a Panel2D document. It creates a `PanelViewportTransform`, applies pan/zoom to the Skia canvas, and renders selected Panel2D elements using `Panel2DRenderer` and the selected document's runtime state.

The Play View handles keyboard focus and pointer input through router services in `MainWindowViewModel`, and its renderer path is read-only except for temporary reel-drag offsets.

### Reuse directly

- The read-only Play/Edit split is already established and should be mirrored by Face.
- Throttled Skia invalidation and selected-document subscription are reusable patterns.
- `PlayViewKeyboardInputRouter`, `PlayViewPointerInputRouter`, `PlayViewInputRouter`, and `MameInputCommandService` are reusable concepts for Face Play View.

### Minimal refactors recommended before Face implementation

- Extract a shared play-surface interaction helper for focus, key normalization, active shortcut tracking, pointer press/release, and release-all behavior. Do not copy PlayView code into a Face Play View.
- Decouple pointer routing from Panel2D visual GUIDs. Face buttons should resolve to `InputDefinitionModel.Id` or a machine input reference, not to Panel2D visual IDs.
- Avoid putting Face runtime ownership in the view. Face Play View should receive runtime state from the same machine runtime resolver/service as Panel2D Play View.

## Input mapping architecture inventory

### Current shape

`InputDefinitionModel` stores input identity, type, button number, MFME shortcut data, normalized keyboard shortcut, optional `LinkedVisualElementId`, MAME tag/mask, and notes.

Project save/load serializes `input_definitions` inside the project JSON. `LinkedVisualElementId` is written only when present and read back as a `Guid`.

Keyboard routing is input-ID based after shortcut normalization. Pointer routing is visual-link based (`Guid` -> `InputDefinitionModel`). The low-level command service sends MAME state changes using the selected platform plus the input definition.

### Reuse directly

- Keyboard shortcut routing is largely ready for Face because it is view-focused and input-definition based.
- MAME input command service and input down/up/release-all flow are reusable.
- Project-level input definitions already sit outside Panel2D documents, which matches the Face plan.

### Minimal refactors recommended before Face implementation

- Add a Face-friendly input target resolver: Face button hit -> Face element -> input ID/reference -> `InputDefinitionModel` -> MAME command.
- Do not reuse `LinkedVisualElementId` as the only Face linkage. It is `Guid?`, Panel2D-import-shaped, and only supports one visual link per input.
- Consider adding a small many-to-one link model (`InputId`, `DocumentId`, `ElementId`) or let Face button elements carry `LinkedInputId`.

### `LinkedVisualElementId` reassessment

`InputDefinitionModel.LinkedVisualElementId` is likely to become a Face constraint if it remains the only visual-link mechanism. It assumes a single GUID visual link from an input definition to a visual element, while Face needs at least one additional visual representation and future Unity may need another. It also assumes the visual identifier can be represented as a `Guid`, even though Panel2D `ObjectId` is stored as a string and Face element IDs are expected to be string-backed.

A cleaner future model would avoid making input definitions own a single visual GUID. Better options are:

- let visual elements carry `LinkedInputId`/`MachineInput` references, so many Panel2D/Face/Unity visuals can target the same input; or
- introduce explicit visual references with `DocumentId + ElementId` when the project needs cross-document links.

For Phase 1, document-only guidance is enough: do not extend the GUID-only assumption into Face. If input-link cleanup is included later, prefer string element IDs or document-scoped visual references over continuing to add more meaning to `LinkedVisualElementId`.

## Skia rendering architecture inventory

### Current shape

`IPanel2DRenderer` renders a list of `PanelElementModel` values with a `PanelRuntimeState` and `PanelViewportTransform` onto an `SKCanvas`.

`Panel2DRenderer` dispatches to per-kind `IPanelElementRenderer` implementations, skips hidden elements, and records render diagnostics.

Both Panel2D Edit View and Play View instantiate a `Panel2DRenderer` with the same element renderers.

### Reuse directly

- SkiaSharp surface usage, `SKCanvas` transform setup, render throttling, diagnostics style, and per-kind renderer dispatch are reusable patterns.
- `PanelViewportTransform` document-space conversion can be reused directly for initial 2D Face Edit/Play Views.
- Runtime text/layout and segment renderer implementation details may be reusable if Face initial rendering uses the same display visuals.

### Minimal refactors recommended before Face implementation

- Do not make `IPanel2DRenderer` a generic Face renderer by widening Panel2D types. Instead, add a parallel `IFaceRenderer` in Phase 4 that follows the same structure.
- If shared renderer infrastructure is desired, extract only generic Skia concepts (`ViewportTransform`, throttled invalidation, diagnostics frame helper), not Panel2D element rendering APIs.
- Rename or generalize `PanelViewportTransform` only if Face begins to use it widely. Until then, reusing it is acceptable, but a future neutral name such as `DocumentViewportTransform` would reduce terminology leakage.

## Document save/load behavior inventory

### Current shape

Opening `.panel2d` files uses `Panel2DDocumentStorage.TryReadValidated`, then stores a layout-only serialized element list in the tab's `PanelLayoutJson`. Unknown/non-Panel2D files open as generic preview text.

Saving uses `DocumentSaveService`, which delegates content generation to `DocumentWorkspaceViewModel.BuildDocumentContent`. Panel2D documents serialize through `Panel2DDocumentStorage.Serialize`; non-Panel2D documents save a generic JSON object with title/type/summary/saved timestamp.

Save path defaults and filters are selected in `PromptSavePath` with explicit special cases for `.cabinet3d` and `.machine`, and `.panel2d` as the default fallback.

### Reuse directly

- `IDocumentSaveService` / `DocumentSaveService` is reusable for Face once `BuildDocumentContent` can serialize Face content.
- `BuildOpenDocumentData` is the current open parser seam, but it should not accumulate many document types inline.
- Panel2D storage normalization/validation is a good pattern for a future `FaceDocumentStorage`.

### Minimal refactors recommended before Face implementation

- Introduce a small document serializer registry or switch helper before adding Face open/save logic. This avoids scattering `.face` support across open, save, save-as filter, and type detection.
- Ensure document save preserves runtime state references by serializing Face document content, not view/runtime state.
- Consider adding a `Faces/` or `Assets/Faces/` convention when Phase 2 starts. Phase 0 should not change project scaffolding.

## Inspector / document integration inventory

### Current shape

`ActiveDocumentContextService` stores active document ID and active Panel2D selection per document. The selection type is `PanelSelectionInfo`.

`InspectorViewModel` is selected-document-aware and Panel2D-element-aware. It builds editable rows from the active Panel2D selection, commits changes through document-scoped canvas commands, and uses `PanelElementModelUpdater` clone-and-replace semantics.

Panel changes flow through `PanelChangeEvent`, which includes document ID, object ID, changed-property flags, and update fanout flags for canvas, hierarchy, inspector rows, and persistence.

`Panel2DHierarchyProvider` builds hierarchy groups from Panel2D elements and maps hierarchy entries back to `PanelSelectionInfo`.

### Reuse directly

- Document-scoped command execution and clone-and-replace mutation are the right patterns for Face inspector edits.
- `PanelChangeEvent` shape is a good template for `FaceChangeEvent` or a future document-neutral change event.
- Incremental inspector update strategy should be preserved for Face; do not reintroduce full refreshes for simple edits.

### Minimal refactors recommended before Face implementation

- Add a document-neutral selection contract or typed selection union. Face should not reuse `PanelSelectionInfo` because it encodes Panel terminology and serialized Panel element kind tokens.
- Add a Face hierarchy provider only after Face document model exists. Do not modify `Panel2DHierarchyProvider` to know about Face.
- Extract shared inspector row creation helpers for common transform/name/visibility/lock fields only after the Face model exists; premature extraction would make the current Panel2D inspector harder to reason about.

## Recommended Face document integration strategy

When Phase 2 starts, Face should plug in as a first-class document type rather than as a Panel2D element kind.

Recommended steps after Phase 0:

1. Add or confirm a minimal machine-object reference model.
2. Add a renderer-neutral runtime-state access seam.
3. Add a document serializer/type metadata seam.
4. Add `EditorDocumentType.Face` and extension metadata (`.face` preferred unless product naming changes require `.oasisface`).
5. Add `FaceDocumentModel` and `FaceDocumentStorage` with schema versioning and validation.
6. Add an empty/placeholder Face document tab content state.
7. Add Face hierarchy/inspector/view support only in later phases.

Initial Face persisted references should separate presentation element identity from logical machine/runtime identity:

```text
FaceElement.ObjectId              // stable Face element id
FaceElement.LinkedMachineObjectId // logical/runtime object reference
FaceElement.LinkedPanel2DElementId // optional source/authoring Panel2D element reference
FaceButtonElement.LinkedInputId   // input definition reference, if button-specific
```

## Readiness answers from Phase 0

### Where should Face document type plug in?

Face should plug into `EditorDocumentType`, `EditorDocument.CreateFromFile`, document creation/open/save services, save-as extension/filter metadata, `DocumentWorkspaceViewModel`, and a future document-content adapter. It should not be added as a `PanelElementKind`.

### Is runtime state renderer-independent enough?

The stored values are renderer-independent enough, but the ownership/keying is not. A small `MachineRuntimeState`/reference abstraction should come before Face views consume runtime values.

### Are runtime object IDs stable enough?

Panel2D visual IDs are stable enough for `LinkedPanel2DElementId`; they are not stable enough as machine-object IDs. Runtime object references should be introduced or confirmed before Face element persistence.

### Can Face views reuse Panel2D pan/zoom/selection services?

Face can reuse viewport math and high-level Skia interaction patterns. It should not reuse Panel2D selection types directly. A neutral viewport transform and typed Face selection contract are recommended.

### What minimal refactor is needed before Face implementation?

Minimum recommended pre-implementation refactors:

1. add a string-backed machine object reference model/resolver;
2. rename/refactor runtime state toward a concrete `MachineRuntimeState` first; add an interface only if a concrete boundary needs it;
3. introduce document type/open/save metadata helpers;
4. separate document tab content from Panel2D-only state;
5. add a non-Panel selection abstraction for future Face hierarchy/inspector integration.

## Suggested next steps

1. Phase 1: implement only the runtime/reference cleanup if accepted.
2. Keep current Panel2D behavior unchanged while introducing adapters/wrappers.
3. Add unit tests around reference parsing/resolution and unchanged Panel2D runtime adapter behavior.
4. Only after those tests pass locally, start Phase 2 Face document type skeleton.
