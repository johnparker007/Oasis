"""Exercise the real unsaved-change close flows through the headless MCP host.

Build OasisEditor.Mcp first. This writes projects only inside a temporary workspace
and rendered dialog PNGs only to --output.
"""
import argparse
import base64
import json
from pathlib import Path
import tempfile
import time

from verify_mcp import Client


def value(mapping, name, default=None):
    return mapping.get(name, mapping.get(name[:1].upper() + name[1:], default))


def workflow_state(client):
    return client.state("inspect_close_workflow")


def wait_for(client, predicate, description, timeout=30):
    deadline = time.monotonic() + timeout
    latest = None
    while time.monotonic() < deadline:
        latest = workflow_state(client)
        if predicate(latest):
            return latest
        time.sleep(.01)
    raise AssertionError(f"Timed out waiting for {description}: {latest}")


def wait_for_prompt(client):
    state = wait_for(client, lambda current: bool(current.get("pending")) or current.get("dialogName"),
                     "an unsaved-changes or error dialog")
    assert state.get("running"), state
    return state


def wait_for_result(client, allow_error=False):
    state = wait_for(client, lambda current: not current.get("running"), "the close workflow to finish")
    if not allow_error:
        assert state.get("error") is None, state
    assert state.get("result") is not None, state
    return state


def request(client, operation, **arguments):
    client.state("request_close", operation=operation, **arguments)
    return wait_for_prompt(client)


def response(client, choice, allow_error=False):
    client.state("respond_unsaved_changes", choice=choice)
    return wait_for_result(client, allow_error=allow_error)


def button_labels(state):
    return {value(button, "content") for button in state.get("buttons", [])}


def pending_assets(state):
    pending = state.get("pending")
    assert pending, state
    return value(pending, "assets", [])


def capture_dialog(client, output, name, theme):
    result = client.tool("capture_unsaved_changes_dialog", theme=theme)
    data = next(item["data"] for item in result["content"] if item["type"] == "image")
    png = base64.b64decode(data)
    assert png.startswith(b"\x89PNG\r\n\x1a\n")
    path = output / f"{name}-{theme.lower()}.png"
    path.write_bytes(png)
    return path


def open_documents(client):
    return client.state("list_open_documents")


def project_path(client):
    state = client.state("inspect_workspace")
    path = value(state, "projectFilePath")
    assert path, state
    return Path(path)


def tabs_of_kind(client, kind="Panel2D"):
    return [tab for tab in open_documents(client) if value(tab, "kind") == kind]


def create_dirty_panel(client, name, marker):
    client.state("create_panel2d", name=name)
    tab = next(tab for tab in tabs_of_kind(client) if value(tab, "isActive"))
    path = Path(value(tab, "path"))
    element_id = client.state("add_rectangle")["selectedId"]
    client.state("update_element", id=element_id, name=marker)
    return path


def edit_panel(client, path, marker):
    client.state("open_panel2d", path=str(path))
    element_id = client.state("add_rectangle")["selectedId"]
    client.state("update_element", id=element_id, name=marker)


def package_snapshot(manifest):
    directory = manifest.parent
    return {path.relative_to(directory): path.read_bytes()
            for path in directory.rglob("*") if path.is_file()}


def package_contains(manifest, text):
    encoded = text.encode("utf-8")
    return any(encoded in content for content in package_snapshot(manifest).values())


def assert_dirty(client, paths, expected=True):
    actual = {str(Path(value(tab, "path"))): bool(value(tab, "isDirty")) for tab in open_documents(client)}
    for path in paths:
        assert actual.get(str(path)) is expected, actual


def assert_project_closed(client):
    state = client.state("inspect_workspace")
    assert value(state, "projectFilePath") is None, state
    assert not open_documents(client)


