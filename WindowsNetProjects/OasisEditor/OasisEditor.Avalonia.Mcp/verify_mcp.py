"""Exercise the real headless MCP host. Requires Python's standard library only.

Build OasisEditor.Avalonia.Mcp first. Outputs PNGs to --output (never committed).
Optional --project/--panel capture an existing project read-only; no authored writes.
"""
import argparse
import base64
import json
from pathlib import Path
import queue
import struct
import subprocess
import tempfile
import threading
import shutil
import zlib


class Client:
    def __init__(self, dll, workspace):
        self.process = subprocess.Popen(
            ["dotnet", str(dll), "--workspace", str(workspace)],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, encoding="utf-8", creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        self.responses = queue.Queue()
        self.errors = []
        self.serial = 0
        def read():
            for line in self.process.stdout:
                try:
                    self.responses.put(json.loads(line))
                except json.JSONDecodeError:
                    self.responses.put({"error": {"message": "Non-protocol stdout: " + line}})
            self.responses.put({"error": {"message": "MCP process closed stdout"}})
        def diagnostics():
            for line in self.process.stderr:
                self.errors.append(line)
        threading.Thread(target=read, daemon=True).start()
        threading.Thread(target=diagnostics, daemon=True).start()
        self.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                    "clientInfo": {"name": "oasis-background-check", "version": "1"}})
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized"})

    def send(self, message):
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()

    def request(self, method, params):
        self.serial += 1
        self.send({"jsonrpc": "2.0", "id": self.serial, "method": method, "params": params})
        while True:
            try:
                reply = self.responses.get(timeout=45)
            except queue.Empty:
                raise RuntimeError("MCP timed out:\n" + "".join(self.errors))
            if "error" in reply:
                raise RuntimeError(str(reply["error"]) + "\n" + "".join(self.errors))
            if reply.get("id") == self.serial:
                return reply["result"]

    def tool(self, tool_name, **arguments):
        result = self.request("tools/call", {"name": tool_name, "arguments": arguments})
        if result.get("isError"):
            raise RuntimeError(str(result) + "\n" + "".join(self.errors))
        return result

    def state(self, tool_name="inspect_workspace", **arguments):
        result = self.tool(tool_name, **arguments)
        return json.loads(next(c["text"] for c in result["content"] if c["type"] == "text"))

    def capture(self, path, theme, width=1400, height=900):
        result = self.tool("capture_workspace", theme=theme, width=width, height=height)
        png = base64.b64decode(next(c["data"] for c in result["content"] if c["type"] == "image"))
        assert png[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", png[16:24]) == (width, height)
        path.write_bytes(png)

    def close(self):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
            raise RuntimeError("MCP host did not exit after stdin closed")
        if self.process.returncode:
            raise RuntimeError("".join(self.errors))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--project", type=Path)
    parser.add_argument("--panel", type=Path)
    parser.add_argument("--face", type=Path)
    parser.add_argument("--fml", type=Path, help="Import a disposable copy of an actual FML; never modifies the source")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    dll = Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll"
    with tempfile.TemporaryDirectory(prefix="oasis-avalonia-") as temporary:
        root = Path(temporary)
        client = Client(dll, root)
        try:
            names = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert {"capture_workspace", "update_element", "history", "create_project", "create_face", "import_fml"} <= names
            created = client.state("create_project", name="Panel Authoring")
            assert any(a["type"] == "Machine" for a in created["assets"])
            client.state("create_panel2d", name="Control panel")
            first = client.state("add_rectangle")["selectedId"]
            client.state("update_element", id=first, name="Artwork registration", x=0, y=0, width=680, height=420)
            second = client.state("add_rectangle")["selectedId"]
            changed = client.state("update_element", id=second, name="Button zone", x=50, y=280, width=170, height=80)
            assert changed["isDirty"]
            client.state("history")
            undone = client.state("history", redo=True)
            assert next(e for e in undone["elements"] if e["id"] == second)["name"] == "Button zone"
            client.state("save_panel2d")
            assert not client.state()["isDirty"]
            client.state("select_elements", ids=[first, second])
            duplicated = client.state("duplicate_selection", offsetX=24, offsetY=20)
            assert len(duplicated["elements"]) == 4
            client.state("move_selection", dx=20, dy=10)
            client.state("history")
            client.state("history", redo=True)
            client.state("add_source_shape", name="Main face", x=20, y=20, width=640, height=380)
            def chunk(kind, data):
                return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
            artwork = root / "Artwork.png"
            artwork.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 96, 64, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress((b"\0" + b"\xC4\x91\x3A" * 96) * 64)) + chunk(b"IEND", b""))
            imported_image = client.state("import_image", path=str(artwork), name="Button artwork")["selectedId"]
            client.state("update_element", id=imported_image, x=460, y=280, width=150, height=100)
            client.state("save_panel2d")
            client.capture(args.output / "workspace-dark.png", "Dark")
            client.capture(args.output / "workspace-light.png", "Light")
            client.capture(args.output / "workspace-compact.png", "Dark", 1024, 700)
            client.state("open_panel2d", path=client.state()["panelPath"])
            assert len(client.state()["elements"]) == 5
            assert len(client.state()["sourceShapes"]) == 1
            client.state("create_face", name="Lamp panel")
            face = client.state("add_face_lamp", name="Start button", x=160, y=140, width=260, height=120, lampIndex=0)
            lamp = face["face"]["selectedId"]
            client.state("update_face_element", id=lamp, name="Start lamp", lampIndex=3)
            client.state("face_history")
            client.state("face_history", redo=True)
            client.state("add_face_lamp", name="Hold lamp", x=460, y=140, width=260, height=120, lampIndex=7)
            face = client.state("save_face")["face"]
            assert not face["isDirty"]
            client.capture(args.output / "workspace-face-dark.png", "Dark")
            client.capture(args.output / "workspace-face-light.png", "Light")
            reopened = client.state("open_face", path=face["facePath"])["face"]
            assert len(reopened["elements"]) == 2
            if args.fml:
                source = root / args.fml.name
                shutil.copyfile(args.fml, source)
                client.state("create_panel2d", name="Imported layout")
                result = client.state("import_fml", path=str(source))
                assert result["result"]["succeeded"], result["result"]["errors"]
                assert result["result"]["importedElementCount"] > 0
                client.state("save_panel2d")
                client.capture(args.output / "workspace-imported.png", "Dark")
                print("Actual FML import:", result["result"]["importedElementCount"], "elements")
        finally:
            client.close()
    if args.project:
        client = Client(dll, args.project.resolve().parent)
        try:
            state = client.state("open_project", path=str(args.project.resolve()))
            panel = str(args.panel.resolve()) if args.panel else next(iter(state["panels"]), None)
            if panel:
                state = client.state("open_panel2d", path=panel)
                if state["elements"]:
                    client.state("select_element", id=state["elements"][-1]["id"])
                client.capture(args.output / "workspace-project.png", "Dark")
                print("Read-only real project capture:", len(state["elements"]), "elements")
            else:
                raise RuntimeError("The supplied project has no Panel2D assets")
            if args.face:
                state = client.state("open_face", path=str(args.face.resolve()))["face"]
                if state["elements"]:
                    client.state("select_face_element", id=state["elements"][-1]["id"])
                client.capture(args.output / "workspace-existing-face.png", "Dark")
                print("Read-only existing Face capture:", len(state["elements"]), "elements")
        finally:
            client.close()
    print("MCP native project/panel/image/source-region/Face authoring, history, save/reopen and headless PNG captures passed.")


if __name__ == "__main__":
    main()
