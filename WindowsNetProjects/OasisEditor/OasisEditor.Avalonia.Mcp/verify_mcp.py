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
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    dll = Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll"
    with tempfile.TemporaryDirectory(prefix="oasis-avalonia-") as temporary:
        root = Path(temporary)
        project = root / "Workspace.oasisproj"
        project.write_text(json.dumps({"name": "Panel Authoring", "version": 9,
                                      "layout": {"assets": "Assets", "generated": "Generated"}}))
        client = Client(dll, root)
        try:
            names = {tool["name"] for tool in client.request("tools/list", {})["tools"]}
            assert {"capture_workspace", "update_element", "history"} <= names
            client.state("open_project", path=str(project))
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
            client.capture(args.output / "workspace-dark.png", "Dark")
            client.capture(args.output / "workspace-light.png", "Light")
            client.capture(args.output / "workspace-compact.png", "Dark", 1024, 700)
            client.state("open_panel2d", path=client.state()["panelPath"])
            assert len(client.state()["elements"]) == 2
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
        finally:
            client.close()
    print("MCP authoring, undo/redo, save/reopen and 3 headless PNG captures passed.")


if __name__ == "__main__":
    main()
