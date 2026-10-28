#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ $# -ne 2 ] || [ "$2" != "--yes" ]; then
    echo "usage: restore.sh <file.dump> --yes" >&2
    echo "the current database is replaced by the dump" >&2
    exit 1
fi
dump="$1"
[ -f "$dump" ] || { echo "restore: no file $dump" >&2; exit 1; }

"$here/compose.sh" exec -T postgres pg_restore --list < "$dump" > /dev/null
"$here/compose.sh" stop nakama caddy
"$here/compose.sh" exec -T postgres sh -c 'dropdb -U "$POSTGRES_USER" --if-exists "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
"$here/compose.sh" exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner --exit-on-error' < "$dump"
"$here/compose.sh" up -d --wait
echo "restore: database restored from $dump"
