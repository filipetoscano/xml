#!/bin/bash
# ------------------------------------------------------------------------
set -eux

yell() { echo "$0: $*" >&2; }
die() { yell "$*"; exit 111; }
try() { "$@" || die "cannot $*"; }


#
# Run all commands from the repository root!
# (That's the directory above the current one :)
# ------------------------------------------------------------------------
#
SCRIPT_PATH="${BASH_SOURCE[0]}"
if ([ -h "${SCRIPT_PATH}" ]); then
  while([ -h "${SCRIPT_PATH}" ]); do cd "$(dirname "$SCRIPT_PATH")";
  SCRIPT_PATH=$(readlink "${SCRIPT_PATH}"); done
fi
cd "$(dirname ${SCRIPT_PATH})" > /dev/null
cd ..


#
# Ensure env
# ------------------------------------------------------------------------
STAGE=${1:-}

if [[ ${STAGE} != build && ${STAGE} != publish ]]; then die "usage: $0 build|publish"; fi

if [ -z ${GITHUB_REF+x} ];      then die "GITHUB_REF is not set"; fi

if [[ ${GITHUB_REF} != refs/tags/v* ]]; then die "Script only works for tags"; fi

export VERSION=${GITHUB_REF##*/v}
echo ${VERSION}


#
# Build and pack
#
# Runs before the nuget.org API key exists: restore and build execute code
# from third-party packages, which must never see the key.
# ------------------------------------------------------------------------
if [[ ${STAGE} == build ]]; then

dotnet clean   -c Release
dotnet restore --packages .nuget
dotnet build   -c Release --no-restore -p:Version=${VERSION}

mkdir -p nupkg
rm -f nupkg/*.*

dotnet pack    -c Release --no-restore --no-build src/Lefty.Xml           -o nupkg -p:Version=${VERSION}

exit 0
fi


#
# Publish to nuget.org
#
# NUGET_APIKEY is the short-lived (1 hour) key which trusted publishing
# issues to the workflow, through the NuGet/login action.
#
# Tracing is off while the secrets are in play: it would echo their values
# into the build log, even from a test such as [ -z "$NUGET_APIKEY" ].
# ------------------------------------------------------------------------
set +x

if [ -z "${GITHUB_TOKEN:-}" ];  then die "GITHUB_TOKEN is not set"; fi
if [ -z "${NUGET_APIKEY:-}" ];  then die "NUGET_APIKEY is not set"; fi

if ! ls nupkg/*.nupkg > /dev/null 2>&1; then die "no packages in nupkg/: run '$0 build' first"; fi

echo "+ dotnet nuget push nupkg/*.nupkg --api-key *** --source=https://api.nuget.org/v3/index.json"
dotnet nuget push "nupkg/*.nupkg" --api-key "${NUGET_APIKEY}" --source=https://api.nuget.org/v3/index.json

set -x


#
# Release, including artifacts
# ------------------------------------------------------------------------

gh release create v${VERSION} --notes="Release v${VERSION}"

# eof