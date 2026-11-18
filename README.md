# YouTube Archivar for Jellyfin

Maintained by [felixfoertsch](https://foerge.de/felixfoertsch/jellyfin-yt-archivar) for YouTube Archivar, the PostgreSQL fork of TubeArchivist. [Fork support](https://foerge.de/felixfoertsch/jellyfin-yt-archivar/issues). Original work by DarkFighterLuke and the upstream TubeArchivist plugin remains attributed; LICENSE is preserved.

## Metadata only

TubeArchivist channels become shows, videos become episodes, and publication years become seasons. Channel thumb, banner and tvart/backdrop and episode thumbnails load through authenticated backend requests. Default episode numbering or YYYYMMDD numbering remains configurable.

There are no progress/watched or playlist synchronization tasks, event handlers, startup actions or controls. Legacy sync flags/usernames/intervals are ignored and cannot reactivate synchronization. The plugin does not change existing history, playlists, playlist order, subscriptions, retention or media. Watch progress lives in Jellyfin. No account linking is needed.

The plugin keeps its GUID `dc97d0c6-28b0-4242-afb4-5833ae1b3715`, URL/key/library/overview/numbering settings and historical catalog versions. Scope providers to the configured TubeArchivist library; mount its media read-only using the channel/video filesystem layout. Configure the library as Shows and enable TubeArchivist metadata/image providers; disable competing providers if unwanted. Seasons derive from episode publication years, not a separate season provider.

## Connection and safety

Configure Collection Title to match the Jellyfin library display name, backend HTTP(S) base URL (including any prefix), your API token, maximum overview length and numbering scheme. HTTPS is recommended; HTTP exposes the token and metadata on the network. Legacy URLs without a scheme retain HTTP behavior. Keys are sent only in request headers to the exact backend origin/base path. Redirects and external images are rejected; no keys appear in image URLs. Clearing/changing the connection invalidates pending client results. Missing/pruned metadata returns no successful replacement; authentication, malformed data and server failures propagate instead of producing valid empty metadata.

Source contracts reviewed against TubeArchivist `20e14a981330ed2d132347aa872684d89c22e74c`: channel/video detail routes, search-processor artwork URLs and protected media authentication. Actual Firefox/Jellyfin/server compatibility is not implied by source review or compilation. Felix owns live metadata refresh/artwork, existing-history/playlist preservation and normal catalog-install QA.

## Build and private delivery

Target: .NET 9 (`net9.0`), Jellyfin.Controller/Model 10.11.0, catalog ABI 10.11.0.0. CI installs pinned SDK 9.0.321. Existing build checks remain mandatory:

```
python3 scripts/test-release.py
dotnet build TubeArchivistMetadata.sln -c Release
python3 scripts/release.py build "$EDGE_TAG" "$GITHUB_SHA" artifacts
python3 scripts/release.py verify "$EDGE_TAG" "$GITHUB_SHA" artifacts
```

Private main pushes publish immutable Gitea `vYYYY.MM.DD.N` edge releases through a separate publisher. ZIP assemblies retain four numeric version components `1.5.<run quotient>.<run remainder>` (base 65535). `build.json` records the exact source revision and SHA-256 of ZIP/catalog artifacts; Jellyfin catalog entries use the ZIP's required MD5 checksum. Historical source manifest entries and release assets are not rewritten.

## Install from the public Jellyfin catalog

Add this repository URL under Jellyfin Dashboard → Plugins → Repositories:

`https://raw.githubusercontent.com/felixfoertsch/jellyfin-yt-archivar/jellyfin-catalog/manifest.json`

Install YouTube Archivar from the catalog and restart when Jellyfin requests it. Updates use the same URL. After publishing each private edge release, the Gitea workflow updates the generated catalog branch. The scheduled mirror copies the branch and release assets separately, without running GitHub Actions. The manifest or ZIP may be temporarily unavailable during replication; retry after the mirror finishes.

Private Gitea releases retain `manifest.json` for existing consumers alongside `catalog.json`, `jellyfin-yt-archivar.zip` and `build.json`. Publication does not install, restart or change any consumer pin.

Before an upgrade, securely back up the plugin directory/configuration and retain the prior immutable ZIP/catalog/version. To roll back, stop Jellyfin and restore that prior plugin/configuration, then restart. Older versions may contain synchronization: disable their sync settings before using them. Do not reset user history or playlists as a rollback step. Felix performs installation and restarts on his own server.
