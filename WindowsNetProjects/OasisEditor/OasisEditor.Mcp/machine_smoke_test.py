"""Inspect/capture/build an existing project via MCP. Use a disposable project copy.
Requires fastmcp 3.x. No authored files are modified; build replaces Generated output.
"""
import argparse
import asyncio
import base64
import json
import os
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload

async def main(args):
    project = Path(args.project).resolve()
    output = Path(args.artifacts).resolve(); output.mkdir(parents=True, exist_ok=True)
    report = {}
    async with Client(StdioTransport(str(Path(args.server).resolve()), ["--workspace", str(project.parent)],
                                    env=dict(os.environ), log_file=output / "server.log"), timeout=240) as client:
        report["recentProjects"] = payload(await client.call_tool("list_recent_projects", {}))
        await client.call_tool("open_project", {"path": str(project)})
        machines = payload(await client.call_tool("list_machine_assets", {}))
        if len(machines) != 1:
            raise RuntimeError(f"Expected one machine, found {len(machines)}; choose an explicit machine before automation")
        report["machine"] = payload(await client.call_tool("open_machine", {"path": machines[0]}))
        (output / "machine-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        document = report["machine"]["documentId"]
        if not args.no_capture:
            capture = await client.call_tool("capture_machine", {"documentId": document})
            image = next(b for b in capture.content if b.type == "image")
            (output / "machine.png").write_bytes(base64.b64decode(image.data))
            print("PASS: captured real Editor Machine Composition view", flush=True)
        report["build"] = payload(await client.call_tool("build_machine", {"documentId": document}))
        (output / "machine-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report["build"]), flush=True)
        if not report["build"]["success"]:
            raise RuntimeError("Machine build failed; see machine-report.json")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True); parser.add_argument("--project", required=True)
    parser.add_argument("--artifacts", required=True); parser.add_argument("--no-capture", action="store_true")
    asyncio.run(main(parser.parse_args()))
