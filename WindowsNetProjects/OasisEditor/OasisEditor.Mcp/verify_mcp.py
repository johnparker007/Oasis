"""Exercise the real headless MCP host. Requires Python's standard library only.

Build OasisEditor.Mcp first. --host accepts a self-contained apphost; --dll accepts a framework-dependent assembly. Outputs PNGs to --output (never committed).
Optional --project/--panel capture an existing project read-only; no authored writes.
"""
import argparse
import base64
import json
from pathlib import Path
import queue
import struct
import subprocess
import tempfile
import threading
import shutil
import zlib


class Client:
    def __init__(self, dll, workspace):
        # A DLL uses the developer/runtime host; a published apphost runs its bundled runtime directly.
        command = ["dotnet", str(dll)] if Path(dll).suffix.lower() == ".dll" else [str(dll)]
        self.process = subprocess.Popen(
            [*command, "--workspace", str(workspace)],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, encoding="utf-8", creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        self.responses = queue.Queue()
        self.errors = []
        self.serial = 0
        def read():
            for line in self.process.stdout:
                try:
                    self.responses.put(json.loads(line))
                except json.JSONDecodeError:
                    self.responses.put({"error": {"message": "Non-protocol stdout: " + line}})
            self.responses.put({"error": {"message": "MCP process closed stdout"}})
        def diagnostics():
            for line in self.process.stderr:
                self.errors.append(line)
        threading.Thread(target=read, daemon=True).start()
        threading.Thread(target=diagnostics, daemon=True).start()
        self.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                    "clientInfo": {"name": "oasis-background-check", "version": "1"}})
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized"})

    def send(self, message):
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()

    def request(self, method, params):
        self.serial += 1
        self.send({"jsonrpc": "2.0", "id": self.serial, "method": method, "params": params})
        while True:
            try:
                reply = self.responses.get(timeout=45)
            except queue.Empty:
                raise RuntimeError("MCP timed out:\n" + "".join(self.errors))
            if "error" in reply:
                raise RuntimeError(str(reply["error"]) + "\n" + "".join(self.errors))
            if reply.get("id") == self.serial:
                return reply["result"]

    def tool(self, tool_name, **arguments):
        result = self.request("tools/call", {"name": tool_name, "arguments": arguments})
        if result.get("isError"):
            raise RuntimeError(str(result) + "\n" + "".join(self.errors))
        return result

    def state(self, tool_name="inspect_workspace", **arguments):
        result = self.tool(tool_name, **arguments)
        return json.loads(next(c["text"] for c in result["content"] if c["type"] == "text"))

    def capture(self, path, theme, width=1400, height=900):
        result = self.tool("capture_workspace", theme=theme, width=width, height=height)
        png = base64.b64decode(next(c["data"] for c in result["content"] if c["type"] == "image"))
        assert png[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", png[16:24]) == (width, height)
        path.write_bytes(png)

    def close(self):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
            raise RuntimeError("MCP host did not exit after stdin closed")
        if self.process.returncode:
            raise RuntimeError("".join(self.errors))


def verify_reel_library(client, root, output, machine_path, project_directory):
    reel = client.state("create_reel", name="Shared physical Reel")
    reel_path, reel_id = reel["path"], reel["document"]["id"]
    client.state("update_reel", diameterMm=290, widthMm=70)
    client.state("scene_history"); client.state("scene_history", redo=True); client.state("save_reel")
    library = root / "Library"; library.mkdir()
    client.state("configure_library_root", directory=str(library))
    copied = client.state("copy_asset_to_library", projectManifest=reel_path)["result"]
    assert copied["success"], copied
    library_path = copied["reference"]["path"]
    client.state("open_library_asset", libraryRelativeManifest=library_path)
    client.state("update_reel", widthMm=80); client.state("save_reel")
    assert json.loads(Path(reel_path).read_text())["widthMm"] == 70
    for theme in ("Dark", "Light"):
        client.capture(output / f"workspace-library-reel-{theme.lower()}.png", theme)
    reel = client.state("inspect_reel")
    assert reel["document"]["id"] == reel_id and reel["document"]["widthMm"] == 80
    assert reel["scene"]["gpu"]["triangles"] > 0 and not reel["scene"].get("renderingError"), reel
    settings = str(root / "library.workspace.json")
    client.state("save_workspace", path=settings); client.state("reset_layout")
    client.state("restore_workspace", path=settings)
    client.capture(output / "workspace-library-restored.png", "Dark")
    assert client.state("inspect_layout")["scene3D"]["scope"] == "Library"
    imported = client.state("import_library_asset", libraryRelativeManifest=library_path, packageName="Imported Reel")["result"]
    assert imported["success"] and json.loads(Path(imported["manifestPath"]).read_text())["id"] == reel_id
    assert not client.state("copy_asset_to_library", projectManifest=reel_path)["result"]["success"]
    client.state("open_machine", path=machine_path)
    client.state("assign_machine_reel", reelReference="reel:0", assetPath=library_path, scope="Library")
    native = client.state("inspect_machine")["document"]
    assert native["reelAssignments"][0]["reelAsset"] == {"scope":"library", "path":library_path}
    client.state("update_machine_document", document=native)  # Native runtime union JSON must round-trip over MCP.
    client.state("machine_history"); client.state("machine_history", redo=True); client.state("save_machine")
    print("Native Reel/Library authoring, copy/import/conflicts, scoped assignment, workspace restoration and GPU captures passed.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--project", type=Path)
    parser.add_argument("--panel", type=Path)
    parser.add_argument("--face", type=Path)
    parser.add_argument("--reel-library", action="store_true", help="Exercise native Reel/Library authoring and actual offscreen GPU captures")
    parser.add_argument("--cabinet", help="Read-only Cabinet3D manifest relative to --project")
    parser.add_argument("--prefab", help="Read-only Object3D manifest relative to --project")
    parser.add_argument("--scene-machine", help="Read-only Machine manifest relative to --project")
    parser.add_argument("--library-root", help="Explicit Library directory for Machine asset resolution")
    parser.add_argument("--fml", type=Path, help="Import a disposable copy of an actual FML; never modifies the source")
    parser.add_argument("--copy-project", type=Path, help="Copy an existing disposable Oasis project into the isolated MCP workspace and run the production Machine build/export")
    parser.add_argument("--machine", default="Assets/Machines/RefactorTest6/asset.machine", help="Machine path inside --copy-project")
    parser.add_argument("--dll","--host",dest="dll",type=Path,default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    dll = args.dll.resolve()
    with tempfile.TemporaryDirectory(prefix="oasis-avalonia-") as temporary:
        root = Path(temporary)
        client = Client(dll, root)
        try:
            names = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert {"capture_workspace", "update_element", "history", "create_project", "create_face", "import_fml",
                    "generate_face_from_source_shape", "import_face_artwork", "set_face_artwork_registration",
                    "set_face_artwork_calibration", "import_face_artwork_override", "set_face_artwork_override_alignment",
                    "rebuild_face_artwork", "inspect_face_artwork", "list_machines", "open_machine", "inspect_machine",
                    "set_machine_platform_setting", "set_machine_interaction", "set_machine_behavior_source",
                    "set_machine_cabinet_asset", "apply_recovered_fml_inputs", "save_machine",
                    "build_machine_for_player", "export_machine_player_package"} <= names
            if args.copy_project:
                project_copy = root / "ActualMachineCopy"
                shutil.copytree(args.copy_project, project_copy)
                project_files = list(project_copy.glob("*.oasisproj"))
                assert len(project_files) == 1, f"Expected one copied .oasisproj, found: {project_files}"
                client.state("open_project", path=str(project_files[0]))
                client.state("open_machine", path=str(project_copy / args.machine))
                actual_build = client.state("build_machine_for_player")
                assert actual_build["success"] and actual_build["playerLaunched"] is False, actual_build
                actual_export = client.state("export_machine_player_package")
                assert actual_export["success"] and actual_export["playerLaunched"] is False, actual_export
                assert Path(actual_export["packageDirectory"], "machine.runtime.json").is_file()
                print("Copied authored project Machine built and exported through headless MCP; no Player process launched.")
            created = client.state("create_project", name="Panel Authoring")
            assert any(a["type"] == "Machine" for a in created["assets"])
            machine_path = client.state("list_machines")["machines"][0]["manifestPath"]
            machine = client.state("open_machine", path=machine_path)
            assert machine["document"]["schemaVersion"] == 11
            client.state("set_machine_name", displayName="MCP-authored Machine")
            client.state("set_machine_runtime", kind="Emulation", platform="Impact")
            changed_machine = client.state("set_machine_platform_setting", propertyPath="ProgramRom1Path", valueJson='"Roms/verified.bin"')
            assert changed_machine["document"]["runtime"]["platformSettings"]["programRom1Path"] == "Roms/verified.bin"
            client.state("machine_history")
            client.state("machine_history", redo=True)
            saved_machine = client.state("save_machine")
            assert not saved_machine["dirty"]
            client.capture(args.output / "workspace-machine-dark.png", "Dark")
            reloaded_machine = client.state("open_machine", path=machine_path)["document"]
            assert reloaded_machine["displayName"] == "MCP-authored Machine"
            project_directory = Path(created["projectFilePath"]).parent
            rom_directory = project_directory / "Roms"
            rom_directory.mkdir()
            (rom_directory / "verified.bin").write_bytes(b"Disposable MCP build ROM fixture")
            cabinet = project_directory / "Assets" / "Cabinet3D" / "Diagnostic"
            cabinet.mkdir(parents=True)
            (cabinet / "asset.cabinet3d").write_text(json.dumps({"version": 9, "model": {"path": "cabinet.glb", "scale": 1, "upAxis": "Y"}, "surfaceTargetSettings": []}, indent=2), encoding="utf-8")
            def make_glb(path):
                positions = struct.pack("<12f", 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)
                uvs = struct.pack("<8f", 0, 0, 1, 0, 1, 1, 0, 1)
                indices = struct.pack("<6H", 0, 1, 2, 0, 2, 3)
                binary = positions + uvs + indices
                gltf = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
                        "nodes": [{"name": "OasisFace_glass", "mesh": 0}],
                        "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "TEXCOORD_0": 1}, "indices": 2}]}],
                        "buffers": [{"byteLength": len(binary)}],
                        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 48},
                                         {"buffer": 0, "byteOffset": 48, "byteLength": 32},
                                         {"buffer": 0, "byteOffset": 80, "byteLength": 12}],
                        "accessors": [{"bufferView": 0, "componentType": 5126, "count": 4, "type": "VEC3"},
                                      {"bufferView": 1, "componentType": 5126, "count": 4, "type": "VEC2"},
                                      {"bufferView": 2, "componentType": 5123, "count": 6, "type": "SCALAR"}]}
                json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
                json_bytes += b" " * ((-len(json_bytes)) % 4)
                binary += b"\0" * ((-len(binary)) % 4)
                chunks = struct.pack("<I4s", len(json_bytes), b"JSON") + json_bytes + struct.pack("<I4s", len(binary), b"BIN\0") + binary
                path.write_bytes(struct.pack("<III", 0x46546C67, 2, 12 + len(chunks)) + chunks)
            make_glb(cabinet / "cabinet.glb")
            assigned_cabinet = client.state("set_machine_cabinet_asset", path="Assets/Cabinet3D/Diagnostic/asset.cabinet3d", scope="Project")
            assert "error" not in assigned_cabinet, assigned_cabinet
            client.state("save_machine")
            retained_prefab = client.state("create_prefab",name="Retained edits")
            prefab_path = retained_prefab["asset"]["path"]
            prefab_root = retained_prefab["asset"]["document"]["root"]["id"]
            client.state("add_prefab_node",parentNodeId=prefab_root,name="Pending edit")
            client.state("open_machine",path=machine_path)
            blocked = client.request("tools/call",{"name":"build_machine_for_player","arguments":{}})
            assert blocked.get("isError"),"Build must refuse dirty retained Prefab edits"
            client.state("open_prefab",path=prefab_path)
            client.state("scene_history")
            client.state("save_scene_document")
            client.state("open_machine",path=machine_path)
            build = client.state("build_machine_for_player")
            assert build["success"] and build["playerLaunched"] is False, build
            assert Path(build["buildRoot"], "machine.runtime.json").is_file()
            package = client.state("export_machine_player_package")
            assert package["success"] and package["playerLaunched"] is False
            assert Path(package["packageDirectory"], "machine.runtime.json").is_file()
            print("Headless Machine create/open/settings/history/save/reopen, production build/export and rendered capture passed; no Player process launched.")
            if args.reel_library:
                verify_reel_library(client,root,args.output,machine_path,project_directory)
            client.state("create_panel2d", name="Control panel")
            first = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=first, name="Artwork registration", x=0, y=0, width=680, height=420)
            second = client.state("add_rectangle")["selectedId"]
            changed = client.state("update_element", id=second, name="Button zone", x=50, y=280, width=170, height=80)
            assert changed["isDirty"]
            client.state("history")
            undone = client.state("history", redo=True)
            assert next(e for e in undone["elements"] if e["id"] == second)["name"] == "Button zone"
            client.state("save_panel2d")
            assert not client.state()["isDirty"]
            client.state("select_elements", ids=[first, second])
            duplicated = client.state("duplicate_selection", offsetX=24, offsetY=20)
            assert len(duplicated["elements"]) == 4
            client.state("move_selection", dx=20, dy=10)
            client.state("history")
            client.state("history", redo=True)
            client.state("add_source_shape", name="Main face", x=20, y=20, width=640, height=380)
            def chunk(kind, data):
                return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
            artwork = root / "Artwork.png"
            artwork.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 96, 64, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress((b"\0" + b"\xC4\x91\x3A" * 96) * 64)) + chunk(b"IEND", b""))
            imported_image = client.state("import_image", path=str(artwork), name="Button artwork")["selectedId"]
            client.state("update_element", id=imported_image, x=460, y=280, width=150, height=100)
            client.state("save_panel2d")
            client.capture(args.output / "workspace-dark.png", "Dark")
            client.capture(args.output / "workspace-light.png", "Light")
            client.capture(args.output / "workspace-compact.png", "Dark", 1024, 700)
            client.state("open_panel2d", path=client.state()["panelPath"])
            assert len(client.state()["elements"]) == 5
            assert len(client.state()["sourceShapes"]) == 1
            client.state("create_face", name="Lamp panel")
            face = client.state("add_face_lamp", name="Start button", x=160, y=140, width=260, height=120, lampIndex=0)
            lamp = face["face"]["selectedId"]
            client.state("update_face_element", id=lamp, name="Start lamp", lampIndex=3)
            client.state("face_history")
            client.state("face_history", redo=True)
            client.state("add_face_lamp", name="Hold lamp", x=460, y=140, width=260, height=120, lampIndex=7)
            client.state("import_face_artwork", path=str(artwork))
            client.state("set_face_artwork_registration", topLeftX=0.02, topLeftY=0.04, topRightX=0.98, topRightY=0.02,
                         bottomRightX=0.96, bottomRightY=0.96, bottomLeftX=0.04, bottomLeftY=0.98)
            calibration = {"strength": 65, "blackReference": {"manualEnabled": True, "manualColor": "#FF202020", "samples": []},
                           "whiteReference": {"manualEnabled": True, "manualColor": "#FFD0D0D0", "samples": []},
                           "sameColorGroups": [], "correctSpatialBrightness": False, "correctSpatialColor": True,
                           "normalizeBlackWhite": True, "neutralizeWhite": True}
            client.state("set_face_artwork_calibration", calibration=calibration)
            client.state("import_face_artwork_override", path=str(artwork))
            client.state("set_face_artwork_override_alignment", x=0.1, y=0.15, width=0.8, height=0.7, alphaSource="OverrideImage")
            client.state("rebuild_face_artwork")
            face = client.state("save_face")["face"]
            assert not face["isDirty"]
            artwork_state = client.state("inspect_face_artwork")["artwork"]
            assert artwork_state["processingPipeline"]["operations"][0]["strength"] == 65
            assert artwork_state["override"]["alphaSource"] == "OverrideImage"
            client.capture(args.output / "workspace-face-dark.png", "Dark")
            client.capture(args.output / "workspace-face-light.png", "Light")
            reopened = client.state("open_face", path=face["facePath"])["face"]
            assert len(reopened["elements"]) == 3  # imported artwork plus two authored lamp windows
            if args.reel_library:
                mount=client.state("add_face_reel_mount",name="Authored Reel",x=60,y=60,width=150,height=240,reelIndex=0)["face"]["selectedId"]
                client.state("update_face_reel_mount",id=mount,options={"stops":24,"visibleScale":0.8,"bandOffset":0.1,"isReversed":True,"isOpaqueReel":True,"topLampNumber":7})
                client.state("import_face_reel_symbol_band",id=mount,path=str(artwork))
                client.state("generate_face_reel_transmission_mask",id=mount)
                reel=client.state("inspect_face_reel_mount",id=mount)["reelMount"]
                assert reel["stops"]==24 and reel["isOpaqueReel"] and reel["reelLampTransmissionMaskAssetPath"]
                client.state("face_history");client.state("face_history",redo=True);client.state("save_face")
                client.capture(args.output/"workspace-face-reel-dark.png","Dark")
                client.capture(args.output/"workspace-face-reel-light.png","Light")
                print("Face Reel mount settings, real image import, transmission-mask generation, history/save and rendered captures passed.")
            workspace_path = str(root / "editor.workspace.json")
            saved_workspace = client.state("save_workspace", path=workspace_path)
            client.state("reset_layout")
            restored_workspace = client.state("restore_workspace", path=workspace_path)
            assert saved_workspace["project"] == restored_workspace["project"]
            assert restored_workspace["activeDocument"] == "Face"
            client.capture(args.output / "workspace-restored.png", restored_workspace["theme"])
            if args.fml:
                source = root / args.fml.name
                shutil.copyfile(args.fml, source)
                client.state("create_panel2d", name="Imported layout")
                result = client.state("import_fml", path=str(source))
                assert result["result"]["succeeded"], result["result"]["errors"]
                assert result["result"]["importedElementCount"] > 0
                client.state("save_panel2d")
                client.capture(args.output / "workspace-imported.png", "Dark")
                print("Actual FML import:", result["result"]["importedElementCount"], "elements")
        finally:
            client.close()
    if args.project:
        client = Client(dll, args.project.resolve().parent)
        try:
            state = client.state("open_project", path=str(args.project.resolve()))
            panel = str(args.panel.resolve()) if args.panel else next(iter(state["panels"]), None)
            if panel:
                state = client.state("open_panel2d", path=panel)
                if state["elements"]:
                    client.state("select_element", id=state["elements"][-1]["id"])
                client.capture(args.output / "workspace-project.png", "Dark")
                print("Read-only real project capture:", len(state["elements"]), "elements")
            elif not any((args.cabinet, args.prefab, args.scene_machine)):
                raise RuntimeError("The supplied project has no Panel2D assets")
            if args.face:
                state = client.state("open_face", path=str(args.face.resolve()))["face"]
                if state["elements"]:
                    client.state("select_face_element", id=state["elements"][-1]["id"])
                client.capture(args.output / "workspace-existing-face.png", "Dark")
                print("Read-only existing Face capture:", len(state["elements"]), "elements")
            if args.library_root:
                client.state("set_scene_library_root", directory=args.library_root)
            for tool, asset, label in [("open_cabinet3d",args.cabinet,"cabinet"),
                                       ("open_prefab",args.prefab,"prefab"),
                                       ("open_machine",args.scene_machine,"machine")]:
                if not asset:
                    continue
                client.state(tool,path=asset)
                for theme in ("Dark","Light"):
                    client.capture(args.output / f"workspace-{label}-{theme.lower()}.png",theme)
                    scene=client.state("inspect_scene3d")["scene"]
                    assert not scene.get("renderingError"),scene
                    assert scene.get("gpu") and scene["gpu"]["triangles"] > 0,scene
                client.state("set_scene_visibility",colliders=True,triggers=True)
                client.capture(args.output / f"workspace-{label}-colliders.png","Dark")
                client.state("set_scene_visibility",colliders=False,triggers=False)
                print("Read-only GPU scene capture:",label,scene["meshes"],"primitives; diagnostics:",scene["diagnostics"])
        finally:
            client.close()
    print("MCP native project/panel/image/source-region/Face authoring, history, save/reopen and headless PNG captures passed.")


if __name__ == "__main__":
    main()