def run(client, output):
    tools = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
    assert {"request_close", "inspect_close_workflow", "respond_unsaved_changes",
            "capture_unsaved_changes_dialog", "save_all_documents"} <= tools

    client.state("create_project", name="Unsaved Changes Base")
    original_project = project_path(client)

    # A single dirty document can be canceled without touching its files, then saved
    # through the same real close button and production document save path.
    panel_a = create_dirty_panel(client, "Panel A", "single-close-edit")
    before_cancel = package_snapshot(panel_a)
    state = request(client, "document", kind="Panel2D", path=str(panel_a))
    assets = pending_assets(state)
    assert len(assets) == 1, state
    assert any(value(asset, "kind") == "Panel2D" and value(asset, "title") for asset in assets), assets
    assert {"Save", "Discard Changes", "Cancel"} <= button_labels(state), state
    capture_dialog(client, output, "single-document", "Dark")
    capture_dialog(client, output, "single-document", "Light")
    state = response(client, "cancel")
    assert state["result"] is False, state
    assert package_snapshot(panel_a) == before_cancel
    assert_dirty(client, [panel_a])

    # A destructive answer is invalidated if the same live editor changes after
    # the dialog captured its revision. The new edit remains open and dirty.
    state = request(client, "document", kind="Panel2D", path=str(panel_a))
    changed_id = client.state("add_rectangle")["selectedId"]
    changed = client.state("update_element", id=changed_id, name="edit-during-close")
    assert any(value(element, "name") == "edit-during-close" for element in changed["elements"]), changed
    client.state("respond_unsaved_changes", choice="discard")
    state = wait_for(client, lambda current: current.get("dialogName") == "UnsavedChangesError",
                     "the changed-document confirmation error")
    assert state.get("running") and "changed while confirming" in state.get("error", "").lower(), state
    state = response(client, "ok", allow_error=True)
    assert state["result"] is False, state
    assert package_snapshot(panel_a) == before_cancel
    assert_dirty(client, [panel_a])

    state = request(client, "document", kind="Panel2D", path=str(panel_a))
    assert {"Save", "Discard Changes", "Cancel"} <= button_labels(state), state
    state = response(client, "save")
    assert state["result"] is True, state
    assert not any(Path(value(tab, "path")) == panel_a for tab in open_documents(client))
    assert package_contains(panel_a, "single-close-edit")

    # Save All in the close dialog includes all dirty native asset kinds, not just
    # Panel2D. Keep the separate save_all_documents command check below as well.
    reel = client.state("create_reel", name="Mixed Close Reel")
    reel_path = Path(value(reel, "path"))
    client.state("update_reel", widthMm=83)
    edit_panel(client, panel_a, "mixed-a-save-all")
    panel_b = create_dirty_panel(client, "Panel B", "mixed-b-save-all")
    state = request(client, "project")
    assets = pending_assets(state)
    kinds = [value(asset, "kind") for asset in assets]
    assert len(assets) == 3 and kinds.count("Panel2D") == 2 and kinds.count("Reel") == 1, state
    assert {"Save All", "Discard All Changes", "Cancel"} <= button_labels(state), state
    state = response(client, "save")
    assert state["result"] is True, state
    assert_project_closed(client)
    assert package_contains(panel_a, "mixed-a-save-all") and package_contains(panel_b, "mixed-b-save-all")
    reel_document = json.loads(reel_path.read_text(encoding="utf-8"))
    assert value(reel_document, "widthMm") == 83, reel_document
    client.state("open_project", path=str(original_project))

    # Two dirty documents stay open on Cancel, save as a group, then Discard leaves
    # both saved packages byte-for-byte intact while closing the project.
    edit_panel(client, panel_a, "multi-a-save")
    edit_panel(client, panel_b, "multi-b-save")
    state = request(client, "project")
    assets = pending_assets(state)
    assert len(assets) == 2, state
    assert {"Save All", "Discard All Changes", "Cancel"} <= button_labels(state), state
    capture_dialog(client, output, "multi-document", "Dark")
    state = response(client, "cancel")
    assert state["result"] is False, state
    assert_dirty(client, [panel_a, panel_b])
    client.state("save_all_documents")
    assert_dirty(client, [panel_a, panel_b], expected=False)
    assert package_contains(panel_a, "multi-a-save") and package_contains(panel_b, "multi-b-save")

    edit_panel(client, panel_a, "project-discard-a")
    edit_panel(client, panel_b, "project-discard-b")
    saved_a, saved_b = package_snapshot(panel_a), package_snapshot(panel_b)
    state = request(client, "project")
    assert len(pending_assets(state)) == 2, state
    state = response(client, "discard")
    assert state["result"] is True, state
    assert package_snapshot(panel_a) == saved_a and package_snapshot(panel_b) == saved_b
    assert_project_closed(client)

    # Project replacement workflows use an existing target inside the temporary
    # workspace and preserve the current target when canceled.
    client.state("open_project", path=str(original_project))
    client.state("create_project", name="Unsaved Changes Second")
    second_project = project_path(client)
    second_panel = create_dirty_panel(client, "Second Panel", "switch-open-save")
    second_before = package_snapshot(second_panel)
    state = request(client, "open_project", path=str(original_project))
    assert len(pending_assets(state)) == 1, state
    assert {"Save", "Discard Changes", "Cancel"} <= button_labels(state), state
    state = response(client, "cancel")
    assert state["result"] is False and project_path(client) == second_project, state
    assert package_snapshot(second_panel) == second_before
    state = request(client, "open_project", path=str(original_project))
    state = response(client, "save")
    assert state["result"] is True and project_path(client) == original_project, state
    assert package_contains(second_panel, "switch-open-save")

    create_panel = create_dirty_panel(client, "Create Switch Panel", "switch-create-save")
    create_before = package_snapshot(create_panel)
    state = request(client, "create_project", name="Unsaved Changes Third")
    assert len(pending_assets(state)) == 1, state
    state = response(client, "cancel")
    assert state["result"] is False and project_path(client) == original_project, state
    assert package_snapshot(create_panel) == create_before
    state = request(client, "create_project", name="Unsaved Changes Third")
    state = response(client, "save")
    assert state["result"] is True, state
    third_project = project_path(client)
    assert third_project != original_project and package_contains(create_panel, "switch-create-save")

    # Externally changing valid JSON produces a real save conflict and leaves the
    # modified document open after the error is acknowledged.
    conflict_panel = create_dirty_panel(client, "Conflict Panel", "conflict-edit")
    manifest = json.loads(conflict_panel.read_text(encoding="utf-8"))
    assert isinstance(manifest, dict), manifest
    with conflict_panel.open("a", encoding="utf-8") as stream:
        stream.write(" \n")
    state = request(client, "document", kind="Panel2D", path=str(conflict_panel))
    assert len(pending_assets(state)) == 1, state
    client.state("respond_unsaved_changes", choice="save")
    state = wait_for(client, lambda current: current.get("dialogName") == "UnsavedChangesError",
                     "the save conflict error dialog")
    assert state.get("running") and state.get("error"), state
    assert "OK" in button_labels(state), state
    capture_dialog(client, output, "save-conflict", "Dark")
    state = response(client, "ok", allow_error=True)
    assert state["result"] is False, state
    assert_dirty(client, [conflict_panel])

    # Exit confirmation is the final interaction because accepting it closes the
    # isolated authoring window.
    state = request(client, "editor")
    assert len(pending_assets(state)) == 1, state
    state = response(client, "cancel")
    assert state["result"] is False, state
    assert_dirty(client, [conflict_panel])
    state = request(client, "editor")
    state = response(client, "discard")
    assert state["result"] is True, state

    print("Unsaved-change document, project, project-switch, conflict and editor-exit flows passed.")
    print(f"Rendered dialog captures saved to {output}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dll", "--host", dest="dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    dll = args.dll.resolve()
    with tempfile.TemporaryDirectory(prefix="oasis-unsaved-changes-") as temporary:
        root = Path(temporary)
        client = Client(dll, root)
        try:
            run(client, args.output.resolve())
        finally:
            client.close()


if __name__ == "__main__":
    main()
