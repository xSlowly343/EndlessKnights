# Развёртывание сервера (Nakama + PostgreSQL) на Ubuntu

Сервер игры на своём VPS: Nakama с нашими модулями, PostgreSQL и Caddy, который даёт HTTPS-сертификат Let's Encrypt. Всё работает в Docker. Пошаговая инструкция для агента, который разворачивает по SSH, лежит в `docs/server-deploy.md`.

## Версии
Все версии в одном файле `versions.env`, другие файлы их не дублируют:

| Что | Версия |
|---|---|
| Ubuntu Server | 24.04 LTS (на другой версии `install.sh` остановится) |
| Docker Engine | 29.9.2 |
| Docker Compose | 5.6.1 |
| Nakama | 3.41.0 (образ `registry.heroiclabs.com/heroiclabs/nakama`) |
| PostgreSQL | 16.15 (alpine) |
| Caddy | 2.11.7 (alpine) |
| Node.js для сборки модулей | 22.23.4 (только внутри сборки образа, на сервер не ставится) |
| TypeScript | 6.0.2 |
| Наши модули (`SERVER_VERSION`) | 0.10.0, совпадает с `Server/package.json` (сборка проверяет) |

Обновление: поменять строку в `versions.env`, проверить на тестовом стенде (`docs/server-deploy.md` §6), закоммитить, на сервере `git pull` и `./deploy.sh`. Пакеты Docker после установки закреплены (`apt-mark hold`), поэтому `apt upgrade` их не обновит. Новую версию Docker ставит повторный запуск `install.sh`.

## Что нужно заранее
- VPS с Ubuntu 24.04, 2 ядра, 2 ГБ памяти, 20 ГБ диска или больше. Нужен вход по SSH пользователем с `sudo`.
- Домен или поддомен (например, `play.riftwarden.example`), A-запись которого указывает на IP сервера. Без неё Let's Encrypt не выдаст сертификат.
- Открытые порты 80 и 443 у хостера (если у него есть свой файрвол).
- Почта для писем Let's Encrypt.
- Позже, для входа через Steam (13.4): Steam Web API publisher key. До своего App ID работает тестовый 480.

## Установка с нуля
```bash
sudo apt-get update && sudo apt-get install -y git
git clone <адрес репозитория> ~/riftwarden
cd ~/riftwarden/Server/deploy
sudo ./install.sh "$USER"
```
`install.sh` ставит Docker закреплённой версии, создаёт swap 2 ГБ (`SWAP_SIZE=4G sudo -E ./install.sh` — другой размер), включает ufw (SSH, 80, 443) и ежедневный таймер бэкапа в 03:17 UTC. После него выйди из SSH и зайди снова, чтобы заработала группа `docker`.

```bash
cd ~/riftwarden/Server/deploy
./make-secrets.sh play.riftwarden.example you@example.com
./deploy.sh
```
`make-secrets.sh` пишет `secrets.env` со случайными ключами. Файл в `.gitignore` и с правами 600: его нельзя коммитить и пересылать. Сохрани копию в менеджере паролей: без `NAKAMA_SESSION_KEY` и пароля базы восстановление из бэкапа на новом сервере не заработает.

`deploy.sh` собирает образ Nakama с нашими модулями (во время сборки идут серверные тесты), поднимает стек и запускает `healthcheck.sh`.

## Проверка
```bash
./healthcheck.sh
```
Проверяет, что три контейнера живы, версии Nakama и модулей совпадают с `versions.env`, `https://<домен>/healthcheck` отвечает, модуль отвечает на RPC `rift_window`, консоль доступна локально, swap включён, бэкап моложе 26 часов. Если всё в порядке, последняя строка — `healthcheck: all good`.

Полная проверка как у игрока (вход, RPC, матч отряда из двух игроков, кап подделанного урона, хранилище) — с любого ПК с Node.js 22:
```bash
cd Server
npm run smoke -- https://play.riftwarden.example <NAKAMA_SERVER_KEY>
```

## Консоль Nakama
Наружу консоль не открыта. Через SSH-туннель:
```bash
ssh -L 7351:127.0.0.1:7351 user@сервер
```
Дальше в браузере `http://localhost:7351`, логин и пароль — `NAKAMA_CONSOLE_USER` и `NAKAMA_CONSOLE_PASSWORD` из `secrets.env`.

## Каждодневное
| Задача | Команда |
|---|---|
| Состояние | `./compose.sh ps` |
| Логи Nakama | `./compose.sh logs -f --tail 200 nakama` |
| Перезапуск | `./compose.sh restart nakama` |
| Обновить после `git pull` | `./deploy.sh` |
| Бэкап сейчас | `sudo ./backup.sh` |
| Список бэкапов | `ls -lh /var/backups/riftwarden` |
| Когда был бэкап по таймеру | `systemctl list-timers riftwarden-backup.timer` |
| Восстановить из бэкапа (база заменяется целиком) | `sudo ./restore.sh /var/backups/riftwarden/nakama-<дата>.dump --yes` |

Бэкапы лежат на том же сервере 14 дней (`BACKUP_KEEP_DAYS`). Копию на другую машину пока делать руками (`scp`), куда копировать автоматически — решение Сергея (`docs/todo.md`, 13.3).

## Что где
| Файл | Зачем |
|---|---|
| `versions.env` | все версии |
| `secrets.env.example` | список секретов; настоящий `secrets.env` создаёт `make-secrets.sh` |
| `docker-compose.yml` | PostgreSQL, Nakama, Caddy; наружу только 80 и 443, консоль на 127.0.0.1:7351 |
| `Dockerfile`, `Dockerfile.dockerignore` | образ Nakama с `index.js` из `Server/`; правила баланса берутся из `Assets/StreamingAssets/Data` |
| `nakama.yml` | настройки Nakama без секретов |
| `nakama-start.sh` | старт в контейнере: проверяет секреты, пишет их во временный конфиг, накатывает миграции базы |
| `Caddyfile` | HTTPS и проксирование на Nakama (HTTP и WebSocket); пути приёма `/intake/telemetry` и `/intake/errors` с подстановкой `http_key` |
| `install.sh` | подготовка чистой Ubuntu 24.04 |
| `make-secrets.sh`, `deploy.sh`, `healthcheck.sh`, `backup.sh`, `restore.sh`, `compose.sh` | см. выше; `compose.sh` — это `docker compose` с нашими файлами |
| `report.sh` | сводка анонимной статистики (`report.sh telemetry`) и частые ошибки игроков (`report.sh errors 20`) |
| `../config/intake.json` | сроки хранения и лимиты приёма статистики и отчётов |
| `systemd/` | служба и таймер ежедневного бэкапа |
