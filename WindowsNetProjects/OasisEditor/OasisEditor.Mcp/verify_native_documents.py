"""Verify independent native documents, Dock splits/floating and workspace restoration through headless MCP."""
import argparse
import base64
import json
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


def document_ids(node):
    return {entry.get("id") for entry in walk(node) if entry.get("kind") == "Document"}


def document_owners(layout):
    owners = {}
    for node in walk(layout):
        if node.get("kind") != "Document":
            continue
        for parent in walk(layout):
            if parent.get("kind") == "Documents" and node in (parent.get("children") or []):
                owners[node.get("id")] = parent.get("id")
                break
    return owners


def assert_split(layout, first_id, second_id, distinct_hosts=False):
    for node in walk(layout):
        if node.get("kind") == "Split" and {first_id, second_id} <= document_ids(node):
            if distinct_hosts:
                owners = document_owners(node)
                assert first_id in owners and second_id in owners and owners[first_id] != owners[second_id], owners
            return
    raise AssertionError(f"Documents {first_id} and {second_id} are not under a shared Dock split")


def capture_workspace(client, output, name, expected_images, themes=("Dark",)):
    for theme in themes:
        result = client.tool("capture_workspace", theme=theme, width=1400, height=900)
        images = [item for item in result["content"] if item["type"] == "image"]
        assert len(images) == expected_images, (theme, len(images), expected_images)
        for index, image in enumerate(images):
            png = base64.b64decode(image["data"])
            assert png[:8] == b"\x89PNG\r\n\x1a\n"
            (output / f"{name}-{theme.lower()}-{index}.png").write_bytes(png)


