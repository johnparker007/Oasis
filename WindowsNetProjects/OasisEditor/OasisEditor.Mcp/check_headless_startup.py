"""Windows-only regression check: the MCP process must never show a window.
Uses only Python's standard library. Inspects windows belonging to its own child
process; it sends no desktop input. Also checks clean termination on stdin EOF.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time


def visible_windows(pid):
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user32.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user32.IsWindowVisible.argtypes = [wintypes.HWND]
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    found = []

    @callback_type
    def inspect(window, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(window, ctypes.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(window):
            found.append(int(window))
        return True

    user32.EnumWindows(inspect, 0)
    return found


def check(server):
    with tempfile.TemporaryDirectory(prefix="oasis-headless-") as workspace:
        process = subprocess.Popen([str(Path(server).resolve()), "--workspace", workspace],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   text=True, encoding="utf-8", env=dict(os.environ),
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        messages = queue.Queue()
        errors = []
        def read_output():
            for line in process.stdout:
                messages.put(json.loads(line))
        def read_errors():
            errors.extend(process.stderr)
        threading.Thread(target=read_output, daemon=True).start()
        threading.Thread(target=read_errors, daemon=True).start()
        def send(message):
            process.stdin.write(json.dumps({"jsonrpc": "2.0", **message}) + "\n")
            process.stdin.flush()
        def response(request_id):
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                if visible_windows(process.pid):
                    raise AssertionError("MCP process displayed a visible window")
                try:
                    message = messages.get(timeout=0.05)
                    if message.get("id") == request_id:
                        assert "result" in message, message
                        return message["result"]
                except queue.Empty:
                    if process.poll() is not None:
                        raise AssertionError("Server exited: " + "".join(errors))
            raise TimeoutError("MCP response timeout")
        try:
            send({"id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "headless-regression", "version": "1"}}})
            response(1)
            send({"method": "notifications/initialized"})
            send({"id": 2, "method": "tools/list"})
            assert {"create_project", "capture_panel2d", "update_panel_element", "select_panel_elements"} <= {t["name"] for t in response(2)["tools"]}
            # Give the application's queued WPF startup event time to execute.
            for _ in range(20):
                assert not visible_windows(process.pid), "MCP process opened a visible window after initialization"
                time.sleep(0.05)
            process.stdin.close()
            assert process.wait(timeout=5) == 0, "Server exited with an error"
            print("PASS: MCP initialized, no visible windows during startup/idle, clean exit on stdin EOF")
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=5)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", required=True)
    check(parser.parse_args().server)
