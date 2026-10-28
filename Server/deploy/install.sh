#!/bin/bash
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$here/versions.env"
swap_size="${SWAP_SIZE:-2G}"
deploy_user="${1:-}"

[ "$(id -u)" -eq 0 ] || { echo "install: run as root (sudo)" >&2; exit 1; }
source /etc/os-release
if [ "${ID:-}" != "ubuntu" ] || [ "${VERSION_ID:-}" != "$UBUNTU_VERSION" ]; then
    echo "install: needs Ubuntu $UBUNTU_VERSION, found ${PRETTY_NAME:-unknown}" >&2
    exit 1
fi

install_docker() {
    local suffix="-1~ubuntu.${UBUNTU_VERSION}~${VERSION_CODENAME}"
    apt-get update
    apt-get install -y ca-certificates curl openssl ufw
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
        > /etc/apt/sources.list.d/docker.list
    apt-get update
    apt-mark unhold docker-ce docker-ce-cli docker-compose-plugin > /dev/null 2>&1 || true
    apt-get install -y --allow-downgrades \
        "docker-ce=5:${DOCKER_VERSION}${suffix}" \
        "docker-ce-cli=5:${DOCKER_VERSION}${suffix}" \
        "docker-compose-plugin=${DOCKER_COMPOSE_VERSION}${suffix}" \
        containerd.io docker-buildx-plugin
    apt-mark hold docker-ce docker-ce-cli docker-compose-plugin
    systemctl enable --now docker
}

make_swap() {
    if swapon --show --noheadings | grep -q .; then
        echo "install: swap already on"
    else
        fallocate -l "$swap_size" /swapfile
        chmod 600 /swapfile
        mkswap /swapfile
        swapon /swapfile
        grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
    fi
    echo 'vm.swappiness=10' > /etc/sysctl.d/99-riftwarden.conf
    sysctl --system > /dev/null
}

open_firewall() {
    ufw allow OpenSSH
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw allow 443/udp
    ufw --force enable
}

install_backup_timer() {
    sed "s|@DEPLOY_DIR@|$here|g" "$here/systemd/riftwarden-backup.service" > /etc/systemd/system/riftwarden-backup.service
    cp "$here/systemd/riftwarden-backup.timer" /etc/systemd/system/riftwarden-backup.timer
    systemctl daemon-reload
    systemctl enable --now riftwarden-backup.timer
}

install_docker
make_swap
open_firewall
install_backup_timer
if [ -n "$deploy_user" ]; then
    usermod -aG docker "$deploy_user"
    echo "install: $deploy_user added to the docker group, log in again to use it"
fi
docker --version
docker compose version
echo "install: done. Next: make-secrets.sh <domain> <email>, then deploy.sh"
