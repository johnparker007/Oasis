"""Run a saved Emulation Machine through the real headless preview, without authored writes.

Requires matching native libraries and real, explicitly configured ROMs. Never guesses
inputs or claims credited gameplay. Uses only Python's standard library.
"""
import argparse
import json
import time
from pathlib import Path
from verify_mcp import Client


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dll', type=Path, default=Path(__file__).parent / 'bin/Debug/net9.0/OasisEditor.Avalonia.Mcp.dll')
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--machine', required=True)
    parser.add_argument('--runtime-library', type=Path, required=True)
    parser.add_argument('--provider-library', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--panel')
    parser.add_argument('--face')
    parser.add_argument('--input', help='Optional exact authored non-coin input ID for press/release verification.')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    client = Client(args.dll.resolve(), args.workspace.resolve())
    try:
        client.tool('open_project', path=str(args.project.resolve()))
        machine = client.state('open_machine', path=args.machine)['document']
        platform = machine['runtime'].get('platform', '').lower()
        fields = {'impact': 'ProductionAmberLibraryPath', 'mpu5': 'Mpu5AmberLibraryPath',
                  'epoch': 'EpochAmberLibraryPath', 'mpu3': 'Mpu3AmberLibraryPath',
                  'maygaym1': 'M1AmberLibraryPath', 'scorpion4': 'Scorpion4AmberLibraryPath'}
        if platform not in fields:
            raise ValueError('Choose a supported saved Emulation Machine.')
        preferences = {'FabricRuntimeLibraryPath': str(args.runtime_library.resolve()),
                       fields[platform]: str(args.provider_library.resolve())}
        if args.panel:
            client.tool('open_panel2d', path=args.panel)
        started = client.state('start_preview', preferences=preferences)
        assert started['state'] == 'Running', started
        deadline = time.monotonic() + 30
        while True:
            state = client.state('inspect_preview')
            if state['state'] == 'Failed':
                raise RuntimeError(state)
            if state['audioFrames'] > 48000:
                break
            if time.monotonic() >= deadline:
                raise TimeoutError(f'Provider did not deliver PCM: {state}')
            time.sleep(.25)
        if args.panel:
            client.capture(args.output / 'native-panel.png', 'Dark')
        if args.face:
            client.tool('open_face', path=args.face)
            time.sleep(.1)
            client.capture(args.output / 'native-face.png', 'Dark')
        if args.input:
            authored = next(i for i in machine['inputDefinitions'] if i['id'] == args.input)
            if authored.get('coinInput'):
                raise ValueError('This verification option accepts non-coin inputs only.')
            client.tool('preview_input', id=args.input, pressed=True)
            client.tool('preview_input', id=args.input, pressed=False)
        assert client.state('control_preview', action='pause')['state'] == 'Paused'
        assert client.state('control_preview', action='resume')['state'] == 'Running'
        assert client.state('control_preview', action='reset')['state'] == 'Running'
        assert client.state('control_preview', action='stop')['state'] == 'Stopped'
        assert client.state('start_preview', preferences=preferences)['state'] == 'Running'
        assert client.state('control_preview', action='stop')['state'] == 'Stopped'
        report = {'native': state, 'lifecycle': 'pause/resume/reset/stop/restart passed',
                  'input': args.input, 'audiblePlaybackTested': False, 'creditedGameplayTested': False}
        (args.output / 'native-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report))
    finally:
        client.close()


if __name__ == '__main__':
    main()
