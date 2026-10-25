"""Call the opt-in Unity project tool against a disposable authored Pool build.
Unity must already run as batch with OASIS_POOL_VERIFY_BUILD explicitly configured.
Requires fastmcp 3.x; never drives the desktop or saves an authored scene.
"""
import argparse
import asyncio
import json
from pathlib import Path
from fastmcp import Client
from smoke_test import payload

async def main(args):
    async with Client(args.endpoint, timeout=120) as client:
        resource = await client.read_resource("mcpforunity://instances")
        instances = json.loads(next(b.text for b in resource if hasattr(b, "text")))
        matches = [i for i in instances["instances"] if i["name"] == "OasisPlayer"]
        if len(matches) != 1: raise RuntimeError("Expected exactly one connected OasisPlayer")
        await client.call_tool("set_active_instance", {"instance": matches[0]["id"]})
        tools = {t.name for t in await client.list_tools()}
        async def call(action):
            if "oasis_verify_pool_build" in tools:
                return payload(await client.call_tool("oasis_verify_pool_build", {"action": action}))
            if "execute_custom_tool" in tools:
                return payload(await client.call_tool("execute_custom_tool", {"tool_name": "oasis_verify_pool_build", "parameters": {"action": action}}))
            # Some bridge versions cache custom-tool discovery before the initial
            # project compile. Invoke only this fixed handler through the existing
            # C# tool rather than changing third-party tool registration internals.
            code = ('var t = System.Type.GetType("OasisPlayer.Editor.Verification.OasisRuntimeVerification, OasisPlayer.McpVerification"); '
                    'var j = System.Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json"); '
                    'var p = j.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { '
                    + json.dumps(json.dumps({"action": action})) + ' }); '
                    'return t.GetMethod("HandleCommand").Invoke(null, new object[] { p });')
            response = payload(await client.call_tool("execute_code", {"action": "execute", "code": code}))
            if not response.get("success"): return response
            return response["data"]["result"]
        result = await call("start")
        print(json.dumps(result), flush=True)
        for _ in range(180):
            await asyncio.sleep(1)
            result = await call("status")
            Path(args.output).write_text(json.dumps(result, indent=2), encoding="utf-8")
            # Project custom tools may wrap the Unity result in an MCPResponse.
            data = result
            while isinstance(data, dict) and "data" in data and isinstance(data["data"], dict) and "pending" not in data:
                data = data["data"]
            if result.get("success") is False: raise RuntimeError(json.dumps(result))
            if isinstance(data, dict) and data.get("pending") is False:
                print(json.dumps(result, indent=2), flush=True)
                return
        raise TimeoutError("Unity verification did not finish")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(); parser.add_argument("--endpoint", default="http://127.0.0.1:8080/mcp")
    parser.add_argument("--output", required=True); asyncio.run(main(parser.parse_args()))
