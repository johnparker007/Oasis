# AGENT.md

## Project Overview
This is a WPF-based desktop editor for designing slot machine content.

The editor supports:
- 2D panel layout (.panel2d)
- 3D cabinet integration (.cabinet3d)
- machine assembly (.machine)
- export to a runtime format for a Unity-based arcade application

Current product direction:
- Focus first on the 2D design workflow for panels containing artwork, lamp backlighting, and masks.
- Keep cabinet/3D model work planned but deferred. Blender-authored 3D models will come later.
- The editor should feel familiar to users of the Unity Editor where that improves usability, especially around hierarchy, inspector, project/assets browsing, and context menus.

## Architecture
The solution is structured as:

- Editor.Core -> domain logic (NO WPF dependencies)
- Editor.Shell.Wpf -> UI shell only
- Feature modules:
  - PanelEditor
  - CabinetEditor
  - MachineEditor

## Key Principles
- All mutations must go through ICommand (undo/redo support)
- Do NOT put business logic in WPF views or code-behind
- Keep Core fully UI-agnostic
- Prefer small, testable classes
- Prefer simple implementations over over-engineering

## Document Context Rules
- Each open document owns its own command history
- Undo/redo must operate only on the active document
- Commands must be bound to the document they were created for
- Commands must never apply to whichever document happens to be active later
- Selection state is document-specific
- Inspector and hierarchy panels must reflect the active document only
- Opening and closing document tabs are not part of undo/redo unless explicitly requested later

## Project Model
The editor is project-based:
- Users must open/create a project before editing
- Projects contain Assets/, Machines/, Generated/, etc.

## Startup Flow Rules
- The application must start in a Launcher window
- The editor shell must NOT open without an active project
- Project creation/opening UI belongs in the Launcher window only
- Closing a project should return the user to the Launcher window
- The editor shell must assume a valid loaded project at all times

## Theme Rules
- Do not hard-code UI colors in views or code-behind
- Use semantic theme resources for editor UI colors
- New UI must work in System, Light, and Dark modes
- Prefer app-defined semantic brushes over direct Fluent resource usage in feature views

## WPF Maintainability Rules
- Large XAML files should not be preserved solely to give Codex more context
- `MainWindow.xaml` should act primarily as the application shell
- Use UserControls for cohesive UI regions such as asset browser, inspector, output/log, document tabs, and canvas host views
- Use ResourceDictionaries for shared styles, brushes, templates, and repeated UI resources
- Do not create UserControls only to reduce line count; extract only when the section has a clear responsibility
- Preserve existing bindings, commands, DataContext assumptions, and visual appearance during view extraction
- Prefer one view extraction per task

## ViewModel Maintainability Rules
- Avoid allowing `MainWindowViewModel` to become the owner of every editor concern
- Move cohesive child concepts into separate ViewModels when they have distinct state, commands, or responsibilities
- Keep public behavior and binding-facing property names stable during refactors unless a task explicitly allows a rename
- Prefer composition over broad rewrites

## Canvas Behavior Refactor Rules
- Treat `CanvasPanBehavior.cs` as interaction code, not a dumping ground for document, persistence, selection, and command logic
- Split canvas behavior gradually into focused components
- Keep pan/zoom, selection, element creation, layout mapping, and mutation command logic separate where practical
- Do not change canvas behavior while extracting unless a task explicitly requests a behavior change

## Context Menu Rules
- Implement right-click menus as reusable WPF resources/styles where practical, not as one-off code-behind branches.
- Context-menu commands should live on the relevant ViewModel or a focused command service, not in the view code-behind.
- Views may contain minimal event glue only when WPF limitations require it; any such glue must delegate immediately to ViewModel commands.
- Do not duplicate keyboard, toolbar, menu, and context-menu behavior. Route all entry points through the same command methods.
- Commands must expose correct CanExecute state and should disable unavailable menu items rather than failing silently.
- Destructive commands such as Delete should confirm only when the UX requires it; do not add excessive confirmations for ordinary editor object deletion unless requested.
- Add context-menu infrastructure in a way that supports future menus on canvas items, inspector rows, document tabs, and asset/project items.

## Hierarchy Context Menu Rules
- Right-clicking a hierarchy entity should select/focus that entity before opening the menu.
- The initial hierarchy entity context menu must include Cut, Copy, Paste, Rename, Duplicate, and Delete.
- Group/category rows such as Images or Rectangles should not expose entity-only operations unless a task explicitly defines group behavior.
- Rename, Duplicate, Delete, Cut, Copy, and Paste must go through document-scoped commands and preserve undo/redo semantics.
- Copy/cut/paste/duplicate must create new stable object IDs when creating new objects; never reuse the copied object's ID.
- Paste should target the active document and should be disabled when the clipboard format is unavailable or incompatible.
- Keep selection identity object-ID based, not display-name or canvas-position based.

