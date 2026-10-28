#!/bin/sh
set -eu

require() {
    eval "value=\${$1:-}"
    if [ -z "$value" ]; then
        echo "nakama-start: $1 is empty, fill it in secrets.env" >&2
        exit 1
    fi
    case "$value" in
        *[!A-Za-z0-9._-]*)
            echo "nakama-start: $1 may contain only letters, digits and . _ -" >&2
            exit 1
            ;;
    esac
}

for name in POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD NAKAMA_SERVER_KEY NAKAMA_HTTP_KEY NAKAMA_SESSION_KEY \
    NAKAMA_REFRESH_KEY NAKAMA_CONSOLE_USER NAKAMA_CONSOLE_PASSWORD NAKAMA_CONSOLE_SIGNING_KEY STEAM_APP_ID; do
    require "$name"
done

database="$POSTGRES_USER:$POSTGRES_PASSWORD@postgres:5432/$POSTGRES_DB"
secrets=/tmp/nakama-secrets.yml
umask 077
cat > "$secrets" <<YAML
database:
  address:
    - "$database"
socket:
  server_key: "$NAKAMA_SERVER_KEY"
session:
  encryption_key: "$NAKAMA_SESSION_KEY"
  refresh_encryption_key: "$NAKAMA_REFRESH_KEY"
runtime:
  http_key: "$NAKAMA_HTTP_KEY"
console:
  username: "$NAKAMA_CONSOLE_USER"
  password: "$NAKAMA_CONSOLE_PASSWORD"
  signing_key: "$NAKAMA_CONSOLE_SIGNING_KEY"
social:
  steam:
    app_id: $STEAM_APP_ID
YAML
if [ -n "${STEAM_PUBLISHER_KEY:-}" ]; then
    require STEAM_PUBLISHER_KEY
    printf '    publisher_key: "%s"\n' "$STEAM_PUBLISHER_KEY" >> "$secrets"
fi

/nakama/nakama migrate up --database.address "$database"
exec /nakama/nakama --config /nakama/data/nakama.yml --config "$secrets"
