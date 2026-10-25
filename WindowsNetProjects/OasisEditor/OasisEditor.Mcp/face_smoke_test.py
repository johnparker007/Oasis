"""Real MCP Face-authoring smoke; optional FML is copied into a disposable workspace.
Requires fastmcp 3.x. Retains only diagnostic logs and PNGs in --artifacts.
"""
import argparse
import asyncio
import base64
import os
from pathlib import Path
import shutil
import tempfile
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from fastmcp.exceptions import ToolError
from smoke_test import payload


async def smoke(args):
    output = Path(args.artifacts).resolve(); output.mkdir(parents=True, exist_ok=True)
    server = str(Path(args.server).resolve())
    with tempfile.TemporaryDirectory(prefix="oasis-face-", dir=output) as workspace:
        root = Path(workspace)
        shutil.copyfile(args.fml, root / "source.fml")
        def transport(log):
            return StdioTransport(server, ["--workspace", str(root)], env=dict(os.environ), log_file=output / log)
        async with Client(transport("face-server.stderr.log"), timeout=180) as client:
            names = {tool.name for tool in await client.list_tools()}
            assert {"create_face", "generate_face_from_panel", "add_face_source_shape", "capture_face", "open_face"} <= names
            project = payload(await client.call_tool("create_project", {"name": "FaceMCPTest"}))
            panel = payload(await client.call_tool("create_panel2d", {"name": "Source Panel"}))
            panel_id = panel["documentId"]
            imported = payload(await client.call_tool("import_fml", {"documentId": panel_id, "source": "source.fml"}))
            assert imported["succeeded"], imported
            shape = payload(await client.call_tool("add_face_source_shape", {"panelDocumentId": panel_id, "name": "Full Face"}))
            try:
                await client.call_tool("generate_face_from_panel", {"panelDocumentId": panel_id, "shapeId": shape["id"], "name": "UnsavedSource"})
                raise AssertionError("Dirty source accepted")
            except ToolError:
                pass
            await client.call_tool("undo", {"documentId": panel_id})
            assert payload(await client.call_tool("inspect_face_source_shapes", {"panelDocumentId": panel_id})) == []
            await client.call_tool("redo", {"documentId": panel_id})
            await client.call_tool("save_panel2d", {"documentId": panel_id})
            generated = payload(await client.call_tool("generate_face_from_panel", {"panelDocumentId": panel_id, "shapeId": shape["id"], "name": "Generated Face"}))
            face = generated["face"]; face_id = face["documentId"]
            assert face["isDirty"] and face["sourceShapeId"] == shape["id"]
            assert face["sourcePanelPath"] == panel["path"]
            assert generated["lampCount"] + generated["reelMountCount"] + generated["sevenSegmentCount"] + generated["alphaCount"] > 0, generated
            print(f"PASS: source-shape undo/redo and generation: {face['elementCount']} Face elements, {generated['lampCount']} lamps, {generated['reelMountCount']} reels", flush=True)
            saved = payload(await client.call_tool("save_face", {"documentId": face_id}))
            assert not saved["isDirty"] and Path(saved["path"]).is_file()
            artwork = Path(project["projectFile"]).parent / saved["artworkPath"]
            assert artwork.is_file() and artwork.read_bytes().startswith(b"\x89PNG")
            for theme in ("Light", "Dark", "System"):
                result = await client.call_tool("capture_face", {"documentId": face_id, "theme": theme, "width": 800, "height": 600})
                image = next(block for block in result.content if block.type == "image")
                png = base64.b64decode(image.data)
                assert png.startswith(b"\x89PNG") and len(png) > 1000
                (output / f"face-{theme.lower()}.png").write_bytes(png)
            inspected = payload(await client.call_tool("inspect_face", {"documentId": face_id, "limit": 1}))
            assert not inspected["isDirty"] and len(inspected["elements"]) <= 1
            blank = payload(await client.call_tool("create_face", {"name": "Blank Face"}))
            image_face = payload(await client.call_tool("create_face", {"name": "Image Face", "sourceImage": str(artwork)}))
            assert not blank["isDirty"] and not image_face["isDirty"]
            try:
                await client.call_tool("create_face", {"name": "Blank Face"})
                raise AssertionError("Existing Face overwritten")
            except ToolError:
                pass
        async with Client(transport("face-reopen.stderr.log"), timeout=180) as client:
            await client.call_tool("open_project", {"path": project["projectFile"]})
            reopened = payload(await client.call_tool("open_face", {"path": saved["path"]}))
            assert reopened["assetId"] == saved["assetId"] and reopened["documentId"] != face_id
            assert reopened["elementCount"] == saved["elementCount"] and reopened["sourceShapeId"] == shape["id"]
            assert not reopened["isDirty"]
            Path(saved["path"]).write_text(Path(saved["path"]).read_text(encoding="utf-8") + " ", encoding="utf-8")
            try:
                await client.call_tool("save_face", {"documentId": reopened["documentId"]})
                raise AssertionError("External package modification overwritten")
            except ToolError:
                pass
            print("PASS: blank/image Face creation, authored PNG save, bounded inspection, 3-theme captures, fresh-process reopen and conflict refusal", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True); parser.add_argument("--artifacts", required=True); parser.add_argument("--fml", required=True)
    asyncio.run(smoke(parser.parse_args()))
