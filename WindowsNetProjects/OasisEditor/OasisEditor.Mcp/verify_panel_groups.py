"""Verify real Panel2D grouping commands through an isolated headless MCP session."""
import argparse
from pathlib import Path
import tempfile

from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dll", type=Path, default=Path(__file__).parent / "bin/Debug/net9.0/OasisEditor.Mcp.dll")
    args = parser.parse_args(); args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="oasis-groups-mcp-") as workspace:
        client = Client(args.dll, Path(workspace))
        try:
            client.state("create_project", name="Groups")
            client.state("create_panel2d", name="Panel")
            a = client.state("add_panel_component", kind="lamp", x=20, y=20)["selectedId"]
            b = client.state("add_panel_component", kind="lamp", x=120, y=20)["selectedId"]
            client.state("select_elements", ids=[a, b])
            state = client.state("create_panel_group", name="Gamble ladder", icon="Lamp")
            group = state["selectedId"]; panel = state["panelPath"]
            client.state("update_panel_group", id=group, x=80, y=50, rotation=25, scaleX=1.5, scaleY=.8)
            before = next(e for e in client.state("inspect_panel_hierarchy")["world"] if e["id"] == a)
            nested = client.state("create_panel_group", name="Buttons", parentId=group, groupSelection=False, icon="Button")["selectedId"]
            client.state("select_elements", ids=[a]); client.state("reparent_panel_selection", parentId=nested)
            after = next(e for e in client.state("inspect_panel_hierarchy")["world"] if e["id"] == a)
            for key in ("x", "y", "width", "height"): assert abs(before[key] - after[key]) < 1e-8
            client.state("set_panel_group_expanded", id=group, expanded=True)
            client.state("set_panel_group_expanded", id=nested, expanded=True)
            assert len(client.state("inspect_panel_hierarchy")["visibleRows"]) == 4
            client.state("select_elements", ids=[group]); client.state("save_panel2d")
            for theme in ("Dark", "Light"): client.capture(args.output / f"groups-{theme.lower()}.png", theme)
            client.state("float_document", kind="Panel2D", path=panel)
            assert set(client.state("inspect_panel_hierarchy")["expanded"]) == {group, nested}
            client.state("dock_document", kind="Panel2D", path=panel)
            assert set(client.state("inspect_panel_hierarchy")["expanded"]) == {group, nested}
            client.state("duplicate_selection"); assert len(client.state()["groups"]) == 4
            client.state("history"); assert len(client.state()["groups"]) == 2 and not client.state()["isDirty"]
            client.state("create_panel2d", name="Other")
            client.state("activate_document", kind="Panel2D", path=panel)
            assert len(client.state()["groups"]) == 2
            client.state("close_document", kind="Panel2D", path=panel); client.state("open_panel2d", path=panel)
            hierarchy = client.state("inspect_panel_hierarchy")
            assert len(hierarchy["visibleRows"]) == 1 and not hierarchy["expanded"]
            client.state("select_elements", ids=[group]); client.state("delete_selection")
            assert not client.state()["groups"] and not client.state()["elements"]
            client.state("history"); assert len(client.state()["groups"]) == 2 and not client.state()["isDirty"]
            client.state("select_elements", ids=[nested]); client.state("ungroup_panel_selection")
            assert len(client.state()["groups"]) == 1
            client.state("history"); assert len(client.state()["groups"]) == 2
        finally:
            client.close()
    print("MCP groups: transforms, nested reparenting, collapse, float/dock, duplicate, per-document history, save/reopen, deletion and ungroup passed.")


if __name__ == "__main__":
    main()
