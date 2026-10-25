"""Build an explicit saved Machine through a workspace-confined Editor MCP session."""
import argparse
import asyncio
import json
import os
from pathlib import Path
import tempfile
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload

async def main(args):
    project = Path(args.project).resolve()
    with tempfile.TemporaryDirectory(prefix="OasisMachineExport-", ignore_cleanup_errors=True) as scratch:
        async with Client(StdioTransport(str(Path(args.server).resolve()), ["--workspace", str(project.parent)], env=dict(os.environ), log_file=Path(scratch)/"server.log"), timeout=240) as client:
            async def call(tool, **parameters): return payload(await client.call_tool(tool, parameters))
            await call("open_project", path=str(project))
            machine = await call("open_machine", path=str(Path(args.machine).resolve()))
            build = await call("build_machine", documentId=machine["documentId"])
            if not build["success"]: raise RuntimeError(json.dumps(build))
            if args.output: Path(args.output).write_text(json.dumps(build, indent=2), encoding="utf-8")
            print(json.dumps(build), flush=True)

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    for name in ["server", "project", "machine"]: parser.add_argument("--"+name, required=True)
    parser.add_argument("--output")
    asyncio.run(main(parser.parse_args()))
