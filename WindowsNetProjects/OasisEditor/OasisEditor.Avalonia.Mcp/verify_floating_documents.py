"""Exercise real independent asset editors through isolated, headless MCP windows."""
import argparse
import base64
import json
from pathlib import Path
import struct
import tempfile
from verify_mcp import Client


def triangle_glb():
    binary = struct.pack("<9f", -.5, 0, 0, .5, 0, 0, 0, 1, 0)
    document = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
        "nodes": [{"name": "Authored model", "mesh": 0}], "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}],
        "buffers": [{"byteLength": len(binary)}], "bufferViews": [{"buffer": 0, "byteLength": len(binary)}],
        "accessors": [{"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [-.5, 0, 0], "max": [.5, 1, 0]}]}
    data = json.dumps(document).encode(); data += b" " * (-len(data) % 4)
    return struct.pack("<III", 0x46546c67, 2, 12 + 8 + len(data) + 8 + len(binary)) + struct.pack("<II", len(data), 0x4e4f534a) + data + struct.pack("<II", len(binary), 0x004e4942) + binary


def captures(client, output, name, expected):
    for theme in ("Dark", "Light"):
        result = client.tool("capture_workspace", theme=theme, width=1400, height=900)
        images = [item for item in result["content"] if item["type"] == "image"]
        assert len(images) == expected + 1, (len(images), expected)
        for index, item in enumerate(images):
            png = base64.b64decode(item["data"]); assert png[:8] == b"\x89PNG\r\n\x1a\n"
            (output / f"{name}-{theme.lower()}-{index}.png").write_bytes(png)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dll","--host",dest="dll",type=Path,default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll")
    args = parser.parse_args()
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    dll = args.dll.resolve()
    with tempfile.TemporaryDirectory(prefix="OasisFloatingMcp-") as temporary:
        root = Path(temporary); client = Client(dll, root)
        try:
            state = client.state("create_project", name="Floating assets")
            project = Path(state["projectFilePath"]).parent
            machine = next(a["manifestPath"] for a in state["assets"] if a["type"] == "Machine")
            panel = client.state("create_panel2d", name="Floating controls")["panelPath"]
            element = client.state("add_rectangle")["selectedId"]
            client.state("create_panel2d", name="Docked controls"); client.state("add_rectangle")
            original = Path(panel).read_bytes()
            client.state("float_document", kind="Panel2D", path=panel)
            edited = client.state("update_floating_element", kind="Panel2D", path=panel, id=element, name="Independent controls", x=50)
            assert edited["elements"][0]["name"] == "Independent controls" and Path(panel).read_bytes() == original
            face = client.state("create_face", name="Floating Face")["face"]["facePath"]
            lamp = client.state("add_face_lamp", name="Lamp", x=40, y=30, width=100, height=80)["face"]["selectedId"]
            client.state("float_document", kind="Face", path=face)
            client.state("update_floating_element", kind="Face", path=face, id=lamp, name="Independent lamp")
            client.state("floating_document_history", kind="Panel2D", path=panel)
            client.state("floating_document_history", kind="Panel2D", path=panel, redo=True)
            captures(client, output, "two-editors", 2)
            # Reopening an owned asset must activate its existing window, never create a second document owner.
            client.state("open_panel2d", path=panel); client.state("open_face", path=face)
            assert len(client.state("list_open_documents")) == 3
            reel = client.state("create_reel", name="Physical Reel")["path"]
            client.state("float_document", kind="Reel", path=reel)
            client.state("update_floating_reel", path=reel, widthMm=74)
            prefab = client.state("create_prefab", name="Floating Prefab")["asset"]["path"]
            client.state("float_document", kind="Prefab", path=prefab)
            native = json.loads(client.state("inspect_floating_document", kind="Prefab", path=prefab)["nativeJson"])
            client.state("add_floating_prefab_node", path=prefab, parentId=native["root"]["id"], name="Retained child")
            (project / "Assets/Source.glb").write_bytes(triangle_glb())
            cabinet = client.state("create_cabinet3d", name="Floating Cabinet", sourceProjectRelativePath="Assets/Source.glb")["asset"]["path"]
            client.state("float_document", kind="Cabinet3D", path=cabinet)
            client.state("set_floating_cabinet_model", path=cabinet, scale=1.5)
            client.state("open_machine", path=machine)
            client.state("float_document", kind="Machine", path=machine)
            client.state("set_floating_machine_name", path=machine, name="Floating Machine")
            client.state("save_all_documents")
            assert all(not t["isDirty"] for t in client.state("list_open_documents"))
            library = root / "Library"; library.mkdir(); client.state("configure_library_root", directory=str(library))
            shared = client.state("copy_asset_to_library", projectManifest=reel)["result"]; assert shared["success"]
            relative = shared["reference"]["path"]; shared_path = str(library / relative)
            client.state("open_library_asset", libraryRelativeManifest=relative)
            client.state("float_document", kind="Reel", path=shared_path)
            client.state("update_floating_reel", path=shared_path, widthMm=84); client.state("save_floating_document", kind="Reel", path=shared_path)
            assert json.loads(Path(reel).read_text())["widthMm"] == 74
            assert json.loads(Path(shared_path).read_text())["widthMm"] == 84
            captures(client, output, "all-editors", 7)
            for kind, path in (("Reel", reel), ("Cabinet3D", cabinet)):
                scene = client.state("inspect_floating_document", kind=kind, path=path)["scene"]
                assert scene["gpu"]["triangles"] > 0 and not scene.get("renderingError"), scene
            settings = str(root / "floating.workspace.json")
            saved = client.state("save_workspace", path=settings); assert len(saved["floatingDocuments"]) == 7
            client.state("close_project"); assert not client.state("inspect_floating_documents")
            restored = client.state("restore_workspace", path=settings); assert len(restored["floatingDocuments"]) == 7
            captures(client, output, "restored-editors", 7)
            client.state("dock_document", kind="Cabinet3D", path=cabinet)
            client.state("update_floating_reel", path=shared_path, widthMm=92)
            with Path(shared_path).open("a") as file: file.write("\n")
            conflict = client.request("tools/call", {"name": "save_floating_document", "arguments": {"kind": "Reel", "path": shared_path}})
            assert conflict.get("isError"), conflict
            client.state("dock_document", kind="Reel", path=shared_path); client.state("scene_history")
            captures(client, output, "after-redocking", 5)
            # Disposing/redocking sibling GPU editors must preserve the still-floating Reel renderer.
            assert client.state("inspect_floating_document", kind="Reel", path=reel)["scene"]["gpu"]["triangles"] > 0
            client.state("close_project")
            print("Independent native Panel/Face/Machine/Cabinet/Prefab/Reel and Library editors: edits/history/save/conflicts, workspace restore, redocking and actual Dark/Light/GPU captures passed.")
        finally: client.close()


if __name__ == "__main__": main()
