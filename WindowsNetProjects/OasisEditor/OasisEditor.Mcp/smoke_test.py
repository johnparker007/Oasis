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
            assert len(tools) == 11, [tool.name for tool in tools]
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
            else:
                count = 0
            saved = payload(await client.call_tool("save_panel2d", {"documentId": document_id}))
            assert not saved["isDirty"]
            inspected = payload(await client.call_tool("inspect_panel2d", {"documentId": document_id, "limit": 1}))
            assert inspected["elementCount"] == count
            for theme in ("Light", "Dark", "System"):
                capture = await client.call_tool("capture_panel2d", {"documentId": document_id, "theme": theme, "width": 800, "height": 600})
                image = next(block for block in capture.content if block.type == "image")
                png = base64.b64decode(image.data)
                assert png.startswith(b"\x89PNG\r\n\x1a\n") and len(png) > 1000
                (output / f"panel-{theme.lower()}.png").write_bytes(png)
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
        print("PASS: discovery, workspace rejection, create/save, bounded inspection, 3 WPF captures, failed import, reopen in fresh process", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--artifacts", required=True)
    parser.add_argument("--fml", help="Optional existing real FML to exercise successful import/undo/redo")
    asyncio.run(smoke(parser.parse_args()))
