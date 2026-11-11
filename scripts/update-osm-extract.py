#!/usr/bin/env python3
"""Updates the OpenStreetMap extract that Nominatim (geocoding) and ORS (routing) import.

Downloads a fresh Geofabrik extract to data/osm/region.osm.pbf and removes the two Docker
volumes that hold the previous import, so the next AppHost start re-imports the new data.
Run this while the AppHost is stopped (`aspire stop`); the volumes cannot be removed while
the containers still use them.

The re-import happens on the next start and takes a while: minutes for a small region,
roughly 1.5 hours for Oberbayern, several hours for all of Bavaria.

Usage: python3 scripts/update-osm-extract.py [--region oberbayern] [--url URL] [--keep-volumes]
"""
import argparse
import datetime
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TARGET = REPO_ROOT / "data" / "osm" / "region.osm.pbf"
VOLUMES = ("transbrain-ors", "transbrain-nominatim")

# Shortcuts for common Geofabrik extracts; any sub-region URL from download.geofabrik.de works.
REGIONS = {
    "oberbayern": "https://download.geofabrik.de/europe/germany/bayern/oberbayern-latest.osm.pbf",
    "bayern": "https://download.geofabrik.de/europe/germany/bayern-latest.osm.pbf",
    "germany": "https://download.geofabrik.de/europe/germany-latest.osm.pbf",
}


def fail(message):
    sys.exit(f"ERROR: {message}")


def apphost_running():
    """True when an Aspire AppHost (and therefore the map containers) is still up."""
    try:
        result = subprocess.run(
            ["aspire", "ps", "--format", "Json"], capture_output=True, text=True, timeout=30)
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return None  # CLI unavailable: cannot tell, volume removal will be the guard.
    output = result.stdout.strip()
    return bool(output) and output not in ("[]", "null")


def download(url):
    print(f"Downloading {url} ...")
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    temp = TARGET.with_suffix(".pbf.tmp")

    def report(blocks, block_size, total):
        done = blocks * block_size
        if total > 0:
            print(f"\r  {done / 1048576:,.0f} / {total / 1048576:,.0f} MiB", end="", flush=True)

    try:
        urllib.request.urlretrieve(url, temp, reporthook=report)
    except (urllib.error.URLError, OSError) as error:
        temp.unlink(missing_ok=True)
        fail(f"download failed: {error}")
    print()
    if temp.stat().st_size < 1_000_000:
        temp.unlink()
        fail("the downloaded file is suspiciously small - wrong URL?")
    temp.replace(TARGET)
    print(f"Saved {TARGET} ({TARGET.stat().st_size / 1048576:,.0f} MiB).")


def remove_volumes():
    """Removes the import volumes; Docker refuses while a container still uses them."""
    for volume in VOLUMES:
        exists = subprocess.run(
            ["docker", "volume", "inspect", volume], capture_output=True, text=True)
        if exists.returncode != 0:
            print(f"Volume {volume} does not exist - nothing to remove.")
            continue
        removed = subprocess.run(
            ["docker", "volume", "rm", volume], capture_output=True, text=True)
        if removed.returncode != 0:
            fail(
                f"could not remove volume {volume}: {removed.stderr.strip()}\n"
                "Stop the AppHost first (`aspire stop`), then re-run this script "
                "or remove the volumes manually:\n"
                f"  docker volume rm {' '.join(VOLUMES)}")
        print(f"Removed volume {volume} - the next start re-imports it.")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--region", choices=sorted(REGIONS), default="oberbayern",
                        help="Geofabrik extract to download (default: oberbayern)")
    parser.add_argument("--url", help="full extract URL, overrides --region")
    parser.add_argument("--keep-volumes", action="store_true",
                        help="only replace the file, keep the current import")
    args = parser.parse_args()

    if apphost_running():
        fail("an Aspire AppHost is still running. Stop it first: aspire stop")

    download(args.url or REGIONS[args.region])

    if args.keep_volumes:
        print("Volumes kept: the services continue to serve the OLD data until you remove")
        print(f"  {' and '.join(VOLUMES)} and restart the AppHost.")
    else:
        remove_volumes()

    print()
    print(f"Done ({datetime.date.today().isoformat()}). Next steps:")
    print("  1. aspire start   (or start the AppHost from the IDE)")
    print("  2. The first start re-imports the extract - minutes to hours depending on size.")
    print("  3. The geodata page in both web apps shows the new OSM data date once ready.")


if __name__ == "__main__":
    main()
