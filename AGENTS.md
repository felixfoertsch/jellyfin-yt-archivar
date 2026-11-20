# Project rules

- deploy: experimental
- Delivery publishes immutable edge releases on private Gitea only. Publication is not installation; do not install plugins or advance consumer pins.
- Keep the source manifest limited to YouTube Archivar. Preserve its existing version entries and published release tags and assets. No stable promotion pipeline is configured.
- Keep upstream attribution in the README and preserve `LICENSE`. Use the Gitea issue tracker for support.
- Run `python3 scripts/test-release.py`, `dotnet build YouTubeArchivar.sln -c Release`, and `dotnet run --file scripts/test-configuration.cs -c Release` before delivery.
