"""Import upstream's C# smoke layouts through the real headless Editor/MCP.

Build OasisEditor.Mcp first. --upstream is the pinned upstream checkout; source
layouts are copied read-only into a disposable workspace. No desktop interaction.
"""
import argparse
import json
from pathlib import Path
import shutil
import tempfile

from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--upstream", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dll", type=Path, default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    args = parser.parse_args()
    samples = sorted((args.upstream / "test/smoke_test").rglob("*.fml"))
    assert samples, "No upstream smoke layouts found"
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    with tempfile.TemporaryDirectory(prefix="oasis-fml-mcp-") as temp:
        root = Path(temp)
        client = Client(args.dll, root)
        try:
            client.state("create_project", name="FML Smoke")
            client.state("create_panel2d", name="Import")
            for index, sample in enumerate(samples):
                source = root / f"source-{index}.fml"
                shutil.copyfile(sample, source)
                imported = client.state("import_fml", path=str(source))["result"]
                row = {"sample": sample.relative_to(args.upstream).as_posix(), **imported}
                results.append(row)
                assert imported["succeeded"], row
                count = imported["importedElementCount"]
                if count:
                    # One import is one real history command; redo restores it.
                    before = client.state()
                    client.state("history")
                    assert not client.state()["elements"], row
                    client.state("history", redo=True)
                    assert len(client.state()["elements"]) == len(before["elements"]), row
                    if "Button/WithoutAngle" in row["sample"] or "Lamp/WithoutAngle" in row["sample"]:
                        for theme in ("Dark", "Light"):
                            client.capture(args.output / f"{sample.parent.parent.name}-{theme.lower()}.png", theme)
                    client.state("history")
                assert not client.state()["isDirty"], row
            # Unsupported/malformed source must leave history/content untouched.
            invalid = root / "invalid.fml"
            invalid.write_bytes(b"not an FML")
            failed = client.state("import_fml", path=str(invalid))["result"]
            assert not failed["succeeded"]
            assert not client.state()["elements"] and not client.state()["isDirty"]
        finally:
            client.close()
            (args.output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"Imported {len(results)} upstream encrypted layouts; undo/redo and malformed-input checks passed.")


if __name__ == "__main__":
    main()
