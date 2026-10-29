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
from verify_unsaved_changes import request, response


def verify_retry_after_discard(client, root, samples):
    sample = next(p for p in samples if "Button/WithoutAngle" in p.as_posix())
    source = root / "retry.fml"; shutil.copyfile(sample, source)
    state = client.state(); project = state["projectFilePath"]; panel = state["panelPath"]
    project_root = Path(project).parent
    first = client.state("import_fml", path=str(source))["result"]
    assert first["succeeded"] and first["importedElementCount"] > 0, first
    originals = {p: (project_root / p).read_bytes() for p in first["copiedAssetRelativePaths"]}
    request(client, "project"); assert response(client, "discard")["result"]
    assert all(not (project_root / p).exists() for p in originals)
    assert not (project_root / "Assets/FmlImport/retry").exists()
    client.state("open_project", path=project); client.state("open_panel2d", path=panel)
    assert not client.state()["elements"]
    second = client.state("import_fml", path=str(source))["result"]
    assert second["succeeded"] and second["importedElementCount"] == first["importedElementCount"], second
    assert all(p.startswith("Assets/FmlImport/retry/") for p in second["copiedAssetRelativePaths"]), second
    client.state("save_panel2d")
    client.state("create_panel2d", name="Retry in new panel")
    third = client.state("import_fml", path=str(source))["result"]
    assert third["succeeded"] and third["importedElementCount"] == first["importedElementCount"], third
    assert all(p.startswith("Assets/FmlImport/retry_2/") for p in third["copiedAssetRelativePaths"]), third
    for path, data in originals.items(): assert (project_root / path).read_bytes() == data
    client.state("history"); assert not client.state()["elements"] and not client.state()["isDirty"]
    preview = client.state("inspect_unreferenced_assets")
    offered = {p["path"] for p in preview["candidates"]}
    assert not offered.intersection(third["copiedAssetRelativePaths"]), "Redo references must survive cleanup"
    client.state("history", redo=True)
    unrelated = project_root / "Assets/FmlImport/unrelated/keep.png"
    unrelated.parent.mkdir(parents=True); unrelated.write_bytes(b"user kept this orphan")
    request(client, "project"); assert response(client, "discard")["result"]
    assert all(not (project_root / p).exists() for p in third["copiedAssetRelativePaths"])
    for path, data in originals.items(): assert (project_root / path).read_bytes() == data
    assert unrelated.exists(), "Discard must not clean unrelated, previously unchecked files"
    client.state("open_project", path=project)
    preview = client.state("inspect_unreferenced_assets")
    assert "Assets/FmlImport/unrelated/keep.png" in {p["path"] for p in preview["candidates"]}
    result = client.state("delete_unreferenced_assets", scanId=preview["scanId"], paths=["Assets/FmlImport/unrelated/keep.png"])
    assert not result["failures"] and not unrelated.exists(), result
    client.state("create_panel2d", name="Smoke layouts")


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
            verify_retry_after_discard(client, root, samples)
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
    print(f"Imported {len(results)} upstream encrypted layouts; discard/reopen/reimport into old/new panels, undo/redo and malformed-input checks passed.")


if __name__ == "__main__":
    main()
