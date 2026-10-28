#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ ! -f "$here/secrets.env" ]; then
    echo "compose: no $here/secrets.env, run make-secrets.sh first" >&2
    exit 1
fi
exec docker compose --project-directory "$here" -f "$here/docker-compose.yml" \
    --env-file "$here/versions.env" --env-file "$here/secrets.env" "$@"
