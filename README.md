# YouTube Archivar for Jellyfin

Maintained by felixfoertsch. [Support and issue tracker](https://foerge.de/felixfoertsch/jellyfin-yt-archivar/issues).

This plugin is based on the [TubeArchivist Jellyfin plugin](https://github.com/tubearchivist/tubearchivist-jf-plugin) by DarkFighterLuke and contributors.

## Metadata only

Archive channels become shows, videos become episodes, and publication years become seasons. Channel thumb, banner and tvart/backdrop and episode thumbnails load through authenticated backend requests. Default episode numbering or YYYYMMDD numbering remains configurable.

There are no progress/watched or playlist synchronization tasks, event handlers, startup actions or controls. Legacy sync flags/usernames/intervals are ignored and cannot reactivate synchronization. The plugin does not change existing history, playlists, playlist order, subscriptions, retention or media. Watch progress lives in Jellyfin. No account linking is needed.

YouTube Archivar uses its own plugin GUID `60b0b958-e53f-443d-b4dc-43c950bc75de`, assembly and settings file. Configure the backend and select the YouTube Archivar providers after installation. Scope providers to the configured archive library and mount its media read-only using the channel/video filesystem layout. Configure the library as Shows and enable YouTube Archivar metadata and image providers. Disable competing providers if unwanted. Episode publication years determine the seasons.

## Connection and safety

Configure Collection Title to match the Jellyfin library display name, backend HTTP(S) base URL (including any prefix), your API token, maximum overview length and numbering scheme. HTTPS is recommended; HTTP exposes the token and metadata on the network. Legacy URLs without a scheme retain HTTP behavior. Keys are sent only in request headers to the exact backend origin/base path. Redirects and external images are rejected; no keys appear in image URLs. Clearing/changing the connection invalidates pending client results. Missing/pruned metadata returns no successful replacement; authentication, malformed data and server failures propagate instead of producing valid empty metadata.

Existing server URLs and API keys are loaded from saved settings after upgrading. Saving settings writes the new field names.

The server API source review covered channel and video detail routes, artwork URLs and media authentication at revision `20e14a981330ed2d132347aa872684d89c22e74c`. Felix verifies catalog installation, metadata refresh, artwork, and preservation of watch history and playlists in Jellyfin.

## Build and private delivery

Target: .NET 10 (`net10.0`), Jellyfin.Controller/Model 12.4.0, catalog ABI 12.4.0.0. CI installs pinned SDK 10.0.400. Existing build checks remain mandatory:

```
python3 scripts/test-release.py
dotnet build YouTubeArchivar.sln -c Release
dotnet run --file scripts/test-configuration.cs -c Release
python3 scripts/release.py build "$EDGE_TAG" "$GITHUB_SHA" artifacts
python3 scripts/release.py verify "$EDGE_TAG" "$GITHUB_SHA" artifacts
```

Private main pushes publish immutable Gitea `vYYYY.MM.DD.N` edge releases through a separate publisher. ZIP assemblies retain four numeric version components `1.5.<run quotient>.<run remainder>` (base 65535). `build.json` records the exact source revision and SHA-256 of ZIP/catalog artifacts; Jellyfin catalog entries use the ZIP's required MD5 checksum. Existing YouTube Archivar version entries and published release assets are preserved.

## Install from the public Jellyfin catalog

Add this repository URL under Jellyfin Dashboard → Plugins → Repositories:

`https://raw.githubusercontent.com/felixfoertsch/jellyfin-yt-archivar/jellyfin-catalog/manifest.json`

Install YouTube Archivar from the catalog and restart when Jellyfin requests it. Updates use the same URL. After publishing each private edge release, the Gitea workflow updates the generated catalog branch. The scheduled mirror copies the branch and release assets separately, without running GitHub Actions. The manifest or ZIP may be temporarily unavailable during replication; retry after the mirror finishes.

Private Gitea releases retain `manifest.json` for existing consumers alongside `catalog.json`, `jellyfin-yt-archivar.zip` and `build.json`. Publication does not install, restart or change any consumer pin.

Before an upgrade, securely back up the plugin directory/configuration and retain the prior immutable ZIP/catalog/version. To roll back, stop Jellyfin and restore that prior plugin/configuration, then restart. Older versions may contain synchronization: disable their sync settings before using them. Do not reset user history or playlists as a rollback step. Felix performs installation and restarts on his own server.
