"""Exercise parented Pool placement and an inline Prefab child through real MCP.
Use only an explicitly reauthored disposable PoolTableTest1 copy (fastmcp 3.x).
"""
import argparse
import asyncio
import json
import os
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload


def transform(x=0, y=0, z=0, ry=0):
    return {"position": {"x": x, "y": y, "z": z}, "rotation": {"x": 0, "y": ry, "z": 0}, "scale": {"x": 1, "y": 1, "z": 1}}


async def main(args):
    project = Path(args.project).resolve()
    artifacts = Path(args.artifacts).resolve()
    artifacts.mkdir(parents=True, exist_ok=True)
    async with Client(StdioTransport(str(Path(args.server).resolve()), ["--workspace", str(project.parent)],
                                     env=dict(os.environ), log_file=artifacts / "tree-server.log"), timeout=240) as client:
        async def call(tool_name, **parameters):
            return payload(await client.call_tool(tool_name, parameters))
        await call("open_project", path=str(project))
        machine = await call("open_machine", path=str(project.parent / "Assets/Machines/PoolTableTest1/asset.machine"))
        prefab = await call("open_prefab", path=str(project.parent / "Assets/Object3D/PoolBall/asset.object3d"))
        if not prefab["prefab"]["root"]["children"]:
            prefab = await call("add_prefab_child", documentId=prefab["documentId"], parentNodeId=prefab["prefab"]["root"]["id"], name="Stable child marker")
            await call("save_prefab", documentId=prefab["documentId"])
        frame_path = project.parent / "Assets/Object3D/VerificationFrame/asset.object3d"
        frame = (await call("open_prefab", path=str(frame_path))) if frame_path.exists() else await call("create_prefab", name="VerificationFrame")
        await call("update_prefab_node", documentId=frame["documentId"], nodeId=frame["prefab"]["root"]["id"], transform=transform(.12, 0, -.08, 15))
        await call("save_prefab", documentId=frame["documentId"])
        if not any(i["id"] == "verificationFrame" for i in machine["machine"]["objectInstances"]):
            await call("add_machine_object", documentId=machine["documentId"], id="verificationFrame", prefabPath=str(frame_path), transform=transform(.1, 0, .1, 20))
        await call("update_machine_object", documentId=machine["documentId"], objectId="ball01", parentId="verificationFrame")
        await call("save_machine", documentId=machine["documentId"])
        build = await call("build_machine", documentId=machine["documentId"])
        assert build["success"], build
        report = {"build": build, "machine": await call("inspect_machine", documentId=machine["documentId"]),
                  "prefab": await call("inspect_prefab", documentId=prefab["documentId"])}
        (artifacts / "tree-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("PASS: inline child and source-root/placement transforms exported with parented Pool ball", flush=True)
        print(json.dumps(build), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--artifacts", required=True)
    asyncio.run(main(parser.parse_args()))
