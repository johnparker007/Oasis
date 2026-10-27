"""Background verification for Output, project asset context actions, Face Build and diagnostics.

The script uses only a disposable project hosted by the real headless stdio MCP server.
It may write PNG evidence to --output, but it never opens a desktop window or accesses
private user projects/assets. Build the Avalonia MCP host before running it.
"""
import argparse
import base64
import json
from pathlib import Path
import struct
import tempfile
import zlib

from verify_mcp import Client


def png_rgba(width, height, pixel):
    """Create a small real PNG fixture without third-party Python packages."""
    def chunk(kind, data):
        return (struct.pack(">I", len(data)) + kind + data
                + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF))

    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            rows.extend(pixel(x, y))
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(rows))) + chunk(b"IEND", b""))


def require_error(client, name, **arguments):
    result = client.request("tools/call", {"name": name, "arguments": arguments})
    assert result.get("isError"), f"{name} unexpectedly succeeded: {result}"
    return "\n".join(block.get("text", "") for block in result.get("content", []))


def save_tool_image(client, name, path, **arguments):
    result = client.tool(name, **arguments)
    image = next(block for block in result["content"] if block["type"] == "image")
    data = base64.b64decode(image["data"])
    assert data.startswith(b"\x89PNG\r\n\x1a\n"), name
    path.write_bytes(data)


def close_reel(client, path):
    client.state("save_reel")
    client.state("close_document", kind="Reel", path=path)


def verify_asset_package_actions(client, project_root):
    # Successful rename proves the package manifest and contained dependencies survive byte-for-byte.
    created = client.state("create_reel", name="Rename Fixture")
    old_path = Path(created["path"])
    reel_id = created["document"]["id"]
    client.state("update_reel", diameterMm=287, widthMm=69)
    close_reel(client, str(old_path))
    dependency = old_path.parent / "dependencies" / "fixture.txt"
    dependency.parent.mkdir(parents=True)
    dependency.write_text("dependency stays beside its package", encoding="utf-8")
    old_manifest = old_path.read_bytes()
    fingerprint = client.state("inspect_project_asset_package", manifestPath=str(old_path))["fingerprint"]
    renamed = client.state("rename_project_asset_package", manifestPath=str(old_path), packageName="Renamed Fixture",
                           expectedFingerprint=fingerprint)
    new_path = Path(renamed["manifestPath"])
    assert new_path.is_file() and not old_path.exists()
    assert new_path.read_bytes() == old_manifest
    assert (new_path.parent / "dependencies" / "fixture.txt").read_text(encoding="utf-8") == "dependency stays beside its package"
    native = json.loads(new_path.read_text(encoding="utf-8"))
    assert native["id"] == reel_id and native["diameterMm"] == 287 and native["widthMm"] == 69

    # A changed package must reject the fingerprint captured before the change.
    stale = client.state("create_reel", name="Stale Fixture")
    stale_path = Path(stale["path"])
    close_reel(client, str(stale_path))
    stale_fingerprint = client.state("inspect_project_asset_package", manifestPath=str(stale_path))["fingerprint"]
    (stale_path.parent / "outside-edit.txt").write_text("independent edit", encoding="utf-8")
    message = require_error(client, "rename_project_asset_package", manifestPath=str(stale_path),
                            packageName="Should Not Rename", expectedFingerprint=stale_fingerprint)
    assert stale_path.is_file(), message

    # Serialized project-relative references block both rename and delete.
    referenced = client.state("create_reel", name="Referenced Fixture")
    referenced_path = Path(referenced["path"])
    close_reel(client, str(referenced_path))
    machines = client.state("list_machines")["machines"]
    machine_path = machines[0]["manifestPath"]
    client.state("open_machine", path=machine_path)
    relative_reel = referenced_path.relative_to(project_root).as_posix()
    client.state("assign_machine_reel", reelReference="reel:0", assetPath=relative_reel, scope="Project")
    client.state("save_machine")
    client.state("close_document", kind="Machine", path=machine_path)
    referenced_fingerprint = client.state("inspect_project_asset_package", manifestPath=str(referenced_path))["fingerprint"]
    blocked_rename = require_error(client, "rename_project_asset_package", manifestPath=str(referenced_path),
                                   packageName="Referenced Rename", expectedFingerprint=referenced_fingerprint)
    blocked_delete = require_error(client, "delete_project_asset_package", manifestPath=str(referenced_path), confirm=True,
                                   expectedFingerprint=referenced_fingerprint)
    assert referenced_path.is_file()

    # Deletion is explicitly confirmed and is a recoverable move, with native IDs intact.
    trash_fixture = client.state("create_reel", name="Trash Fixture")
    trash_path = Path(trash_fixture["path"])
    trash_id = trash_fixture["document"]["id"]
    close_reel(client, str(trash_path))
    trash_fingerprint = client.state("inspect_project_asset_package", manifestPath=str(trash_path))["fingerprint"]
    require_error(client, "delete_project_asset_package", manifestPath=str(trash_path), confirm=False,
                  expectedFingerprint=trash_fingerprint)
    assert trash_path.is_file()
    deleted = client.state("delete_project_asset_package", manifestPath=str(trash_path), confirm=True,
                           expectedFingerprint=trash_fingerprint)
    recovery = Path(deleted["recoveryPath"])
    recovered_manifest = recovery / trash_path.name
    assert recovered_manifest.is_file() and not trash_path.exists()
    assert json.loads(recovered_manifest.read_text(encoding="utf-8"))["id"] == trash_id
    assert str(trash_path) not in [asset["manifestPath"] for asset in client.state()["assets"]]


