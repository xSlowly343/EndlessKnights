#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target="$here/secrets.env"

if [ $# -ne 2 ]; then
    echo "usage: make-secrets.sh <domain> <email for Let's Encrypt>" >&2
    exit 1
fi
if [ -e "$target" ]; then
    echo "make-secrets: $target already exists, nothing changed" >&2
    exit 1
fi

random() { openssl rand -hex "$1"; }

umask 077
sed \
    -e "s|^DOMAIN=.*|DOMAIN=$1|" \
    -e "s|^ACME_EMAIL=.*|ACME_EMAIL=$2|" \
    -e "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(random 24)|" \
    -e "s|^NAKAMA_SERVER_KEY=.*|NAKAMA_SERVER_KEY=$(random 16)|" \
    -e "s|^NAKAMA_HTTP_KEY=.*|NAKAMA_HTTP_KEY=$(random 24)|" \
    -e "s|^NAKAMA_SESSION_KEY=.*|NAKAMA_SESSION_KEY=$(random 32)|" \
    -e "s|^NAKAMA_REFRESH_KEY=.*|NAKAMA_REFRESH_KEY=$(random 32)|" \
    -e "s|^NAKAMA_CONSOLE_PASSWORD=.*|NAKAMA_CONSOLE_PASSWORD=$(random 16)|" \
    -e "s|^NAKAMA_CONSOLE_SIGNING_KEY=.*|NAKAMA_CONSOLE_SIGNING_KEY=$(random 32)|" \
    "$here/secrets.env.example" > "$target"
echo "make-secrets: wrote $target"
