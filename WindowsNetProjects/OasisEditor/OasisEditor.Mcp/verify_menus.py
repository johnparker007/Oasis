"""Verify canonical menu topology and capture real open popups without desktop interaction."""
import argparse
import base64
import json
from pathlib import Path
import tempfile
from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", "--host", dest="dll", type=Path, default=Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="OasisMenuVerification-") as root:
        client = Client(args.dll.resolve(), Path(root))
        try:
            welcome = client.state("inspect_menus")
            assert [m["header"] for m in welcome] == ["File", "Face", "Edit", "Window", "Emulation"]
            client.state("create_project", name="Menu verification")
            client.state("create_panel2d", name="Control panel")
            client.state("add_rectangle")
            menus = client.state("inspect_menus")
            create = next(m for m in menus[0]["items"] if m["header"] == "Create")
            assert [m["header"] for m in create["items"]] == ["Panel2D", "Face", "Cabinet3D", "Machine", "Reel", "Prefab"]
            assert not any(m["header"] in ("Emulation preview", "Validate Face", "Preferences…") for m in menus[0]["items"])
            (args.output / "menus.json").write_text(json.dumps(menus, indent=2), encoding="utf-8")
            paths = [["File"], ["File", "Create"], ["File", "Import"], ["Face"], ["Edit", "Arrange"], ["Edit", "Object"], ["Window"], ["Emulation"]]
            for path in paths:
                for theme in ("Dark", "Light") if len(path) == 1 else ("Dark",):
                    result = client.tool("capture_menu", path=path, theme=theme)
                    png = base64.b64decode(next(item["data"] for item in result["content"] if item["type"] == "image"))
                    assert png.startswith(b"\x89PNG\r\n\x1a\n") and len(png) > 5000
                    (args.output / ("-".join(path).lower() + "-" + theme.lower() + ".png")).write_bytes(png)
            print("Restored menu hierarchy, six asset creation entries and 12 real Dark/Light popup captures passed.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
