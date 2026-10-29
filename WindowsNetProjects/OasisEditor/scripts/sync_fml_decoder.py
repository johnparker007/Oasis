"""Vendor the C# decoder from a local, clean checkout at an explicit Git revision.

No network access or build-time downloads. Run --check to verify the pinned snapshot.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

EDITOR = Path(__file__).resolve().parents[1]
DEST = EDITOR / "Oasis.Authoring.Core/FmlDecoder/Upstream"
REPOSITORY = "https://github.com/paul-maidment/mfme-fml-decoder"
ROOTS = ("Decoder", "Decryption", "Model", "Utilities", "GameConfig", "Diagnostics")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def adapt(path, data):
    # Two narrow integration hooks; implementation stays outside the vendor tree.
    replacements = {
        "Model/Component/BaseComponent.cs": (
            "internal abstract class BaseComponent", "internal abstract partial class BaseComponent"),
        "Decoder/Component/Core/ComponentParserBase.cs": (
            "            AssignStringsFromParseResult(component, parseResult);",
            "            OasisSourcePresence.Record(component, componentTagMap, parseResult);\n"
            "            AssignStringsFromParseResult(component, parseResult);")}
    if path in replacements:
        text = data.decode("utf-8-sig").replace("\r\n", "\n")
        before, after = replacements[path]
        if text.count(before) != 1:
            raise ValueError(f"Integration hook changed upstream: {path}; review before updating")
        return text.replace(before, after).encode("utf-8")
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--checkout", type=Path)
    parser.add_argument("--revision", help="Full 40-character upstream commit SHA")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.check:
        manifest = json.loads((DEST / "upstream.json").read_text(encoding="utf-8"))
        expected = {p: entry["integratedSha256"] for p, entry in manifest["files"].items()}
        actual = {p.relative_to(DEST).as_posix(): digest(p.read_bytes())
                  for p in DEST.rglob("*") if p.is_file() and p.name != "upstream.json"}
        if actual != expected:
            raise ValueError("Vendored files differ from upstream.json; use the sync script")
        print(f"Verified {len(actual)} pinned C# source/license files at {manifest['revision']}")
        return
    if not args.checkout or not args.revision or len(args.revision) != 40:
        parser.error("Supply --checkout and the full --revision, or --check")
    checkout = args.checkout.resolve()
    def git(*arguments):
        return subprocess.check_output(["git", "-C", str(checkout), *arguments], text=True).strip()
    if git("rev-parse", "HEAD") != args.revision or git("status", "--porcelain"):
        raise ValueError("Checkout must be clean and match the requested revision")
    def blob(path):
        # Read committed bytes, independent of checkout autocrlf settings.
        return subprocess.check_output(["git", "-C", str(checkout), "show", f"HEAD:{path}"])
    files = {"LICENSE": blob("LICENSE")}
    for root in ROOTS:
        for source in (checkout / "src" / root).rglob("*.cs"):
            if not source.name.endswith("Tests.cs"):
                files[source.relative_to(checkout / "src").as_posix()] = blob(source.relative_to(checkout).as_posix())
    manifest = {"repository": REPOSITORY, "revision": args.revision,
                "license": "AGPL-3.0-or-later", "files": {}}
    # Prepare everything before replacing only the explicitly owned vendor directory.
    integrated = {path: adapt(path, data) for path, data in files.items()}
    if DEST.resolve() != (EDITOR / "Oasis.Authoring.Core/FmlDecoder/Upstream").resolve():
        raise ValueError("Unexpected vendor target")
    for existing in DEST.rglob("*"):
        if existing.is_file():
            existing.unlink()
    for path, data in integrated.items():
        target = DEST / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        manifest["files"][path] = {"upstreamSha256": digest(files[path]), "integratedSha256": digest(data)}
    (DEST / "upstream.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Vendored {len(files)} source/license files at {args.revision}")


if __name__ == "__main__":
    main()
