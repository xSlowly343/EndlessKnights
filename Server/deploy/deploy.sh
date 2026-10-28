#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$here/compose.sh" build --pull nakama
"$here/compose.sh" up -d --remove-orphans --wait
"$here/healthcheck.sh"
