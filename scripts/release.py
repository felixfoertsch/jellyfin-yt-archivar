#!/usr/bin/env python3
"""Build once, verify delivered bytes, publish an immutable Jellyfin edge release."""
import base64
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.error
import urllib.request
import zipfile

ASSET = 'jellyfin-yt-archivar.zip'
REPO = 'felixfoertsch/jellyfin-yt-archivar'
BASE = 'https://foerge.de/' + REPO
PUBLIC = 'https://github.com/' + REPO


class NoRedirect(urllib.request.HTTPRedirectHandler):
	def redirect_request(self, req, fp, code, msg, headers, newurl):
		raise ValueError('Authenticated release requests must not redirect')


HTTP = urllib.request.build_opener(NoRedirect())


def identity(tag, sha):
	match = re.fullmatch(r'v(\d{4})\.(\d{2})\.(\d{2})\.([1-9]\d*)', tag)
	if not (match and re.fullmatch('[0-9a-f]{40}', sha)):
		raise ValueError('Invalid release identity')
	datetime.date(*map(int, match.groups()[:3]))
	run = int(match[4])
	# ponytail: four UInt16 assembly components; fail rather than wrap after 4 billion runs.
	if not (run < 65535 ** 2):
		raise ValueError('Assembly version space exhausted')
	return f'1.5.{run // 65535}.{run % 65535}'


def digest(data):
	return hashlib.sha256(data).hexdigest()


def build(tag, sha, directory):
	version = identity(tag, sha)
	if not (subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == sha):
		raise ValueError("Release validation failed: subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip() == sha")
	subprocess.run(['dotnet', 'build', 'TubeArchivistMetadata.sln', '-c', 'Release',
		f'-p:Version={version}', f'-p:AssemblyVersion={version}', f'-p:FileVersion={version}'], check=True)
	directory.mkdir()
	dll = Path('Jellyfin.Plugin.TubeArchivistMetadata/bin/Release/net9.0/Jellyfin.Plugin.TubeArchivistMetadata.dll').read_bytes()
	with zipfile.ZipFile(directory / ASSET, 'w', zipfile.ZIP_DEFLATED) as archive:
		archive.writestr(zipfile.ZipInfo('Jellyfin.Plugin.TubeArchivistMetadata.dll', (1980, 1, 1, 0, 0, 0)), dll)
	plugin = json.loads(Path('manifest.json').read_text())[0]
	entry = dict(plugin['versions'][0])
	entry.update(version=version, sourceUrl=f'{BASE}/releases/download/{tag}/{ASSET}',
		checksum=hashlib.md5((directory / ASSET).read_bytes()).hexdigest(),
		timestamp=datetime.datetime.now(datetime.timezone.utc).isoformat().replace('+00:00', 'Z'), changelog=f'Edge build {tag} ({sha})')
	plugin.update(imageUrl=f'{BASE}/raw/commit/{sha}/images/logo.png', versions=[entry])
	(directory / 'manifest.json').write_text(json.dumps([plugin], indent=2) + '\n')
	consumer = dict(plugin, imageUrl=f'https://raw.githubusercontent.com/{REPO}/{sha}/images/logo.png', versions=[dict(entry, sourceUrl=f'{PUBLIC}/releases/download/{tag}/{ASSET}')])
	(directory / 'catalog.json').write_text(json.dumps([consumer], indent=2) + '\n')
	metadata = dict(channel='edge', releaseTag=tag, sourceRevision=sha, pluginVersion=version,
		assets={name: digest((directory / name).read_bytes()) for name in [ASSET, 'manifest.json', 'catalog.json']})
	(directory / 'build.json').write_text(json.dumps(metadata, indent=2) + '\n')


def verify(directory, tag, sha):
	version = identity(tag, sha)
	if not ({p.name for p in directory.iterdir()} == {ASSET, 'manifest.json', 'catalog.json', 'build.json'}):
		raise ValueError("Release validation failed: {p.name for p in directory.iterdir()} == {ASSET, 'manifest.json', 'catalog.json', 'build.json'}")
	metadata = json.loads((directory / 'build.json').read_text())
	if not ((metadata['channel'], metadata['releaseTag'], metadata['sourceRevision'], metadata['pluginVersion']) == ('edge', tag, sha, version)):
		raise ValueError("Release validation failed: (metadata['channel'], metadata['releaseTag'], metadata['sourceRevision'], metadata['pluginVersion']) == ('edge', tag, sha, version)")
	if not (set(metadata['assets']) == {ASSET, 'manifest.json', 'catalog.json'}):
		raise ValueError("Release validation failed: set(metadata['assets']) == {ASSET, 'manifest.json', 'catalog.json'}")
	for name, info in metadata['assets'].items():
		if not (digest((directory / name).read_bytes()) == info):
			raise ValueError(f'Hash mismatch: {name}')
	entry = json.loads((directory / 'manifest.json').read_text())[0]['versions']
	if not (len(entry) == 1):
		raise ValueError('Release validation failed: len(entry) == 1')
	if not (entry[0]['version'] == version and entry[0]['sourceUrl'] == f'{BASE}/releases/download/{tag}/{ASSET}'):
		raise ValueError("Release validation failed: entry[0]['version'] == version and entry[0]['sourceUrl'] == f'{BASE}/releases/download/{tag}/{ASSET}'")
	if not (entry[0]['checksum'] == hashlib.md5((directory / ASSET).read_bytes()).hexdigest()):
		raise ValueError("Release validation failed: entry[0]['checksum'] == hashlib.md5((directory / ASSET).read_bytes()).hexdigest()")
	plugin = json.loads(Path('manifest.json').read_text())[0]
	expected_entry = dict(plugin['versions'][0], version=version,
		sourceUrl=f'{BASE}/releases/download/{tag}/{ASSET}', checksum=hashlib.md5((directory / ASSET).read_bytes()).hexdigest(),
		timestamp=entry[0]['timestamp'], changelog=f'Edge build {tag} ({sha})')
	expected = dict(plugin, imageUrl=f'{BASE}/raw/commit/{sha}/images/logo.png', versions=[expected_entry])
	if json.loads((directory / 'manifest.json').read_text()) != [expected]:
		raise ValueError('Private catalog mismatch')
	consumer = dict(expected, imageUrl=f'https://raw.githubusercontent.com/{REPO}/{sha}/images/logo.png',
		versions=[dict(expected_entry, sourceUrl=f'{PUBLIC}/releases/download/{tag}/{ASSET}')])
	if json.loads((directory / 'catalog.json').read_text()) != [consumer]:
		raise ValueError('Consumer catalog mismatch')
	with zipfile.ZipFile(directory / ASSET) as archive:
		if not (archive.namelist() == ['Jellyfin.Plugin.TubeArchivistMetadata.dll']):
			raise ValueError("Release validation failed: archive.namelist() == ['Jellyfin.Plugin.TubeArchivistMetadata.dll']")
		if not (archive.testzip() is None):
			raise ValueError('Release validation failed: archive.testzip() is None')


