#!/bin/sh
set -eu

SCRIPT_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec /bin/sh "$SCRIPT_ROOT/build-current-platform.sh" "$@" --open-installer
