# Project rules

- deploy: experimental
- Delivery publishes immutable edge releases on private Gitea only. Publication is not installation; do not install plugins or advance consumer pins.
- Preserve historical release tags, assets, and manifest version entries. No stable promotion pipeline is configured.
- Run `python3 scripts/test-release.py` before delivery.