def publisher():
	if not (os.environ['GITHUB_REPOSITORY'] == REPO):
		raise ValueError("Release validation failed: os.environ['GITHUB_REPOSITORY'] == REPO")
	if not (os.environ['GITHUB_REF'] == 'refs/heads/main'):
		raise ValueError("Release validation failed: os.environ['GITHUB_REF'] == 'refs/heads/main'")
	if not (os.environ['GITHUB_EVENT_NAME'] == 'push'):
		raise ValueError("Release validation failed: os.environ['GITHUB_EVENT_NAME'] == 'push'")
	token = os.environ['GITEA_TOKEN']
	if not (token):
		raise ValueError('Release validation failed: token')
	api = f'https://foerge.de/api/v1/repos/{REPO}'

	def request(path, data=None, method=None, content_type='application/json'):
		req = urllib.request.Request(api + path, data=data, method=method,
			headers={'Authorization': 'token ' + token, 'Content-Type': content_type})
		with HTTP.open(req, timeout=60) as response:
			return response.read()

	return request


def publish(directory, tag, sha):
	verify(directory, tag, sha)
	request = publisher()
	token = os.environ['GITEA_TOKEN']
	# Check exact provenance against forge state; never move an existing tag or repair a partial release.
	commit = json.loads(request('/git/commits/' + sha))
	if not (commit['sha'] == sha):
		raise ValueError("Release validation failed: commit['sha'] == sha")
	try:
		request('/tags/' + tag)
	except urllib.error.HTTPError as error:
		if error.code != 404:
			raise
	else:
		raise RuntimeError('Tag already exists; published identities are immutable')
	release = json.loads(request('/releases', json.dumps(dict(tag_name=tag, target_commitish=sha,
		name=tag, draft=True, prerelease=True, body=f'Experimental edge build of {sha}. No consumer installation.')).encode(), 'POST'))
	for name in [ASSET, 'manifest.json', 'catalog.json', 'build.json']:
		data = (directory / name).read_bytes()
		boundary = 'jellyfin-edge-upload'
		body = (f'--{boundary}\r\nContent-Disposition: form-data; name="attachment"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + data + f'\r\n--{boundary}--\r\n'.encode())
		request(f'/releases/{release["id"]}/assets?name={name}', body, 'POST', 'multipart/form-data; boundary=' + boundary)
		# Upload responses may use /attachments/<uuid>; verify bytes at the fixed release URL.
		url = f'{BASE}/releases/download/{tag}/{name}'
		with HTTP.open(urllib.request.Request(url, headers={'Authorization': 'token ' + token}), timeout=60) as response:
			remote = response.read()
		if not (digest(remote) == digest(data)):
			raise ValueError(f'Uploaded hash mismatch: {name}')
	request(f'/releases/{release["id"]}', b'{"draft":false}', 'PATCH')


def publish_catalog(directory, tag, sha):
	verify(directory, tag, sha)
	request = publisher()
	catalog = (directory / 'catalog.json').read_bytes()
	# ponytail: one verified version; add retention only if older builds need catalog discovery.
	version = json.loads(catalog)[0]['versions'][0]['version']
	path = '/contents/manifest.json'
	branch = 'jellyfin-catalog'
	options = dict(branch=branch)
	try:
		current = json.loads(request(path + '?ref=' + branch))
	except urllib.error.HTTPError as error:
		if error.code != 404:
			raise
		current = json.loads(request(path + '?ref=main'))
		options = dict(branch='main', new_branch=branch)
	else:
		previous = json.loads(base64.b64decode(current['content']))[0]['versions'][0]['version']
		if tuple(map(int, previous.split('.'))) >= tuple(map(int, version.split('.'))):
			return  # A retried older workflow must not roll the public catalog back.
	request(path, json.dumps(dict(options, sha=current['sha'], content=base64.b64encode(catalog).decode(),
		message=f'publish Jellyfin catalog for {tag}')).encode(), 'PUT')


if __name__ == '__main__':
	mode, tag, sha, directory = sys.argv[1:]
	if not (mode in {'build', 'verify', 'publish', 'publish_catalog'}):
		raise ValueError(f'Invalid release command: {mode}')
	globals()[mode](Path(directory), tag, sha) if mode != 'build' else build(tag, sha, Path(directory))