def verify_output(client, output):
    snapshot = client.state("inspect_output")
    assert snapshot["entries"], "Workspace status events did not reach Output"
    filtered = client.state("filter_output", searchText="Reel", showInfo=True, showWarnings=True, showErrors=True)
    assert filtered["filteredEntries"] and all("Reel" in row["message"] for row in filtered["filteredEntries"]), filtered
    shown = client.state("show_output")
    assert shown["filteredEntries"]
    for theme in ("Dark", "Light"):
        client.capture(output / f"port-gap-output-{theme.lower()}.png", theme)


def verify_input_diagnostics(client):
    machine_path = client.state("list_machines")["machines"][0]["manifestPath"]
    client.state("open_machine", path=machine_path)
    inputs = [
        {"id": "start", "name": "Start", "kind": "Button", "keyboardShortcut": "Space"},
        {"id": "hold", "name": "Hold", "kind": "Button", "keyboardShortcut": "space"},
        {"id": "service", "name": "Service", "kind": "Button", "keyboardShortcut": "F2"},
    ]
    client.state("set_machine_inputs", inputs=inputs)
    diagnostics = client.state("inspect_machine_input_diagnostics")["diagnostics"]
    duplicates = [item for item in diagnostics if item["code"] == "input.duplicate_shortcut"]
    assert len(duplicates) == 2, diagnostics
    assert any("space" in str(item).lower() for item in duplicates), duplicates
    client.state("save_machine")
    client.state("close_document", kind="Machine", path=machine_path)


