"""Exercise tab maximise/restore through the production headless MCP host."""
import argparse
import base64
from copy import deepcopy
from pathlib import Path
import tempfile

from verify_mcp import Client


def walk(node):
    if not node:
        return
    yield node
    for key in ("children", "hidden", "left", "right", "top", "bottom"):
        for child in node.get(key) or []:
            yield from walk(child)
    for window in node.get("windows") or []:
        yield from walk(window.get("layout"))


def asset_document(layout, path):
    docs = layout["assetLayout"]["documents"]
    return next(doc for doc in docs if Path(doc["path"]).name == "asset.panel2d"
                and Path(path).parent.name in doc["path"].replace("\\", "/"))


def topology(node):
    """Dock ownership/topology without transient host bounds/state."""
    if node is None:
        return None
    result = {}
    for key, value in node.items():
        if key == "windows":
            result[key] = [{"layout": topology(window.get("layout"))} for window in value or []]
            continue
        if key in ("children", "hidden", "left", "right", "top", "bottom"):
            result[key] = [topology(child) for child in value or []]
        else:
            result[key] = value
    return result


def capture(client, output, name, expected_windows, themes):
    for theme in themes:
        result = client.tool("capture_workspace", theme=theme, width=1400, height=900)
        images = [item for item in result["content"] if item["type"] == "image"]
        assert len(images) == expected_windows, (name, theme, len(images), expected_windows)
        for index, item in enumerate(images):
            png = base64.b64decode(item["data"])
            assert png.startswith(b"\x89PNG\r\n\x1a\n")
            (output / f"{name}-{theme.lower()}-{index}.png").write_bytes(png)


