#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dir="${BACKUP_DIR:-/var/backups/riftwarden}"
keep_days="${BACKUP_KEEP_DAYS:-14}"

umask 027
mkdir -p "$dir"
if getent group docker > /dev/null; then
    chgrp docker "$dir"
    chmod 2750 "$dir"
else
    chmod 700 "$dir"
fi
file="$dir/nakama-$(date -u +%Y%m%d-%H%M%S).dump"
partial="$file.partial"

"$here/compose.sh" exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$partial"
"$here/compose.sh" exec -T postgres pg_restore --list < "$partial" > /dev/null
mv "$partial" "$file"
find "$dir" -maxdepth 1 -name 'nakama-*.dump' -mtime +"$keep_days" -delete
echo "backup: $file ($(du -h "$file" | cut -f1))"
