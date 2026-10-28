"""Exercise repeated live tab float/maximise/restore/redock cycles through MCP."""
import argparse
import base64
from pathlib import Path
import tempfile

from verify_mcp import Client


LABELS = {"Maximise", "Add Tab", "Close Tab", "Float"}
THEMES = ("Dark", "Light")


def assert_menu(client, pane_id, maximised=False, floating=False):
    state = client.state("inspect_tab_menu", paneId=pane_id)
    entries = state["entries"]
    items = state["items"]
    by_label = {item["header"]: item for item in items}
    assert set(by_label) == LABELS, (pane_id, by_label)
    assert sum(entry["type"] == "Separator" for entry in entries) == 1, (pane_id, entries)
    assert by_label["Maximise"]["checkedState"] is maximised, (pane_id, by_label)
    if maximised:
        for label in ("Close Tab", "Add Tab", "Float"):
            assert by_label[label]["enabled"] is False, (pane_id, label, by_label)
    if floating and not maximised:
        assert by_label["Float"]["enabled"] is False, (pane_id, by_label)
    add_children = by_label["Add Tab"]["children"]
    assert {child["header"] for child in add_children} == {"Hierarchy", "Inspector", "Project", "Output"}, (pane_id, add_children)
    return state


def assert_no_floating_windows(client):
    state = client.state("inspect_layout")
    assert not state["layout"].get("windows"), state["layout"].get("windows")
    assert not state.get("floatingDocuments"), state.get("floatingDocuments")


def capture_menu(client, output, name, pane_id, theme):
    state = client.state("inspect_layout")
    expected_images = 1 + len(state["layout"].get("windows") or [])
    result = client.tool("capture_tab_menu", paneId=pane_id, theme=theme)
    images = [item for item in result["content"] if item["type"] == "image"]
    assert len(images) == expected_images, (name, theme, len(images), expected_images)
    for index, item in enumerate(images):
        png = base64.b64decode(item["data"])
        assert png.startswith(b"\x89PNG\r\n\x1a\n"), (name, theme, index)
        suffix = f"-{index}" if len(images) > 1 else ""
        (output / f"{name}-{theme.lower()}{suffix}.png").write_bytes(png)


def document_state(client, path, element_id, floating):
    if floating:
        state = client.state("inspect_floating_document", kind="Panel2D", path=path)
        elements = state["elements"]
        dirty = state["isDirty"]
    else:
        client.state("activate_document", kind="Panel2D", path=path)
        state = client.state("inspect_workspace")
        elements = state["elements"]
        dirty = state["isDirty"]
    element = next(element for element in elements if element["id"] == element_id)
    assert dirty and element["name"] == "Cycle retained edit", (floating, state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="OasisTabCycles-") as temporary:
        client = Client(args.dll.resolve(), Path(temporary))
        try:
            required = {"create_project", "create_panel2d", "add_rectangle", "update_element",
                        "inspect_layout", "inspect_tab_menu", "capture_tab_menu", "float_document",
                        "dock_document", "move_workspace_pane", "toggle_tab_maximised",
                        "restore_maximised_tab", "inspect_workspace", "inspect_floating_document",
                        "history"}
            tools = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert required <= tools, required - tools

            project = client.state("create_project", name="Repeated tab cycles")
            panel_path = client.state("create_panel2d", name="Dirty cycle document")["panelPath"]
            element_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=element_id, name="Cycle retained edit", x=37)
            layout = client.state("inspect_layout")
            document_id = next(document["id"] for document in layout["assetLayout"]["documents"]
                               if document["kind"] == "Panel2D" and document["path"].endswith("asset.panel2d"))
            pane_ids = ("Hierarchy", "Inspector", "Project", "Output")

            # Each target follows the same float -> maximise -> restore -> redock lifecycle.
            targets = [("document", document_id)] + [("pane", pane_id) for pane_id in pane_ids]
            for kind, pane_id in targets:
                label = "panel" if kind == "document" else pane_id.lower()
                for cycle in range(3):
                    assert_menu(client, pane_id)
                    if kind == "document":
                        client.state("float_document", kind="Panel2D", path=panel_path)
                    else:
                        client.state("move_workspace_pane", paneId=pane_id, placement="float")
                    floating_layout = client.state("inspect_layout")
                    assert len(floating_layout["layout"].get("windows") or []) == 1, floating_layout["layout"]
                    assert_menu(client, pane_id, floating=True)
                    if cycle == 0:
                        for theme in THEMES:
                            capture_menu(client, output, f"{label}-normal", pane_id, theme)

                    client.state("toggle_tab_maximised", paneId=pane_id)
                    assert_menu(client, pane_id, maximised=True, floating=True)
                    if cycle == 0:
                        for theme in THEMES:
                            capture_menu(client, output, f"{label}-maximised", pane_id, theme)

                    client.state("restore_maximised_tab")
                    assert_menu(client, pane_id, floating=True)
                    if cycle == 0:
                        for theme in THEMES:
                            capture_menu(client, output, f"{label}-restored", pane_id, theme)

                    if kind == "document":
                        client.state("dock_document", kind="Panel2D", path=panel_path)
                    else:
                        target = "Output" if pane_id == "Project" else "Project"
                        client.state("move_workspace_pane", paneId=pane_id, placement="tab", targetId=target)
                    assert_no_floating_windows(client)
                    assert_menu(client, pane_id)
                    if kind == "document":
                        document_state(client, panel_path, element_id, floating=False)

            # The native document's pending edit and command history survive all
            # fifteen window cycles and remain undoable after its final redock.
            client.state("activate_document", kind="Panel2D", path=panel_path)
            state = client.state("inspect_workspace")
            assert state["isDirty"] and state["canUndo"]
            assert next(element for element in state["elements"] if element["id"] == element_id)["name"] == "Cycle retained edit"
            client.state("history")
            assert next(element for element in client.state("inspect_workspace")["elements"]
                        if element["id"] == element_id)["name"] != "Cycle retained edit"
            client.state("history", redo=True)
            state = client.state("inspect_workspace")
            assert state["isDirty"] and state["canUndo"]
            assert next(element for element in state["elements"] if element["id"] == element_id)["name"] == "Cycle retained edit"
            assert_no_floating_windows(client)
            print("Native asset and four workspace panes completed three actual-menu float/maximise/restore/redock cycles; menu state, Dark/Light popup captures, floating-window cleanup, dirty edits and undo/redo history passed.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
