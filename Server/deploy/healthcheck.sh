#!/bin/bash
set -uo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$here/versions.env"
source "$here/secrets.env"
backup_dir="${BACKUP_DIR:-/var/backups/riftwarden}"
curl_tls=()
[ "${HEALTHCHECK_INSECURE:-0}" = "1" ] && curl_tls=(--insecure)
failed=0

pass() { echo "ok    $1"; }
fail() { echo "FAIL  $1"; failed=1; }
warn() { echo "warn  $1"; }
check() { local name="$1"; shift; if "$@" > /dev/null 2>&1; then pass "$name"; else fail "$name"; fi; }

compose() { "$here/compose.sh" "$@"; }

for service in postgres nakama caddy; do
    state="$(compose ps --format '{{.Health}}' "$service" 2>/dev/null)"
    case "$state" in
        healthy) pass "$service healthy" ;;
        "") if [ -n "$(compose ps --status running -q "$service" 2>/dev/null)" ]; then pass "$service running"; else fail "$service not running"; fi ;;
        *) fail "$service $state" ;;
    esac
done

nakama_version="$(compose exec -T nakama /nakama/nakama --version 2>/dev/null | tr -d '\r')"
if [[ "$nakama_version" == "$NAKAMA_VERSION"* ]]; then pass "nakama $nakama_version"; else fail "nakama version '$nakama_version', expected $NAKAMA_VERSION"; fi

image="$(compose images -q nakama 2>/dev/null | head -1)"
server_version="$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.version"}}' "$image" 2>/dev/null)"
if [ "$server_version" = "$SERVER_VERSION" ]; then pass "server modules $server_version"; else fail "server modules '$server_version', expected $SERVER_VERSION"; fi

check "nakama healthcheck inside the container" compose exec -T nakama /nakama/nakama healthcheck
check "https://$DOMAIN/healthcheck" curl -fsS "${curl_tls[@]}" --max-time 10 "https://$DOMAIN/healthcheck"

rift="$(curl -fsS "${curl_tls[@]}" --max-time 10 "https://$DOMAIN/v2/rpc/rift_window?http_key=$NAKAMA_HTTP_KEY&unwrap" 2>/dev/null)"
if [[ "$rift" == *serverUnixMs* ]]; then pass "module answers rpc rift_window"; else fail "rpc rift_window: '$rift'"; fi

intake="$(curl -s "${curl_tls[@]}" --max-time 10 -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' --data '{}' "https://$DOMAIN/intake/telemetry" 2>/dev/null)"
if [ "$intake" = "400" ]; then pass "intake checks packets (junk -> 400)"; else fail "intake /intake/telemetry answered '$intake', expected 400"; fi

check "console on 127.0.0.1:7351" curl -fsS --max-time 10 -o /dev/null http://127.0.0.1:7351/

if swapon --show --noheadings 2>/dev/null | grep -q .; then pass "swap on"; else warn "swap off (install.sh turns it on)"; fi

latest="$(find "$backup_dir" -maxdepth 1 -name 'nakama-*.dump' -mmin -1560 2>/dev/null | head -1)"
if [ -n "$latest" ]; then pass "backup in the last 26 h"; else warn "no backup in the last 26 h in $backup_dir"; fi

if [ "$failed" -eq 0 ]; then echo "healthcheck: all good"; else echo "healthcheck: problems found"; fi
exit "$failed"
