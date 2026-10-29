"""Verify persisted MFME controls/live names through real headless MCP, using an upstream sample."""
import argparse
import json
from pathlib import Path
import shutil
import tempfile

from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--upstream", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dll", type=Path, default=Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    args = parser.parse_args(); args.output.mkdir(parents=True, exist_ok=True)
    source = next(p for p in (args.upstream / "test/smoke_test").rglob("*.fml") if "Button/WithoutAngle" in p.as_posix())
    with tempfile.TemporaryDirectory(prefix="oasis-controls-mcp-") as temp:
        root = Path(temp); shutil.copyfile(source, root / "control.fml"); client = Client(args.dll, root)
        try:
            client.state("create_project", name="Controls"); client.state("create_panel2d", name="Panel")
            imported = client.state("import_fml", path=str(root / "control.fml"))["result"]
            assert imported["succeeded"] and imported["inputDefinitions"], imported
            state = client.state(); panel = state["panelPath"]
            control = next(g for g in state["groups"] if (g.get("control") or {}).get("input"))
            control_id = control["id"]; section = control["parentId"]
            channels = client.state("inspect_panel_control", id=control_id)["channels"]
            assert len(channels) > 1 and all(e["parentId"] == control_id for e in channels)
            first = imported["inputDefinitions"][0]
            assert first["linkedVisualElementId"].replace("-", "") == control_id
            client.state("set_panel_group_expanded", id=section, expanded=True)
            client.state("select_elements", ids=[control_id])
            for theme in ("Dark", "Light"): client.capture(args.output / f"imported-control-{theme.lower()}.png", theme)
            client.state("update_panel_group", id=section, name="Gamble ladder")
            assert next(g for g in client.state("inspect_panel_hierarchy")["labels"] if g["id"] == section)["displayName"].startswith("Gamble ladder (")
            client.state("update_panel_group", id=section, automaticName=True)
            client.state("update_panel_control_input", id=control_id, buttonNumber="5", coinInput=False, keyboardShortcut="N")
            client.state("save_panel2d"); client.state("close_document", kind="Panel2D", path=panel); client.state("open_panel2d", path=panel)
            assert client.state()["recoveredInputDefinitions"][0]["buttonNumber"] == "5"
            client.state("float_document", kind="Panel2D", path=panel)
            assert client.state()["recoveredInputDefinitions"][0]["buttonNumber"] == "5"
            client.state("dock_document", kind="Panel2D", path=panel)
            assert client.state()["recoveredInputDefinitions"][0]["buttonNumber"] == "5"
            client.state("create_machine", name="Input target")
            applied = client.state("apply_recovered_fml_inputs")
            assert first["id"] in json.dumps(applied), applied
            client.state("open_panel2d", path=panel)
            shape = client.state("add_source_shape", name="Face")["sourceShapes"][-1]["id"]
            client.state("save_panel2d")
            face = client.state("generate_face_from_source_shape", sourceShapeId=shape, title="Generated")
            buttons = [e for e in face["face"]["elements"] if e["kind"].lower() == "button"]
            assert len(buttons) == 1, face
            client.state("open_panel2d", path=panel); client.state("select_elements", ids=[control_id]); client.state("duplicate_selection")
            assert len(client.state()["recoveredInputDefinitions"]) == 2
            client.state("history"); assert len(client.state()["recoveredInputDefinitions"]) == 1
            client.state("update_panel_control_input", id=control_id, enabled=False); assert not client.state()["recoveredInputDefinitions"]
            disabled = client.state("inspect_panel_control", id=control_id)["group"]["control"]["input"]
            assert disabled["buttonNumber"] == "5" and not disabled["enabled"]
            client.state("update_panel_control_input", id=control_id, enabled=True)
            assert client.state()["recoveredInputDefinitions"][0]["id"] == first["id"]
            client.state("history"); assert not client.state()["recoveredInputDefinitions"]
            client.state("history"); assert client.state()["recoveredInputDefinitions"][0]["buttonNumber"] == "5"
        finally:
            client.close()
    print("MCP controls passed: real import, all channels/one input, live/custom labels, save/reopen, float/dock, Machine handoff, Face input, duplicate and undo.")


if __name__ == "__main__":
    main()
