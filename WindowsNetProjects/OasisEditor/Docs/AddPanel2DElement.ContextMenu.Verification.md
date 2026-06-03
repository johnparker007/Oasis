# Add Panel2D Element Context Menu - Verification

## Objective

Verify that users can create real Panel2D elements directly from the editor via a right-click context menu and that the operation integrates correctly with undo/redo, selection, rendering, hierarchy updates, and persistence.

## Build information

### Branch

`work`

### Commit

`b816f3a` (implementation commit; later commits may update this verification document)

### Build command(s)

```text
dotnet test OasisEditor.sln
```

### Result

- [ ] Build succeeded
- [x] Build could not run in this container because `dotnet` is not installed (`/bin/bash: line 1: dotnet: command not found`).
- [x] Build warnings reviewed: no compiler warnings were available because the SDK is unavailable; `git diff --check` passed with no whitespace errors.

## Automated tests

### New tests

List new tests added for this feature.

```text
OasisEditor.Tests/AddPanel2DElementCommandTests.cs
- CreateAddableElement_ReturnsValidVisibleElementAtRequestedPosition
- CreateAddableElement_UsesVisibleDefaultsForEachRealElementType
- AddPanelElementCommand_AddsSelectsUndoesAndRedoesSameElement
```

### Existing tests run

```text
dotnet test OasisEditor.sln
git diff --check
```

### Result

- [ ] All relevant tests passed
- [x] Automated tests could not run in this container because `dotnet` is not installed.
- [x] `git diff --check` passed.

## Manual verification checklist

### Context menu behaviour

- [ ] Right-clicking a Panel2D edit surface opens a context menu.
- [ ] The menu contains:
  - [ ] Add Lamp
  - [ ] Add Reel
  - [ ] Add 7 Segment Display
  - [ ] Add Segment Alpha
- [ ] Menu appears at the expected pointer location.

### Element creation

#### Lamp

- [ ] Lamp can be added.
- [ ] Lamp appears at clicked Panel2D coordinates.
- [ ] Lamp is visible.

#### Reel

- [ ] Reel can be added.
- [ ] Reel appears at clicked Panel2D coordinates.
- [ ] Reel is visible.

#### 7 Segment Display

- [ ] 7 Segment Display can be added.
- [ ] Display appears at clicked Panel2D coordinates.
- [ ] Display is visible.

#### Segment Alpha

- [ ] Segment Alpha can be added.
- [ ] Display appears at clicked Panel2D coordinates.
- [ ] Display is visible.

### Selection and editor integration

- [ ] Newly created element becomes selected if that matches editor conventions.
- [ ] Inspector updates correctly.
- [ ] Hierarchy updates correctly.
- [ ] Render surface refreshes automatically.
- [ ] Document dirty state updates correctly.

### Undo / redo

For at least one instance of each supported type:

- [ ] Add element.
- [ ] Undo removes element.
- [ ] Redo restores same element.
- [ ] Position is preserved.
- [ ] Identity/properties are preserved.

### Persistence

- [ ] Save document.
- [ ] Reload document.
- [ ] Newly added elements are still present.
- [ ] Element properties survive round-trip serialization.

### Regression checks

- [ ] Existing MFME import still works.
- [ ] Existing Panel2D editing behaviour still works.
- [ ] Existing selection behaviour still works.
- [ ] Existing undo/redo behaviour still works.

## Files touched

Record final implementation files.

```text
Docs/AddPanel2DElement.ContextMenu.Investigation.md
Docs/AddPanel2DElement.ContextMenu.Verification.md
OasisEditor/CanvasMutationCommands.cs
OasisEditor/PanelElementFactory.cs
OasisEditor/Views/SkiaPanel2DEditView.xaml.cs
OasisEditor.Tests/AddPanel2DElementCommandTests.cs
```

## Notes

Capture any compromises, known limitations, or follow-up work.

- Manual UI verification was not performed in this non-interactive container.
- Automated build/test verification is blocked by the missing .NET SDK in this container.
- The UI layer only maps the captured right-click point to a document point and delegates element creation and undoable mutation to shared factory/command paths.

### Follow-up candidates

- Replace hard-coded menu population with a registry-driven source.
- Add toolbar/palette creation workflow.
- Add keyboard shortcuts.
- Add placement preview/ghosting.
- Add additional Oasis element types.