## Asset Browser / Project Window Rules
- The Assets pane should evolve toward a Unity Project-window style browser.
- Model the asset browser as a project asset tree plus selected-directory contents, rather than as a flat list of all files.
- The left side should show the directory tree rooted at the project's Assets directory.
- The right side should show child directories and files in the currently selected directory.
- Use distinct icons or templates for empty folder, closed folder, and open/selected folder.
- Double-clicking a folder in the right pane should navigate into that folder; double-clicking an asset should open/load it through the existing asset-opening flow.
- Right-clicking an asset item should select/focus that item before opening its context menu.
- The initial asset context menu must include Show In Explorer, Open, Delete, and Rename.
- File-system mutations must refresh the tree/content panes, preserve selection where possible, and report errors in the output log.
- Rename/delete must guard against invalid paths, missing files, duplicate target names, and attempts to escape the project Assets root.
- Show In Explorer should use the platform-appropriate Windows shell behavior and should handle missing paths cleanly.
- Do not let file-system UI classes leak into domain model classes.

## Refactor Workflow Rules
- For risky files, propose a short split plan before editing
- Complete one refactor task at a time
- Build after each refactor task
- Fix compile errors, binding errors, and missing resource errors before marking a task complete
- Keep refactor-only changes separate from feature changes where practical

## Current Code Review Priorities
The immediate work is usability and maintainability for the existing WPF Panel2D editor. Continue to keep correctness and document-context safety intact while adding the next editor usability features.

Priority order:
1. Add reusable context-menu command infrastructure without duplicating existing keyboard/menu behavior.
2. Add Hierarchy entity context menu commands: Cut, Copy, Paste, Rename, Duplicate, Delete.
3. Refactor the Assets pane from a flat all-files list into a Unity-style two-pane Project window.
4. Add Assets item context menu commands: Show In Explorer, Open, Delete, Rename.
5. Keep copy/paste/duplicate, rename, and delete document-scoped and undoable where they mutate editor documents.
6. Continue planning layer ordering, locking, visibility, cabinet import, machine assembly, and Unity export only after the above usability track is stable.

## Code Review Rules
- The live editor state should not be raw JSON long term. JSON is a persistence format, not the canonical in-memory model.
- Panel2D document state should move toward a UI-agnostic model containing elements with stable IDs, display names, kinds, coordinates, and dimensions.
- WPF controls, dependency properties, brushes, and canvas visuals must not leak into domain/model classes.
- Canvas visuals should be a projection of the Panel2D model.
- Save/load should preserve the existing `.panel2d` format until a dedicated schema migration task changes it.
- Dirty state must change only for real document mutations and must clear after successful save.
- No-op commands must not be recorded in undo/redo history.
- Commands that cannot execute should fail safely and visibly during development rather than silently corrupting state.
- Keep object identity based on stable object IDs, not display names or canvas positions.

## Panel2D Storage and Migration Rules
- Treat `SchemaVersion = 1` as the current `.panel2d` format.
- Add explicit validation before expanding the format.
- Add migration hooks before introducing schema version 2 or later.
- Unsupported future schema versions should produce clear errors rather than being silently normalised.
- Malformed or partially invalid files should fail gracefully with useful output-log/error messages.

## Canvas and Interaction Rules
- `CanvasPanBehavior.cs` should remain coordination/glue only.
- Tool placement, command dispatch, selection, pan/zoom, visual mapping, and persistence mapping should stay in separate focused classes.
- Non-persisted instructional or placeholder visuals must not be treated as selectable/persisted editor objects.
- Hierarchy should list persisted/model-backed objects only.
- Keep click-to-place, pan, zoom, selection, and hierarchy selection behaviour unchanged during refactor-only tasks.

## Testing Rules
- Build after every task.
- Smoke test the relevant editor flow after every task.
- Add automated tests for model/storage/command behaviour where practical.
- Add tests for hierarchy command availability and document-scoped object mutation where practical.
- Add tests for asset browser tree/path behavior where practical, especially root containment, rename/delete refresh, and selected-directory contents.
- If a new test project is needed, keep it under `Oasis/WindowsNetProjects/OasisEditor` and limit it to the editor project.
- Do not proceed to the next task while compile errors, binding errors, missing resources, or failing tests remain.

## Current Focus
Follow TASKS.md in order.
Always implement the next unchecked task unless instructed otherwise.

For larger refactors:
- complete one small UI/command task at a time
- keep the app runnable after each task where practical
- do not combine Assets pane restructuring with unrelated editor changes
- do not combine context menu infrastructure with unrelated document model changes

## How to Work
- Keep changes minimal and focused
- Do not refactor unrelated systems
- If a task is unclear, ask for clarification
