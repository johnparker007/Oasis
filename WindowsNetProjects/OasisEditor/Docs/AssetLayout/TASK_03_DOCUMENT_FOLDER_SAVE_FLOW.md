# Task 03 - Folder-Based First Save Flow

## Goal

Make first-save/new-document creation use named folders under `Assets` for primary document types.

## New document layout

Use this general layout for new documents:

```text
Assets/
  Panel2D/
    <Name>/
      <Name>.panel2d

  Faces/
    <Name>/
      <Name>.face
      artwork.png
      mask.png

  Cabinet3D/
    <Name>/
      <Name>.cabinet3d
```

This task is primarily about the save/create flow. Face-specific generated artwork and mask handling is covered by Task 02.

## Name prompt

Introduce a simple name prompt where needed:

- creating a new Panel2D document
- creating a Face from a Face Source Shape
- creating a new Cabinet3D document

The prompt should validate that the name is non-empty after trimming and can be converted into a safe path segment.

If a folder with the same name already exists, either reject the name and ask again or generate a predictable unique suffix. Prefer asking the user to choose a unique name where the UI already supports validation.

## Save behavior

Once a document has a folder-based location, normal save should save back to that path.

Save As may continue to exist, but first-save for these project document types should default into the correct `Assets/<Type>/<Name>/` folder rather than a generic file dialog location.

## Cabinet3D note

Cabinet3D references a GLB and must not modify the GLB.

This task does not require deciding whether a GLB is copied into the Cabinet3D folder or referenced externally. Keep the current GLB reference semantics unless the existing code makes that awkward. The important part is that the `.cabinet3d` document itself lives in a named authored project folder.

## Acceptance criteria

- First save/new creation creates named folders under `Assets` for Panel2D, Face, and Cabinet3D documents.
- The document file name matches the safe folder name.
- Existing document save continues to work after the document has a path.
- The UI no longer encourages saving these documents as loose files in arbitrary project locations.
- Unit tests cover the path decisions and collision behavior where practical.
