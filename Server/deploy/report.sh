#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$here/secrets.env"
curl_tls=()
[ "${REPORT_INSECURE:-0}" = "1" ] && curl_tls=(--insecure)

case "${1:-}" in
    telemetry) rpc=telemetry_summary; body='{}' ;;
    errors) rpc=error_report_list; body="{\"limit\": ${2:-20}}" ;;
    *) echo "usage: report.sh telemetry | errors [how many groups]" >&2; exit 1 ;;
esac

curl -fsS "${curl_tls[@]}" --max-time 60 -X POST -H 'Content-Type: application/json' --data "$body" \
    "https://$DOMAIN/v2/rpc/$rpc?http_key=$NAKAMA_HTTP_KEY&unwrap" | python3 -m json.tool --no-ensure-ascii
