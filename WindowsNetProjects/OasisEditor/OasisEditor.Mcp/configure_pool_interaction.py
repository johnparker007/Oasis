import argparse,asyncio,os,json
from pathlib import Path
from fastmcp import Client
from fastmcp.client.transports import StdioTransport
from smoke_test import payload
async def main(args):
 project=Path(args.project).resolve();out=Path(args.artifacts).resolve();out.mkdir(parents=True,exist_ok=True)
 async with Client(StdioTransport(str(Path(args.server).resolve()),['--workspace',str(project.parent)],env=dict(os.environ),log_file=out/'profile-server.log'),timeout=240) as client:
  async def call(tool,**params):return payload(await client.call_tool(tool,params))
  await call('open_project',path=str(project))
  opened=await call('open_machine',path=str(project.parent/'Assets/Machines/PoolTableTest1/asset.machine'))
  doc=opened['documentId']
  await call('set_machine_interaction',documentId=doc,kind='Pool',cuePrefabAlias='cueBall',playingSurfaceId='playingSurface')
  saved=await call('save_machine',documentId=doc)
  build=await call('build_machine',documentId=doc)
  assert build['success'],build
  manifest=json.loads((Path(build['buildRoot'])/'machine.runtime.json').read_text(encoding='utf-8-sig'))
  assert manifest['schemaVersion']==13 and manifest['interaction']['kind']=='Pool'
  assert manifest['interaction']['cuePrefabAlias']=='cueBall' and manifest['interaction']['playingSurfaceId']=='playingSurface'
  (out/'profile-report.json').write_text(json.dumps(dict(saved=saved,build=build),indent=2))
  print(json.dumps(build),flush=True)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--server',required=True);p.add_argument('--project',required=True);p.add_argument('--artifacts',required=True);asyncio.run(main(p.parse_args()))