def menu_entry(menus, menu_name, item_name):
    menu = next(item for item in menus if item["header"] == menu_name)
    return next(item for item in menu["items"] if item["header"] == item_name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="OasisNativeDocuments-") as temporary:
        root = Path(temporary)
        client = Client(args.dll.resolve(), root)
        try:
            tools = {item["name"] for item in client.request("tools/list", {})["tools"]}
            assert {"split_document", "float_document", "activate_document", "history",
                    "capture_workspace", "save_workspace", "restore_workspace", "inspect_menus",
                    "capture_menu", "inspect_panel_element_properties", "update_panel_element_properties"} <= tools

            client.state("create_project", name="Native document hosts")
            project = Path(client.state("inspect_workspace")["projectFilePath"]).parent
            first = client.state("create_panel2d", name="Panel A")
            first_path = first["panelPath"]
            first_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=first_id, name="A independent history")

            second = client.state("create_panel2d", name="Panel B")
            second_path = second["panelPath"]
            second_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=second_id, name="B independent history")

            third = client.state("create_panel2d", name="Panel C")
            third_path = third["panelPath"]
            third_id = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=third_id, name="C floating history")

            reel = client.state("create_reel", name="Split physical Reel")
            reel_path = reel["path"]
            reel_state = client.state("inspect_reel")
            reel_id = reel_state["document"]["id"]
            assert reel_state["scene"]["meshes"] > 0 and not reel_state["scene"].get("renderingError"), reel_state

            client.state("activate_document", kind="Panel2D", path=second_path)
            client.state("history")
            second_after_undo = client.state("inspect_workspace")
            assert second_after_undo["elements"][0]["name"] != "B independent history"
            client.state("activate_document", kind="Panel2D", path=first_path)
            first_before_undo = client.state("inspect_workspace")
            assert first_before_undo["elements"][0]["name"] == "A independent history"
            client.state("history")
            first_after_undo = client.state("inspect_workspace")
            assert first_after_undo["elements"][0]["name"] != "A independent history"
            client.state("activate_document", kind="Panel2D", path=second_path)
            second_after_switch = client.state("inspect_workspace")
            assert second_after_switch["elements"][0]["name"] != "B independent history"
            client.state("history", redo=True)
            assert client.state("inspect_workspace")["elements"][0]["name"] == "B independent history"
            client.state("activate_document", kind="Panel2D", path=first_path)
            client.state("history", redo=True)
            assert client.state("inspect_workspace")["elements"][0]["name"] == "A independent history"

            before_split = client.state("inspect_layout")
            asset_docs = before_split["assetLayout"]["documents"]
            first_doc = next(doc for doc in asset_docs if doc["path"].endswith("Panel A/asset.panel2d"))
            reel_doc = next(doc for doc in asset_docs if doc["kind"] == "Reel")
            client.state("split_document", kind="Reel", path=reel_path,
                         targetKind="Panel2D", targetPath=first_path, side="right")
            split_state = client.state("inspect_layout")
            assert_split(split_state["assetLayout"]["layout"], first_doc["id"], reel_doc["id"], distinct_hosts=True)
            capture_workspace(client, output, "native-mixed-split", 1, themes=("Dark", "Light"))
            reel_state = client.state("inspect_reel")
            assert reel_state["scene"]["gpu"]["triangles"] > 0, reel_state

            # A real menu capture and Inspector command must stay with the active Panel editor.
            client.state("activate_document", kind="Panel2D", path=first_path)
            label = client.state("add_element", kind="Label", name="Inspector target", x=25, y=35, width=180, height=40)
            label_id = label["selectedId"]
            menu_state = client.state("inspect_menus")
            undo_menu = menu_entry(menu_state, "Edit", "Undo")
            assert undo_menu["enabled"], undo_menu
            assert menu_entry(menu_state, "File", "Save Asset")["enabled"]
            menu_capture = client.tool("capture_menu", path=["Edit"], theme="Dark")
            menu_png = base64.b64decode(next(item["data"] for item in menu_capture["content"] if item["type"] == "image"))
            assert menu_png.startswith(b"\x89PNG\r\n\x1a\n")
            (output / "native-active-edit-menu.png").write_bytes(menu_png)
            properties = client.state("inspect_panel_element_properties", id=label_id)["properties"]
            assert properties.get("displayText") is None
            client.state("update_panel_element_properties", id=label_id, settings={"displayText": "Inspector retained"})
            assert any(element["id"] == label_id and element["displayText"] == "Inspector retained"
                       for element in client.state("inspect_workspace")["elements"])
            client.state("activate_document", kind="Reel", path=reel_path)
            client.state("update_reel", widthMm=71)
            reel_menu = client.state("inspect_menus")
            assert menu_entry(reel_menu, "File", "Save Asset")["enabled"]
            after_inspector = client.state("inspect_reel")["document"]
            assert after_inspector["id"] == reel_id and after_inspector["diameterMm"] == reel_state["document"]["diameterMm"] and after_inspector["widthMm"] == 71
            client.state("activate_document", kind="Panel2D", path=first_path)
            assert any(element["id"] == label_id and element["displayText"] == "Inspector retained"
                       for element in client.state("inspect_workspace")["elements"])

            client.state("float_document", kind="Panel2D", path=third_path)
            floating = client.state("inspect_floating_documents")
            third_relative = Path(third_path).resolve().relative_to(project.resolve()).as_posix()
            assert len(floating) == 1 and floating[0]["path"].replace("\\", "/") == third_relative, floating
            client.state("update_floating_element", kind="Panel2D", path=third_path, id=third_id,
                         name="C retained while floating")
            capture_workspace(client, output, "native-floating", 2)

            client.state("save_all_documents")
            docs = client.state("list_open_documents")
            assert len(docs) == 4 and all(not doc["isDirty"] for doc in docs), docs
            settings = str(root / "native-documents.workspace.json")
            saved = client.state("save_workspace", path=settings)
            assert len(saved["assetLayout"]["documents"]) == 4
            client.state("close_project")
            restored = client.state("restore_workspace", path=settings)
            assert_split(restored["assetLayout"]["layout"], first_doc["id"], reel_doc["id"], distinct_hosts=True)
            restored_floating = client.state("inspect_floating_documents")
            assert len(restored_floating) == 1 and restored_floating[0]["path"].replace("\\", "/") == third_relative
            restored_docs = client.state("list_open_documents")
            assert len(restored_docs) == 4 and sum(doc["isFloating"] for doc in restored_docs) == 1
            capture_workspace(client, output, "native-restored", 2)

            client.state("activate_document", kind="Panel2D", path=first_path)
            assert client.state("inspect_workspace")["elements"][0]["name"] == "A independent history"
            client.state("activate_document", kind="Panel2D", path=second_path)
            assert client.state("inspect_workspace")["elements"][0]["name"] == "B independent history"
            client.state("activate_document", kind="Panel2D", path=first_path)
            assert any(element["id"] == label_id and element["displayText"] == "Inspector retained"
                       for element in client.state("inspect_workspace")["elements"])
            client.state("activate_document", kind="Reel", path=reel_path)
            capture_workspace(client, output, "native-restored-reel", 2)
            restored_reel = client.state("inspect_reel")
            assert restored_reel["document"]["id"] == reel_id and restored_reel["scene"]["gpu"]["triangles"] > 0
            third_state = client.state("inspect_floating_document", kind="Panel2D", path=third_path)
            assert third_state["elements"][0]["name"] == "C retained while floating"
            client.state("close_project")
            print("Independent Panel2D histories, mixed Panel2D/Reel split hosts, menu/Inspector routing, floating state, save/restore and Dark/Light captures passed.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
