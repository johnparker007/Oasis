"""Exercise standard workspace tab menus, close/reopen, and captures through MCP."""
import argparse
import base64
from pathlib import Path
import tempfile

from verify_mcp import Client


def walk(node, include_hidden=True):
    if not node:
        return
    yield node
    for key in ("children", "left", "right", "top", "bottom"):
        for child in node.get(key) or []:
            yield from walk(child, include_hidden)
    if include_hidden:
        for child in node.get("hidden") or []:
            yield from walk(child, include_hidden)
    for window in node.get("windows") or []:
        yield from walk(window.get("layout"), include_hidden)


def visible_ids(layout):
    return {node.get("id") for node in walk(layout, include_hidden=False) if node.get("id")}


def all_ids(layout):
    return [node.get("id") for node in walk(layout) if node.get("id")]


def capture(client, output, stem, theme):
    result = client.tool("capture_workspace", theme=theme, width=1400, height=900)
    images = [item for item in result["content"] if item["type"] == "image"]
    assert images, (stem, theme, result)
    for index, image in enumerate(images):
        png = base64.b64decode(image["data"])
        assert png.startswith(b"\x89PNG\r\n\x1a\n")
        suffix = f"-{index}" if len(images) > 1 else ""
        (output / f"{stem}-{theme.lower()}{suffix}.png").write_bytes(png)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="OasisStandardTabs-") as temporary:
        client = Client(args.dll.resolve(), Path(temporary))
        try:
            tools = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert {"inspect_tab_menu", "add_workspace_tab", "close_workspace_tab", "inspect_layout",
                    "create_project", "create_panel2d", "capture_workspace"} <= tools

            client.state("create_project", name="Standard tab actions")
            panel = client.state("create_panel2d", name="Document beside restored panes")
            panel_path = panel["panelPath"]
            before = client.state("inspect_layout")
            layout_before = before["layout"]
            assert {"Hierarchy", "Inspector", "Project", "Output"} <= visible_ids(layout_before), layout_before

            menu = client.state("inspect_tab_menu", paneId="Project")
            entries = {item["header"]: item for item in menu["items"]}
            assert {"Maximise", "Add Tab", "Close Tab", "Float"} <= set(entries), entries
            assert {"Hierarchy", "Inspector", "Project", "Output"} <= {
                child["header"] for child in entries["Add Tab"]["children"]
            }, entries["Add Tab"]
            assert entries["Maximise"]["checkedState"] is not True

            for theme in ("Dark", "Light"):
                capture(client, output, "standard-tabs-before-close", theme)

            document_id = next(doc["id"] for doc in before["assetLayout"]["documents"] if doc["kind"] == "Panel2D")
            for pane in ("Hierarchy", "Inspector", document_id):
                client.state("toggle_tab_maximised", paneId=pane)
                client.state("restore_maximised_tab")
                for theme in ("Dark", "Light"):
                    capture(client, output, f"{pane.lower().replace(chr(58), chr(45))}-restored-active", theme)

            client.state("close_workspace_tab", paneId="Hierarchy")
            closed = client.state("inspect_layout")["layout"]
            assert "Hierarchy" not in visible_ids(closed), closed
            assert all_ids(closed).count("Hierarchy") == 1, closed

            client.state("add_workspace_tab", paneId="Hierarchy", targetPaneId="Inspector")
            restored = client.state("inspect_layout")["layout"]
            assert "Hierarchy" in visible_ids(restored), restored
            assert all_ids(restored).count("Hierarchy") == 1, restored
            assert {"Inspector", "Hierarchy", "Project", "Output"} <= visible_ids(restored), restored
            assert any(doc["kind"] == "Panel2D" and Path(doc["path"]).name == "asset.panel2d"
                       for doc in client.state("list_open_documents")), panel_path

            restored_menu = client.state("inspect_tab_menu", paneId="Hierarchy")
            restored_entries = {item["header"]: item for item in restored_menu["items"]}
            assert {"Maximise", "Add Tab", "Close Tab", "Float"} <= set(restored_entries), restored_entries
            assert {"Hierarchy", "Inspector", "Project", "Output"} <= {
                child["header"] for child in restored_entries["Add Tab"]["children"]
            }, restored_entries["Add Tab"]
            for theme in ("Dark", "Light"):
                capture(client, output, "standard-tabs-restored-with-document", theme)

            print("Standard tab menu entries, production close/reopen behavior, singleton pane ownership, native document retention, and Dark/Light workspace captures passed.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
