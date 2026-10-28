"""Exercise live Dock pane float/tab operations and render their actual content."""
import argparse
import base64
from pathlib import Path
import tempfile
from verify_mcp import Client


def nodes(node):
    if not node:
        return
    yield node
    for key in ("children", "hidden", "left", "right", "top", "bottom"):
        for child in node.get(key) or []:
            yield from nodes(child)
    for window in node.get("windows") or []:
        yield from nodes(window.get("layout"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path, default=Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="OasisLivePaneDocking-") as folder:
        client = Client(args.dll.resolve(), Path(folder))
        try:
            client.state("create_project", name="Live pane docking")
            client.state("create_panel2d", name="Retained edits")
            element_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=element_id, name="Retained through live pane moves")
            layout = client.state("inspect_layout")
            asset_layout = layout.get("assetLayout") or {}
            target_id = asset_layout.get("activeId")
            assert target_id, "The created asset must be represented by a native document host"
            tool_ids = {node.get("id") for node in nodes(layout["layout"]) if node.get("kind") == "Tool"}
            panes = [pane for pane in ("Project", "Hierarchy", "Inspector", "Output", "Authoring") if pane in tool_ids]
            assert {"Project", "Hierarchy", "Inspector", "Output"} <= set(panes), panes
            for pane in panes:
                client.state("move_workspace_pane", paneId=pane, placement="float")
                images = client.tool("capture_workspace", theme="Dark")["content"]
                images = [item for item in images if item["type"] == "image"]
                assert len(images) >= 2, pane
                for index, image in enumerate(images):
                    (args.output / f"{pane.lower()}-floating-{index}.png").write_bytes(base64.b64decode(image["data"]))
                client.state("move_workspace_pane", paneId=pane, placement="tab", targetId=target_id)
                client.capture(args.output / f"{pane.lower()}-redocked.png", "Light")
                state = client.state("inspect_workspace")
                assert state["isDirty"] and state["selectedId"] == element_id, pane
                assert state["elements"][0]["name"] == "Retained through live pane moves", pane
            print(f"All {len(panes)} available live Dock panes floated, redocked and rendered; native document edits and selection survived.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
