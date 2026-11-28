#!/usr/bin/env python3
"""Static repository contracts; does not replace a Unity compile/import."""
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
assets = root / 'Assets'
errors = []
def check(condition, message):
    if not condition:
        errors.append(message)

seen = {}
for path in assets.rglob('*'):
    if path.suffix == '.meta':
        match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
        check(match is not None, f'Invalid GUID: {path}')
        if match:
            guid = match[1]
            check(guid not in seen, f'Duplicate GUID: {path} and {seen.get(guid)}')
            seen[guid] = path
        check(Path(str(path)[:-5]).exists(), f'Orphan meta: {path}')
    else:
        check(Path(str(path) + '.meta').exists(), f'Missing meta: {path}')

assemblies = {}
for path in assets.rglob('*.asmdef'):
    data = json.loads(path.read_text())
    check(data['name'] not in assemblies, f'Duplicate assembly: {path}')
    assemblies[data['name']] = data
external = {'Unity.Addressables', 'Unity.ResourceManager', 'Unity.Addressables.Editor'}
for name, data in assemblies.items():
    for reference in data.get('references', []):
        check(reference in assemblies or reference in external, f'Unknown assembly reference: {reference}')
    if '.Editor' in name or '.Tests.' in name:
        check(data.get('includePlatforms') == ['Editor'], f'Editor assembly leaks into Player: {name}')
runtime = assets / 'LGameFramework/Runtime'
for path in runtime.rglob('*.cs'):
    check('using UnityEditor' not in path.read_text(), f'UnityEditor in runtime: {path}')
    check('using LGame.Game' not in path.read_text(), f'Game dependency in framework: {path}')
manifest = json.loads((root / 'Packages/manifest.json').read_text())['dependencies']
locked = json.loads((root / 'Packages/packages-lock.json').read_text())['dependencies']
for name, version in manifest.items():
    check(name in locked and locked[name]['version'] == version and locked[name]['depth'] == 0,
          f'Direct package mismatch: {name}')
check(manifest['com.unity.addressables'] == '1.21.21', 'Unexpected Addressables version')
check(locked['com.unity.scriptablebuildpipeline']['version'] == '1.21.23', 'Unexpected SBP version')
meta = (assets / 'Game/Runtime/Bootstrap/AppMain.cs.meta').read_text()
check('3912e86cd46e9404b91172ec2b369dc2' in meta, 'AppMain GUID changed')
if errors:
    raise SystemExit('\n'.join(errors))
print(f'PASS: {len(seen)} unique Unity GUIDs, {len(assemblies)} assemblies, package pins and runtime boundaries.')
