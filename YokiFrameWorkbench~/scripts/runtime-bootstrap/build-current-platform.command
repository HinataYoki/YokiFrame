#!/bin/sh
set -eu

SCRIPT_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_ROOT=
OPEN_INSTALLER=0
while [ "$#" -gt 0 ]; do
    case "$1" in
        --project)
            if [ "$#" -lt 2 ]; then
                echo "Missing value for --project." >&2
                exit 2
            fi
            PROJECT_ROOT=$2
            shift 2
            ;;
        --open-installer)
            OPEN_INSTALLER=1
            shift
            ;;
        *)
            echo "Usage: $(basename "$0") --project <UnityOrGodotProjectRoot> [--open-installer]" >&2
            exit 2
            ;;
    esac
done

if [ -z "$PROJECT_ROOT" ]; then
    echo "Missing required --project <UnityOrGodotProjectRoot>." >&2
    exit 2
fi

WORKBENCH_ROOT=$(CDPATH= cd -- "$SCRIPT_ROOT/../.." && pwd)
PACKAGE_ROOT=$(CDPATH= cd -- "$WORKBENCH_ROOT/.." && pwd)
PACKAGING_PROJECT="$WORKBENCH_ROOT/src/YokiFrame.Packaging/YokiFrame.Packaging.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET 10 SDK is required to build the YokiFrame project Runtime cache." >&2
    exit 1
fi

if [ "$OPEN_INSTALLER" -eq 1 ]; then
    exec dotnet run --project "$PACKAGING_PROJECT" -- runtime bootstrap --package-root "$PACKAGE_ROOT" --project-root "$PROJECT_ROOT" --configuration Release --open-installer
fi

exec dotnet run --project "$PACKAGING_PROJECT" -- runtime bootstrap --package-root "$PACKAGE_ROOT" --project-root "$PROJECT_ROOT" --configuration Release
