#!/usr/bin/env python3
"""Offline contract and tamper checks; no build tools or credentials required."""
import hashlib
import json
from pathlib import Path
import tempfile
import zipfile
import release

sha = 'a' * 40
tag = 'v2026.11.16.123'
assert release.identity(tag, sha) == '1.5.0.123'
assert release.identity('v2026.11.16.65535', sha) == '1.5.1.0'
for invalid in ['v1.4.5', 'v2026.02.30.1', 'v2026.11.16.0', '../bad']:
	try:
		release.identity(invalid, sha)
	except (AssertionError, ValueError):
		pass
	else:
		raise AssertionError(invalid)
with tempfile.TemporaryDirectory() as temp:
	directory = Path(temp)
	with zipfile.ZipFile(directory / release.ASSET, 'w') as archive:
		archive.writestr('Jellyfin.Plugin.TubeArchivistMetadata.dll', b'test payload')
	entry = dict(version='1.5.0.123', sourceUrl=f'{release.BASE}/releases/download/{tag}/{release.ASSET}',
		checksum=hashlib.md5((directory / release.ASSET).read_bytes()).hexdigest())
	(directory / 'manifest.json').write_text(json.dumps([{'versions': [entry]}]))
	metadata = dict(channel='edge', releaseTag=tag, sourceRevision=sha, pluginVersion='1.5.0.123',
		assets={name: release.digest((directory / name).read_bytes()) for name in [release.ASSET, 'manifest.json']})
	(directory / 'build.json').write_text(json.dumps(metadata))
	release.verify(directory, tag, sha)
	for field, value in [('channel', 'stable'), ('sourceRevision', 'b' * 40)]:
		changed = dict(metadata, **{field: value})
		(directory / 'build.json').write_text(json.dumps(changed))
		try:
			release.verify(directory, tag, sha)
		except ValueError:
			pass
		else:
			raise AssertionError('Accepted altered provenance')
	(directory / 'build.json').write_text(json.dumps(metadata))
	(directory / release.ASSET).write_bytes(b'tampered')
	try:
		release.verify(directory, tag, sha)
	except ValueError:
		pass
	else:
		raise AssertionError('Accepted altered artifact')
try:
	release.NoRedirect().redirect_request(None, None, 302, 'Found', {}, 'https://example.com/')
except ValueError:
	pass
else:
	raise AssertionError('Accepted authenticated redirect')
print('release contract checks passed')
