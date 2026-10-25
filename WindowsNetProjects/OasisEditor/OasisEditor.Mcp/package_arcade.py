"""Package explicit current-format Machine builds into a new hand-authored arcade.
No authoring assets or ROM directories are scanned. Player validates actual geometry.
"""
import argparse
import json
import math
from pathlib import Path
import re
import shutil


def package(args):
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError("Output already exists; choose a new package folder.")
    if not 1 <= len(args.machine) <= 32:
        raise ValueError("Specify 1–32 explicit Machine placements.")
    placements, sources, ids = [], {}, set()
    for machine_id, source, x, z, yaw in args.machine:
        if not re.fullmatch(r"[A-Za-z0-9_-]{1,64}", machine_id) or machine_id in ids:
            raise ValueError("Placement IDs must be valid and unique.")
        ids.add(machine_id)
        position = dict(x=float(x), y=0, z=float(z))
        rotation = float(yaw)
        if not all(math.isfinite(value) for value in [*position.values(), rotation]):
            raise ValueError("Placement poses must be finite.")
        build = Path(source).resolve()
        if output.is_relative_to(build):
            raise ValueError("Package output must be outside every source Machine build.")
        manifest = json.loads((build / "machine.runtime.json").read_text(encoding="utf-8-sig"))
        if manifest.get("schema") != "oasis.machine.runtime" or manifest.get("schemaVersion") != 13 or not manifest.get("interaction"):
            raise ValueError(f"{machine_id}: rebuild with the current Editor (Machine runtime 13).")
        key = str(build).casefold()
        if key not in sources:
            sources[key] = (build, f"machines/{machine_id}")
        placements.append(dict(id=machine_id, build=sources[key][1], position=position, yaw=rotation))
    layout = dict(schema="oasis.arcade.runtime", schemaVersion=1, width=args.width, length=args.length, height=4,
                  visitorSpawn=dict(x=0, y=0, z=args.visitor_z), visitorYaw=0, machines=placements)
    if not all(math.isfinite(v) for v in [args.width, args.length, args.visitor_z]) or not 4 <= args.width <= 100 or not 4 <= args.length <= 100:
        raise ValueError("Room width and length must be finite and between 4 and 100 metres.")
    output.mkdir(parents=True, exist_ok=False)
    for build, relative in sources.values():
        shutil.copytree(build, output / relative)
    path = output / "arcade.runtime.json"
    path.write_text(json.dumps(layout, indent=2) + "\n", encoding="utf-8")
    print(path, flush=True)
    print("Packaged explicit dependencies; Player still validates actual Cabinet clearance.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--machine", nargs=5, action="append", required=True, metavar=("ID", "BUILD", "X", "Z", "YAW"))
    parser.add_argument("--width", type=float, default=12)
    parser.add_argument("--length", type=float, default=10)
    parser.add_argument("--visitor-z", type=float, default=-3)
    package(parser.parse_args())