def verify_face_build(client, root, output):
    face = client.state("create_face", name="Runtime Fixture")["face"]
    face_path = Path(face["facePath"])
    face_id = face.get("document", {}).get("id")
    artwork = root / "face-artwork.png"
    mask = root / "lamp-mask.png"
    artwork.write_bytes(png_rgba(128, 96, lambda x, y: (25 + x, 45 + y, 100 + x // 2, 255)))
    mask.write_bytes(png_rgba(128, 96, lambda x, y: (255, 255, 255, 255) if 20 <= x < 108 and 18 <= y < 78 else (0, 0, 0, 255)))

    imported = client.state("import_face_artwork", path=str(artwork))
    assert imported["face"]["artworkPath"]
    client.state("import_face_lamp_mask", path=str(mask))
    client.state("add_face_lamp", name="Start lamp",x=20,y=18,width=32,height=32,lampIndex=3)
    client.state("set_face_artwork_calibration", calibration={"strength":65,
        "blackReference":{"manualEnabled":True,"manualColor":"#FF000000","samples":[{"id":"black1","x":.1,"y":.15,"samplingMode":"Area","radiusNormalized":.03}]},
        "whiteReference":{"manualEnabled":True,"manualColor":"#FFFFFFFF","samples":[{"id":"white1","x":.9,"y":.8,"samplingMode":"Pixel"}]},
        "sameColorGroups":[{"id":"group1","name":"Artwork background","samples":[{"id":"same1","x":.3,"y":.4,"samplingMode":"Area","radiusNormalized":.02},{"id":"same2","x":.6,"y":.7,"samplingMode":"Area","radiusNormalized":.02}]}]})
    client.state("save_face")
    for theme in ("Dark", "Light"):
        save_tool_image(client, "capture_face_artwork_editor", output / f"port-gap-face-artwork-{theme.lower()}.png",
                        calibrationStage=False, theme=theme)
        save_tool_image(client, "capture_face_artwork_editor", output / f"port-gap-face-calibration-{theme.lower()}.png", calibrationStage=True, theme=theme)

    built = client.state("build_face", runtimeAssets=True)
    assert built["face"]["facePath"] == str(face_path)
    build = client.state("inspect_face_build")
    runtime = build["runtimeRenderAssets"]
    assert runtime and runtime["manifestPath"]
    project_root = face_path.parents[3]
    runtime_manifest = project_root / runtime["manifestPath"]
    assert runtime_manifest.is_file(), runtime_manifest
    manifest = json.loads(runtime_manifest.read_text(encoding="utf-8"))
    if face_id is None:
        native_face = json.loads(face_path.read_text(encoding="utf-8"))
        face_id = native_face["Id"]
    assert manifest["faceId"] == face_id, manifest
    assert manifest["schemaVersion"] == 10, manifest
    for key in ("artworkPath", "maskPath", "trayIdPath", "lampIds0Path", "lampWeights0Path"):
        path = runtime.get(key)
        assert path and (project_root / path).is_file(), (key, path)
    mask_layer = build["maskLayer"]
    assert mask_layer and (project_root / mask_layer["assetPath"]).is_file(), mask_layer
    assert (project_root / mask_layer["authoredAssetPath"]).is_file(), mask_layer

    # A missing opaque-reel transmission source fails late in runtime export. All existing
    # Generated/Faces outputs and the saved native Face manifest must be restored unchanged.
    generated_face = project_root / "Generated" / "Faces" / "Runtime Fixture"
    before_generated = {path.relative_to(generated_face).as_posix(): path.read_bytes()
                        for path in generated_face.rglob("*") if path.is_file()}
    before_native = face_path.read_bytes()
    mount = client.state("add_face_reel_mount", name="Missing transmission", x=12, y=12, width=40, height=60,
                         reelIndex=0)["face"]["selectedId"]
    dependency = project_root / "Assets" / "TestDependencies" / "transmission.png"
    dependency.parent.mkdir(parents=True, exist_ok=True)
    dependency.write_bytes(mask.read_bytes())
    missing_path = "Assets/TestDependencies/transmission.png"
    client.state("update_face_reel_mount", id=mount, options={"isOpaqueReel": True, "transmissionMaskAssetPath": missing_path})
    dependency.unlink()
    failure = require_error(client, "build_face", runtimeAssets=True)
    after_generated = {path.relative_to(generated_face).as_posix(): path.read_bytes()
                       for path in generated_face.rglob("*") if path.is_file()}
    assert after_generated == before_generated, "Failed Face runtime build changed Generated/Faces outputs"
    assert face_path.read_bytes() == before_native, "Failed Face runtime build changed the authored manifest"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path, help="Directory for disposable PNG evidence")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    dll = Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll"
    with tempfile.TemporaryDirectory(prefix="OasisPortGapMcp-") as temporary:
        root = Path(temporary).resolve()
        client = Client(dll, root)
        try:
            names = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            expected = {
                "inspect_output", "filter_output", "show_output", "capture_workspace",
                "inspect_project_asset_package", "rename_project_asset_package", "delete_project_asset_package",
                "inspect_machine_input_diagnostics", "import_face_lamp_mask", "build_face", "inspect_face_build",
                "capture_face_artwork_editor", "create_reel", "close_document", "assign_machine_reel",
            }
            assert expected <= names, f"Missing MCP tools: {sorted(expected - names)}"
            client.state("configure_recent_projects", path=str(root / "recent-projects.json"))
            for theme in ("Dark", "Light"):
                client.capture(output / f"port-gap-welcome-{theme.lower()}.png", theme)
            created = client.state("create_project", name="Port gap workflows")
            project_root = Path(created["projectFilePath"]).parent
            verify_asset_package_actions(client, project_root)
            verify_face_build(client, root, output)
            verify_input_diagnostics(client)
            verify_output(client, output)
            print("Port-gap workflows passed: Output filters and captures, guarded package rename/recoverable delete, native Face artwork/mask runtime build and rollback, and duplicate-shortcut diagnostics.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
