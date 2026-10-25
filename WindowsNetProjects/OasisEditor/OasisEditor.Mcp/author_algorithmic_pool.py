"""Author the calculated Pool example into an explicit disposable project via MCP.
The project must already use the current Machine and Prefab schemas. fastmcp 3.x.
"""
import argparse
import asyncio
import json
import os
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload


async def main(args):
    project = Path(args.project).resolve()
    output = Path(args.artifacts).resolve()
    output.mkdir(parents=True, exist_ok=True)
    source = Path(args.source).resolve().read_text(encoding="utf-8-sig")
    async with Client(StdioTransport(str(Path(args.server).resolve()), ["--workspace", str(project.parent)],
                                     env=dict(os.environ), log_file=output / "author-server.log"), timeout=240) as client:
        async def call(tool, **parameters):
            return payload(await client.call_tool(tool, parameters))
        await call("open_project", path=str(project))
        opened = await call("open_machine", path=str(project.parent / "Assets/Machines/PoolTableTest1/asset.machine"))
        document_id = opened["documentId"]
        # This proving case is entirely spawned; no authored balls/frames are needed.
        pending = {item["id"] for item in opened["machine"]["objectInstances"]}
        while pending:
            object_id = sorted(pending)[0]
            updated = await call("remove_machine_object", documentId=document_id, objectId=object_id)
            pending = {item["id"] for item in updated["machine"]["objectInstances"]}
        prefab = str(project.parent / "Assets/Object3D/PoolBall/asset.object3d")
        for alias in ["poolBall", "cueBall"]:
            await call("declare_machine_prefab", documentId=document_id, alias=alias, prefabPath=prefab)
        for anchor_id, z in [("rackOrigin", .15), ("collectionTray", -.65), ("cueStart", -.35)]:
            await call("set_machine_anchor", documentId=document_id,
                       anchor={"id": anchor_id, "displayName": anchor_id,
                               "position": {"x": 0, "y": .80154955, "z": z}, "rotation": {"x": 0, "y": 0, "z": 0}})
        await call("set_machine_geometry_surface", documentId=document_id,
                   surface={"id": "playingSurface", "position": {"x": 0, "y": .80154955, "z": 0},
                            "rotation": {"x": 0, "y": 0, "z": 0}, "width": .778, "length": 1.58})
        behavior = await call("set_machine_behavior", documentId=document_id, source=source)
        if behavior["behaviorDiagnostics"]:
            raise RuntimeError(json.dumps(behavior["behaviorDiagnostics"], indent=2))
        saved = await call("save_machine", documentId=document_id)
        build = await call("build_machine", documentId=document_id)
        (output / "author-report.json").write_text(json.dumps({"machine": saved, "build": build}, indent=2), encoding="utf-8")
        assert build["success"], build
        generated = json.loads((Path(build["buildRoot"]) / "machine.runtime.json").read_text(encoding="utf-8-sig"))
        assert not generated["objectInstances"]
        assert len(generated["prefabDependencies"]) == 2
        print("PASS: zero authored balls, declared spawn-only Prefab dependencies, measured frames/surface and calculated script exported", flush=True)
        print(json.dumps(build), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    parser.add_argument("--project", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--artifacts", required=True)
    asyncio.run(main(parser.parse_args()))
