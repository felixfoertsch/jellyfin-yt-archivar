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
	plugin = json.loads(Path('manifest.json').read_text())[0]
	entry = dict(plugin['versions'][0], version='1.5.0.123', sourceUrl=f'{release.BASE}/releases/download/{tag}/{release.ASSET}',
		timestamp='2026-11-16T00:00:00Z', changelog=f'Edge build {tag} ({sha})', checksum=hashlib.md5((directory / release.ASSET).read_bytes()).hexdigest())
	plugin.update(imageUrl=f'{release.BASE}/raw/commit/{sha}/images/logo.png', versions=[entry])
	(directory / 'manifest.json').write_text(json.dumps([plugin]))
	consumer = dict(plugin, imageUrl=f'https://raw.githubusercontent.com/{release.REPO}/{sha}/images/logo.png', versions=[dict(entry, sourceUrl=f'{release.PUBLIC}/releases/download/{tag}/{release.ASSET}')])
	(directory / 'catalog.json').write_text(json.dumps([consumer]))
	metadata = dict(channel='edge', releaseTag=tag, sourceRevision=sha, pluginVersion='1.5.0.123',
		assets={name: release.digest((directory / name).read_bytes()) for name in [release.ASSET, 'manifest.json', 'catalog.json']})
	(directory / 'build.json').write_text(json.dumps(metadata))
	release.verify(directory, tag, sha)
	for malicious in [[consumer, consumer], [dict(consumer, versions=consumer['versions'] * 2)], [dict(consumer, name='other')]]:
		(directory / 'catalog.json').write_text(json.dumps(malicious))
		changed = dict(metadata, assets=dict(metadata['assets'], **{'catalog.json': release.digest((directory / 'catalog.json').read_bytes())}))
		(directory / 'build.json').write_text(json.dumps(changed))
		try:
			release.verify(directory, tag, sha)
		except ValueError:
			pass
		else:
			raise AssertionError('Accepted malicious catalog')
	(directory / 'catalog.json').write_text(json.dumps([consumer]))
	(directory / 'build.json').write_text(json.dumps(metadata))
	# Gitea's upload response may return an attachment URL, not the release URL.
	from unittest.mock import patch
	import io
	import urllib.error
	urls = []
	def fake_open(request, timeout):
		url = request.full_url
		urls.append(url)
		if '/git/commits/' in url:
			return io.BytesIO(json.dumps({'sha': sha}).encode())
		if '/tags/' in url:
			raise urllib.error.HTTPError(url, 404, 'Not Found', {}, None)
		if request.get_method() == 'POST' and url.endswith('/releases'):
			return io.BytesIO(b'{"id":1}')
		if '/assets?' in url:
			return io.BytesIO(b'{"browser_download_url":"https://example.com/attachments/uuid"}')
		if '/releases/download/' in url:
			return io.BytesIO((directory / url.rsplit('/', 1)[1]).read_bytes())
		if request.get_method() == 'PATCH':
			return io.BytesIO(b'{}')
		raise AssertionError(url)
	with patch.dict(release.os.environ, GITHUB_REPOSITORY=release.REPO, GITHUB_REF='refs/heads/main', GITHUB_EVENT_NAME='push', GITEA_TOKEN='test'), patch.object(release.HTTP, 'open', side_effect=fake_open):
		release.publish(directory, tag, sha)
	assert all(url.startswith('https://foerge.de/') for url in urls)
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

# Metadata-only delivery must not register legacy synchronization again.
root = Path(__file__).resolve().parents[1]
sources = list((root / 'Jellyfin.Plugin.TubeArchivistMetadata').rglob('*.cs'))
sources = [p for p in sources if 'obj' not in p.parts and 'bin' not in p.parts]
for path in sources:
 text = path.read_text()
 assert not any(value in text for value in ['PlaybackProgress +=', 'UserDataSaved +=', 'IScheduledTask', 'SetProgress(', 'SetWatchedStatus(', 'HttpMethod.Post', 'HttpMethod.Delete']), path
assert not list((root / 'Jellyfin.Plugin.TubeArchivistMetadata/Tasks').glob('*.cs'))
assert 'ParentIndexNumber = Published.Year' in (root / 'Jellyfin.Plugin.TubeArchivistMetadata/TubeArchivist/Video/Video.cs').read_text()
print('metadata-only source safeguards passed')

# Guard the real client/provider seams without adding a compatibility environment.
api = (root / 'Jellyfin.Plugin.TubeArchivistMetadata/TubeArchivist/TubeArchivistApi.cs').read_text()
assert "path.Contains('%', StringComparison.Ordinal)" in api
assert 'Unknown artwork route.' in api and 'Ambiguous artwork path.' in api
assert 'description == null' in api and 'JTokenType.Null' in api
assert 'tags == null ? typeof(T) == typeof(Video)' in api
for name in ['Episode', 'Series']:
 provider = (root / f'Jellyfin.Plugin.TubeArchivistMetadata/Providers/{name}MetadataProvider.cs').read_text()
 assert 'ImageUrl = video.Channel' not in provider and 'ImageUrl = channel.ThumbUrl' not in provider
 assert 'taApi.NormalizeImage(entry.ImageUrl)' in provider
assert 'result.Item.Tags = existing.Tags;' in (root / 'Jellyfin.Plugin.TubeArchivistMetadata/Providers/SeriesMetadataProvider.cs').read_text()
print('artwork and replacement source safeguards passed')
