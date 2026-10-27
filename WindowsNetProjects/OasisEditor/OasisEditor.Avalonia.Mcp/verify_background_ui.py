"""Verify compact layouts, UTF-8 paths and saved floating workspaces in a headless MCP host.

Use --dll to run the Release/published artifact rather than the development build.
Never launches a desktop window or changes personal settings.
"""
import argparse
import base64
import json
from pathlib import Path
import tempfile
from verify_mcp import Client


def captures(client, directory, prefix, theme, width=960, height=640):
    response=client.tool("capture_workspace",theme=theme,width=width,height=height)
    blocks=[block for block in response["content"] if block["type"]=="image"]
    for index,block in enumerate(blocks):
        data=base64.b64decode(block["data"])
        assert data.startswith(b"\x89PNG\r\n\x1a\n")
        (directory / f"{prefix}-{theme.lower()}-{index}.png").write_bytes(data)
    return len(blocks)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--dll",type=Path,default=Path(__file__).resolve().parent / "bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll")
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="OasisBackgroundUI-") as temporary:
        root=Path(temporary);client=Client(args.dll.resolve(),root)
        try:
            state=client.state("create_project",name="Arcade café Ω")
            project_root=Path(state["projectFilePath"]).parent
            panel=client.state("create_panel2d",name="Façade Ω — a longer authoring document title")["panelPath"]
            first=client.state("add_rectangle")["selectedId"]
            client.state("update_element",id=first,name="Éclairage 日本語",x=30,y=40,width=180,height=100)
            client.state("save_panel2d")
            # Several asset rows and long tab captions exercise space that cannot be checked at 1400x900 alone.
            for index in range(4):
                client.state("create_panel2d",name=f"Controls {index} — café")
            client.state("activate_document",kind="Panel2D",path=panel)
            for theme in ("Dark","Light"):
                assert captures(client,args.output,"compact-panel",theme)==1
                captures(client,args.output,"large-panel",theme,1920,1200)
            reel=client.state("create_reel",name="Roue Ω — compact GPU preview")
            scene=client.state("inspect_reel")["scene"]
            assert scene["gpu"]["triangles"]>0 and not scene.get("renderingError"),scene
            for theme in ("Dark","Light"): captures(client,args.output,"compact-reel",theme)
            client.state("activate_document",kind="Panel2D",path=panel)
            client.state("show_output")
            for theme in ("Dark","Light"): captures(client,args.output,"compact-output",theme)
            client.state("float_document",kind="Panel2D",path=panel,width=960,height=640)
            settings=str(root / "workspace.json")
            client.state("save_workspace",path=settings)
            assert len(client.state("inspect_floating_documents"))==1
            client.state("close_project")
            client.state("restore_workspace",path=settings)
            floated=client.state("inspect_floating_documents")
            assert len(floated)==1 and (project_root / floated[0]["path"]).resolve()==Path(panel).resolve()
            native=client.state("inspect_floating_document",kind="Panel2D",path=panel)
            assert any(e["name"]=="Éclairage 日本語" for e in native["elements"])
            for theme in ("Dark","Light"):
                assert captures(client,args.output,"restored-floating",theme)==2
            client.state("dock_document",kind="Panel2D",path=panel)
            assert not client.state("inspect_floating_documents")
            assert client.state()["panelPath"]==panel
            client.state("close_project")
            print("Background MCP checks passed: compact/large layouts, UTF-8 native assets, saved floating workspace, redocking and isolated shutdown.")
        finally:client.close()

if __name__=="__main__":main()
