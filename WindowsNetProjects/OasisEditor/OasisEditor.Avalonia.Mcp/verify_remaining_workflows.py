"""Verify remaining authoring workflows using the real isolated stdio MCP host.

Outputs real headless Avalonia PNGs. Never launches a desktop application.
"""
import argparse
import json
from pathlib import Path
import tempfile
from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dll","--host",dest="dll",type=Path,default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll")
    args = parser.parse_args()
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    dll = args.dll.resolve()
    with tempfile.TemporaryDirectory(prefix="OasisRemainingMcp-") as temporary:
        root = Path(temporary)
        client = Client(dll, root)
        try:
            client.state("create_project", name="Remaining workflows")
            machine = client.state()["assets"]
            machine_path = next(a["manifestPath"] for a in machine if a["type"] == "Machine")
            first = client.state("create_panel2d", name="Controls")
            panel_path = first["panelPath"]
            ids = []
            for index, kind in enumerate(("lamp", "reel", "sevenSegment", "alpha", "vfdDotMatrix")):
                state = client.state("add_panel_component", kind=kind, x=20 + index * 155, y=35 + (index % 2) * 130)
                ids.append(state["selectedId"])
            client.state("update_panel_component", id=ids[0], displayNumber=7, displayText="Start lamp", onColorHex="#20FF40")
            assert client.state("inspect_panel_component", id=ids[0])["settings"]["displayNumber"] == 7
            client.state("select_elements", ids=ids[:2])
            client.state("copy_selection")
            other = client.state("create_panel2d", name="Copied controls")
            other_path = other["panelPath"]
            pasted = client.state("paste_selection")
            assert len(pasted["elements"]) == 2 and not set(ids).intersection(pasted["selectedIds"])
            client.state("send_selection_to_back"); client.state("bring_selection_to_front")
            client.state("cut_selection"); assert not client.state()["elements"]
            client.state("history"); assert len(client.state()["elements"]) == 2
            tabs = client.state("list_open_documents")
            assert len(tabs) == 2 and all(t["isDirty"] for t in tabs)
            blocked = client.request("tools/call", {"name": "close_document", "arguments": {"kind": "Panel2D", "path": panel_path}})
            assert blocked.get("isError"), blocked
            client.state("activate_document", kind="Panel2D", path=panel_path)
            assert len(client.state()["elements"]) == 5
            client.state("save_all_documents")
            for theme in ("Dark", "Light"):
                client.capture(output / f"panel-components-{theme.lower()}.png", theme)

            face = client.state("create_face", name="Face components")
            face_path = face["face"]["facePath"]
            client.state("add_face_button", name="Start", x=30, y=30, inputId="Start")
            seven = client.state("add_face_seven_segment_display", name="Credits", x=150, y=30, digitCount=4, displayIndex=0)
            client.state("add_face_alpha_display", name="Message", x=30, y=130, digitCount=16, displayIndex=0)
            client.state("update_face_generation_settings", settings={"maskExtractionThreshold": 90, "trayBoundsInflationPercent": 4, "trayBoundsPaddingPixels": 2,
                "clampTrayBoundsToLampWindow": True, "postWarpSharpeningEnabled": True, "postWarpSharpeningAmount": .4,
                "postWarpSharpeningRadiusPixels": 1, "postWarpSharpeningThreshold": 5})
            client.state("face_history"); client.state("face_history", redo=True); client.state("save_face")
            for theme in ("Dark", "Light"):
                client.capture(output / f"face-components-{theme.lower()}.png", theme)
            client.state("validate_face")
            client.state("open_machine", path=machine_path)
            graph = client.state("show_machine_composition_graph", visible=True)
            assert any(n["kind"] == "Machine" for n in graph["nodes"])
            for theme in ("Dark", "Light"):
                client.capture(output / f"machine-graph-{theme.lower()}.png", theme)
            settings = str(root / "workspace.json")
            saved = client.state("save_workspace", path=settings)
            assert len(saved["documents"]) == 4
            client.state("close_project"); assert not client.state("list_open_documents")
            client.state("restore_workspace", path=settings)
            assert len(client.state("list_open_documents")) == 4
            assert client.state("inspect_layout")["machineGraph"]
            client.state("close_document", kind="Panel2D", path=other_path)
            client.state("activate_document", kind="Face", path=face_path)
            assert len(client.state()["face"]["elements"]) == 3
            prefs = client.state("save_preferences", path=str(root / "preferences.json"), preferences={
                "processing": {"cpuMode": 2, "customMaximumWorkers": 1},
                "player": {"executablePath": "", "fullscreen": True, "previewWidth": 1600, "previewHeight": 900}})
            assert prefs["effectiveProcessingWorkers"] == 1
            assert json.loads((root / "preferences.json").read_text())["Player"]["Fullscreen"]
            client.state("close_project")
            print("Remaining workflows: native component/clipboard/history, tabs/save/close/restore, graph, explicit preferences and Dark/Light captures passed.")
        finally:
            client.close()


if __name__ == "__main__":
    main()
