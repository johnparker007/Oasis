"""Independent MCP client smoke test. Requires Python 3.10+ and fastmcp 3.x.
All authored content stays in a fresh disposable workspace. Optional FML is copied,
never modified. Screenshots and server stderr are retained in --artifacts.
"""
import argparse
import asyncio
import base64
import json
import os
import shutil
import tempfile
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from fastmcp.exceptions import ToolError


def payload(result):
    texts = [block.text for block in result.content if block.type == "text"]
    return json.loads(texts[0])


async def smoke(args):
    output = Path(args.artifacts).resolve()
    output.mkdir(parents=True, exist_ok=True)
    executable = str(Path(args.server).resolve())
    with tempfile.TemporaryDirectory(prefix="oasis-mcp-", dir=output) as temporary:
        root = Path(temporary)
        # WPF needs Windows shell/font environment variables; the generic Python
        # MCP client's minimal default environment omits some of these.
        transport = StdioTransport(executable, ["--workspace", str(root)], env=dict(os.environ), log_file=output / "server.stderr.log")
        async with Client(transport, timeout=120) as client:
            tools = await client.list_tools()
            assert {"create_project", "capture_panel2d", "update_panel_element", "select_panel_elements", "inspect_panel_element", "set_panel_view"} <= {tool.name for tool in tools}
            try:
                await client.call_tool("create_project", {"name": "../escape"})
                raise AssertionError("Traversal accepted")
            except ToolError:
                pass
            project = payload(await client.call_tool("create_project", {"name": "MCPTest1"}))
            panel = payload(await client.call_tool("create_panel2d", {"name": "Main Panel"}))
            document_id = panel["documentId"]
            assert Path(panel["path"]).is_file()
            if args.fml:
                source = root / "source.fml"
                shutil.copyfile(args.fml, source)
                imported = payload(await client.call_tool("import_fml", {"documentId": document_id, "source": str(source)}))
                assert imported["succeeded"], imported
                count = imported["panel"]["elementCount"]
                assert count > 0, imported
                undone = payload(await client.call_tool("undo", {"documentId": document_id}))
                assert undone["elementCount"] == 0
                redone = payload(await client.call_tool("redo", {"documentId": document_id}))
                assert redone["elementCount"] == count
                print(f"PASS: real FML import, {count} elements, undo and redo", flush=True)
                editable = next((e for e in redone["elements"] if not e["isTransformLocked"] and e["width"] > 0 and e["height"] > 0), None)
                assert editable, "Expected an editable element in the import fixture"
                element_id = editable["id"]
                await client.call_tool("select_panel_elements", {"documentId": document_id, "elementIds": [element_id]})
                selected = payload(await client.call_tool("inspect_panel_element", {"documentId": document_id, "elementId": element_id}))
                assert selected["isSelected"]
                edit = {"name": "MCP verified element", "x": editable["x"] + 3, "width": editable["width"] + 2, "isVisible": True}
                changed = payload(await client.call_tool("update_panel_element", {"documentId": document_id, "elementId": element_id, "edit": edit}))
                assert changed["element"]["name"] == edit["name"] and changed["element"]["x"] == edit["x"]
                await client.call_tool("undo", {"documentId": document_id})
                original = payload(await client.call_tool("inspect_panel_element", {"documentId": document_id, "elementId": element_id}))
                assert original["name"] == editable["name"] and original["x"] == editable["x"]
                await client.call_tool("redo", {"documentId": document_id})
                print("PASS: element selection, atomic property edit, undo and redo", flush=True)
            else:
                count = 0
            saved = payload(await client.call_tool("save_panel2d", {"documentId": document_id}))
            assert not saved["isDirty"]
            if args.fml:
                no_op = payload(await client.call_tool("update_panel_element", {"documentId": document_id, "elementId": element_id, "edit": {"name": edit["name"]}}))
                assert not no_op["panel"]["isDirty"]
                try:
                    await client.call_tool("update_panel_element", {"documentId": document_id, "elementId": element_id, "edit": {"name": "Invalid partial edit", "width": 0}})
                    raise AssertionError("Invalid dimensions accepted")
                except ToolError:
                    pass
                unchanged = payload(await client.call_tool("inspect_panel_element", {"documentId": document_id, "elementId": element_id}))
                assert unchanged["name"] == edit["name"]
            await client.call_tool("set_panel_view", {"documentId": document_id, "zoom": 0.75, "panX": 6, "panY": -8})
            inspected = payload(await client.call_tool("inspect_panel2d", {"documentId": document_id, "limit": 1}))
            assert inspected["elementCount"] == count
            for theme in ("Light", "Dark", "System"):
                capture = await client.call_tool("capture_panel2d", {"documentId": document_id, "theme": theme, "width": 800, "height": 600, "framing": "fit"})
                image = next(block for block in capture.content if block.type == "image")
                png = base64.b64decode(image.data)
                assert png.startswith(b"\x89PNG\r\n\x1a\n") and len(png) > 1000
                (output / f"panel-{theme.lower()}.png").write_bytes(png)
            if args.fml:
                target = await client.call_tool("capture_panel2d", {"documentId": document_id, "width": 800, "height": 600, "framing": "element", "elementId": element_id})
                (output / "panel-element.png").write_bytes(base64.b64decode(next(b for b in target.content if b.type == "image").data))
            after_capture = payload(await client.call_tool("inspect_panel2d", {"documentId": document_id, "limit": 1}))
            assert after_capture["view"] == {"zoom": 0.75, "panX": 6, "panY": -8}
            assert not after_capture["isDirty"]
            invalid = root / "broken.fml"
            invalid.write_bytes(b"invalid fml")
            failure = payload(await client.call_tool("import_fml", {"documentId": document_id, "source": str(invalid)}))
            assert not failure["succeeded"] and failure["errors"]
            assert failure["panel"]["elementCount"] == count
        # A fresh MCP process must reopen persisted data with new document identities.
        async with Client(StdioTransport(executable, ["--workspace", str(root)], env=dict(os.environ), log_file=output / "reopen.stderr.log"), timeout=60) as client:
            await client.call_tool("open_project", {"path": project["projectFile"]})
            reopened = payload(await client.call_tool("open_panel2d", {"path": saved["path"]}))
            assert reopened["elementCount"] == count
            assert reopened["documentId"] != document_id
            assert not reopened["isDirty"]
            if args.fml:
                persisted = payload(await client.call_tool("inspect_panel_element", {"documentId": reopened["documentId"], "elementId": element_id}))
                assert persisted["name"] == edit["name"] and persisted["width"] == edit["width"]
        print("PASS: discovery, path/invalid-edit rejection, create/save/inspection, fit/element captures, transient view restoration, failed import and edited-property reopen", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--artifacts", required=True)
    parser.add_argument("--fml", help="Optional existing real FML to exercise successful import/undo/redo")
    asyncio.run(smoke(parser.parse_args()))