def assert_menu(client, pane_id, maximised):
    state = client.state("inspect_tab_menu", paneId=pane_id)
    items = {item["header"]: item for item in state["items"]}
    assert items["Maximise"]["checkedState"] is maximised, items
    assert items["Close Tab"]["enabled"] is (not maximised), items


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--themes", nargs="+", choices=("Dark", "Light"), default=("Dark", "Light"))
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="OasisTabMaximise-") as temporary:
        client = Client(args.dll.resolve(), Path(temporary))
        try:
            tools = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert {"toggle_tab_maximised", "inspect_tab_presentation", "restore_maximised_tab",
                    "inspect_tab_menu", "capture_tab_menu", "inspect_layout", "capture_workspace", "float_document",
                    "move_workspace_pane", "inspect_floating_documents", "history"} <= tools

            client.state("create_project", name="Transient tab maximise")
            panel_state = client.state("create_panel2d", name="Dirty retained Panel")
            panel_path = panel_state["panelPath"]
            element_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=element_id, name="Unsaved tab content", x=37)

            before = client.state("inspect_layout")
            doc = asset_document(before, panel_path)
            document_id = doc["id"]
            assert client.state("inspect_workspace")["isDirty"]
            assert_menu(client, document_id, False)
            initial_topology = topology(before["assetLayout"]["layout"])

            # A docked native asset tab and a general workspace pane both maximise
            # without mutating the saved Dock tree or the asset's edit history.
            state = client.state("toggle_tab_maximised", paneId=document_id)
            assert state == {"maximisedPaneId": document_id, "wasFloating": False}, state
            assert client.state("inspect_tab_presentation")["maximisedPaneId"] == document_id
            assert_menu(client, document_id, True)
            menu_capture = client.tool("capture_tab_menu", paneId=document_id, theme="Dark")
            menu_png = base64.b64decode(next(item["data"] for item in menu_capture["content"] if item["type"] == "image"))
            assert menu_png.startswith(b"\x89PNG\r\n\x1a\n")
            (output / "docked-asset-maximised-context-menu-dark.png").write_bytes(menu_png)
            during = client.state("inspect_layout")
            assert topology(during["assetLayout"]["layout"]) == initial_topology
            capture(client, output, "docked-asset-maximised", 1, args.themes)
            restored = client.state("restore_maximised_tab")
            assert restored.get("maximisedPaneId") is None and restored["wasFloating"] is False, restored
            assert client.state("inspect_layout")["assetLayout"]["layout"] == before["assetLayout"]["layout"]

            state = client.state("toggle_tab_maximised", paneId="Project")
            assert state["maximisedPaneId"] == "Project" and state["wasFloating"] is False, state
            assert_menu(client, "Project", True)
            assert topology(client.state("inspect_layout")["assetLayout"]["layout"]) == initial_topology
            capture(client, output, "docked-project-pane-maximised", 1, args.themes)
            client.state("restore_maximised_tab")
            assert client.state("inspect_layout")["assetLayout"]["layout"] == before["assetLayout"]["layout"]

            # Floating-asset restore checks exact saved bounds and window state.
            client.state("float_document", kind="Panel2D", path=panel_path, x=95, y=115, width=940, height=620)
            floating_before = client.state("inspect_floating_documents")
            assert len(floating_before) == 1, floating_before
            saved_asset_bounds = deepcopy(floating_before[0]["bounds"])
            floating_layout = client.state("inspect_layout")
            floating_topology = topology(floating_layout["assetLayout"]["layout"])
            state = client.state("toggle_tab_maximised", paneId=document_id)
            assert state["maximisedPaneId"] == document_id and state["wasFloating"] is True, state
            assert_menu(client, document_id, True)
            assert topology(client.state("inspect_layout")["assetLayout"]["layout"]) == floating_topology
            capture(client, output, "floating-asset-maximised", 2, args.themes)
            client.state("restore_maximised_tab")
            floating_after = client.state("inspect_floating_documents")
            assert floating_after[0]["bounds"] == saved_asset_bounds, (saved_asset_bounds, floating_after)
            assert client.state("inspect_layout")["assetLayout"]["layout"] == floating_layout["assetLayout"]["layout"]

            # A floating pane uses the existing native host and returns to the
            # same owner and bounds after its content is maximised temporarily.
            client.state("move_workspace_pane", paneId="Project", placement="float")
            pane_layout = client.state("inspect_layout")
            pane_window = next(window for window in pane_layout["layout"].get("windows", [])
                               if "Project" in {node.get("id") for node in walk(window.get("layout"))})
            saved_pane_bounds = deepcopy(pane_window["bounds"])
            pane_topology = topology(pane_layout["layout"])
            state = client.state("toggle_tab_maximised", paneId="Project")
            assert state["maximisedPaneId"] == "Project" and state["wasFloating"] is True, state
            assert_menu(client, "Project", True)
            assert topology(client.state("inspect_layout")["assetLayout"]["layout"]) == pane_topology
            capture(client, output, "floating-project-pane-maximised", 3, args.themes)
            client.state("restore_maximised_tab")
            pane_after = client.state("inspect_layout")
            pane_window_after = next(window for window in pane_after["layout"].get("windows", [])
                                     if "Project" in {node.get("id") for node in walk(window.get("layout"))})
            assert pane_window_after["bounds"] == saved_pane_bounds, (saved_pane_bounds, pane_window_after)
            assert pane_after["layout"] == pane_layout["layout"]

            # The original dirty data and undo/redo stack survive every transient
            # presentation operation.
            client.state("activate_document", kind="Panel2D", path=panel_path)
            current = client.state("inspect_workspace")
            assert current["isDirty"] and current["canUndo"]
            assert next(element for element in current["elements"] if element["id"] == element_id)["name"] == "Unsaved tab content"
            client.state("history")
            assert next(element for element in client.state("inspect_workspace")["elements"]
                        if element["id"] == element_id)["name"] != "Unsaved tab content"
            client.state("history", redo=True)
            final = client.state("inspect_workspace")
            assert final["isDirty"] and final["canUndo"]
            assert next(element for element in final["elements"] if element["id"] == element_id)["name"] == "Unsaved tab content"
            print("Docked and floating asset/pane tabs maximised and restored; Dock topology, host bounds, context menu state, dirty content and undo/redo history passed in Dark and Light captures.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
