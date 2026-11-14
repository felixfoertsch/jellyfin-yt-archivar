#!/usr/bin/env bash
set -euo pipefail

# Description: Build a deterministic Jellyfin plugin release zip.
# Usage: ./scripts/package-release.sh

main() {
	local root
	root="$(git rev-parse --show-toplevel)"
	cd "$root"

	local version
	version="$(awk -F'\"' '/^version:/ { print $2 }' build.yaml)"

	local dll_path
	dll_path="Jellyfin.Plugin.TubeArchivistMetadata/bin/Release/net9.0/Jellyfin.Plugin.TubeArchivistMetadata.dll"
	if [[ ! -f "$dll_path" ]]; then
		echo "Missing release DLL: $dll_path" >&2
		exit 1
	fi

	local output_dir
	output_dir="artifacts"
	mkdir -p "$output_dir"

	local zip_path
	zip_path="$output_dir/tubearchivistmetadata_${version}.zip"
	rm -f "$zip_path"

	local package_dir
	package_dir="$(mktemp -d)"
	trap 'rm -rf "'"$package_dir"'"' EXIT

	cp "$dll_path" "$package_dir/Jellyfin.Plugin.TubeArchivistMetadata.dll"
	TZ=UTC touch -t 198001010000 "$package_dir/Jellyfin.Plugin.TubeArchivistMetadata.dll"
	(
		cd "$package_dir"
		TZ=UTC zip -X -q "$root/$zip_path" Jellyfin.Plugin.TubeArchivistMetadata.dll
	)

	local entry_count
	entry_count="$(zipinfo -1 "$zip_path" | wc -l | tr -d ' ')"
	if [[ "$entry_count" != "1" ]]; then
		echo "Unexpected zip entry count: $entry_count" >&2
		zipinfo -1 "$zip_path" >&2
		exit 1
	fi

	local entry_name
	entry_name="$(zipinfo -1 "$zip_path")"
	if [[ "$entry_name" != "Jellyfin.Plugin.TubeArchivistMetadata.dll" ]]; then
		echo "Unexpected zip entry: $entry_name" >&2
		exit 1
	fi

	md5sum "$zip_path" | cut -d' ' -f1
}

main "$@"
