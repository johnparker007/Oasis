"""Verify current Prefab authoring and the real single-ball Pool copy through MCP.

Requires fastmcp 3.x and an explicitly reauthored disposable PoolTableTest1 project.
Only the disposable project's assets/Generated output are changed.
"""
import argparse
import asyncio
import base64
import hashlib
import json
import os
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload


def fingerprints(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob("*") if p.is_file()}


async def main(args):
    project = Path(args.project).resolve()
    artifacts = Path(args.artifacts).resolve()
    artifacts.mkdir(parents=True, exist_ok=True)
    async with Client(StdioTransport(str(Path(args.server).resolve()), ["--workspace", str(project.parent)],
                                     env=dict(os.environ), log_file=artifacts / "server.log"), timeout=240) as client:
        async def call(tool_name, **parameters):
            return payload(await client.call_tool(tool_name, parameters))
        await call("open_project", path=str(project))
        machine = await call("open_machine", path=str(project.parent / "Assets/Machines/PoolTableTest1/asset.machine"))
        machine_id = machine["documentId"]
        for anchor_id, position in [("rackBall01", {"x": 0, "y": .827, "z": .25}),
                                    ("traySlot01", {"x": .25, "y": .827, "z": -.55})]:
            await call("update_machine_anchor", documentId=machine_id, anchorId=anchor_id, position=position)
        machine = await call("save_machine", documentId=machine_id)
        assert not machine["isDirty"]
        prefab = await call("open_prefab", path=str(project.parent / "Assets/Object3D/PoolBall/asset.object3d"))
        prefab_id = prefab["documentId"]
        assert prefab["canBuildStructure"]
        build = await call("build_machine", documentId=machine_id)
        assert build["success"], build
        before_id = prefab["prefab"]["root"]["id"]
        child = await call("add_prefab_child", documentId=prefab_id, parentNodeId=before_id, name="Proof marker")
        assert len(child["prefab"]["root"]["children"]) == 1
        assert child["canBuildStructure"]
        await call("save_prefab", documentId=prefab_id)
        tree_build = await call("build_machine", documentId=machine_id)
        assert tree_build["success"], tree_build
        definition = next(Path(tree_build["buildRoot"]).rglob("object.runtime.json"))
        runtime_tree = json.loads(definition.read_text(encoding="utf-8-sig"))["root"]
        assert runtime_tree["children"][0]["id"] == child["prefab"]["root"]["children"][0]["id"]
        restored = await call("prefab_history", documentId=prefab_id)
        assert restored["prefab"]["root"]["id"] == before_id
        assert not restored["prefab"]["root"]["children"]
        await call("save_prefab", documentId=prefab_id)
        build = await call("build_machine", documentId=machine_id)
        assert build["success"], build
        for theme in ["System", "Light", "Dark"]:
            result = await client.call_tool("capture_prefab", {"documentId": prefab_id, "theme": theme})
            image = next(block for block in result.content if block.type == "image")
            (artifacts / f"prefab-{theme.lower()}.png").write_bytes(base64.b64decode(image.data))
        result = await client.call_tool("capture_machine", {"documentId": machine_id})
        image = next(block for block in result.content if block.type == "image")
        (artifacts / "machine.png").write_bytes(base64.b64decode(image.data))
        (artifacts / "report.json").write_text(json.dumps({"machine": machine, "prefab": restored, "build": build}, indent=2), encoding="utf-8")
        print("PASS: command-backed anchor fix, prefab identity/hierarchy/undo/save, runtime component tree export, three-theme captures and actual Pool build", flush=True)
        print(json.dumps(build), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--artifacts", required=True)
    asyncio.run(main(parser.parse_args()))
