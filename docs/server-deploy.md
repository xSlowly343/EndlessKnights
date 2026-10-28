# Развёртывание сервера: инструкция для агента

Как агенту развернуть, обновить и проверить сервер игры (Nakama + PostgreSQL + Caddy в Docker) на VPS Сергея с Ubuntu 24.04. Файлы и краткая инструкция для человека — `Server/deploy/README.md`. Версии — только в `Server/deploy/versions.env`.

## 0. Правила
- Секреты не попадают в git, PR, логи, отчёты и чат: `secrets.env`, пароли, ключи Nakama, Steam publisher key. В отчёте пиши «ключ на месте», а не сам ключ. Исключение — `NAKAMA_SERVER_KEY`: его нужно передать человеку для клиента (13.4), и делаешь это только по его просьбе.
- Без явного «да» Сергея нельзя: `restore.sh`, `docker compose down -v`, `docker volume rm`, удаление бэкапов и `secrets.env`, `make-secrets.sh` поверх существующего файла (он и сам откажется), смена домена, `ufw reset`, правка SSH-настроек. Это касается и «чистки» сервера.
- Версии меняются только через `versions.env` и PR. Руками на сервере `docker pull`/`apt install` другой версии не делай.
- Работай от пользователя без root; `sudo` только для `install.sh`, `backup.sh`, `restore.sh`.
- Если что-то не совпадает с этой инструкцией (другая ОС, занят порт 80, нет A-записи), остановись и спроси человека.

## 1. Что спросить у человека перед началом
| Что | Пример | Зачем |
|---|---|---|
| Адрес сервера и SSH-пользователь с `sudo` | `deploy@203.0.113.10` | вход |
| Как зайти: ключ SSH уже добавлен? | — | агент не принимает пароли в чате |
| Домен для игры | `play.riftwarden.example` | HTTPS |
| Почта для Let's Encrypt | `you@example.com` | письма об истечении |
| Ветка или коммит | `main` | что развернуть |
| Steam publisher key (только для 13.4) | — | человек вписывает сам в `secrets.env` |

Проверить DNS до установки:
```bash
dig +short play.riftwarden.example
```
Ответ должен совпасть с IP сервера, иначе Caddy не получит сертификат.

## 2. Первичная установка
```bash
ssh deploy@203.0.113.10
```
На сервере:
```bash
. /etc/os-release && echo "$PRETTY_NAME"
```
Должно быть `Ubuntu 24.04.x LTS`. Иначе стоп, спросить человека.

```bash
sudo apt-get update && sudo apt-get install -y git
git clone <адрес репозитория> ~/riftwarden
cd ~/riftwarden/Server/deploy
git checkout <ветка или коммит>
sudo ./install.sh "$USER"
```
Ожидаемый конец вывода: `Docker version 29.9.2`, `Docker Compose version v5.6.1`, `install: done`. После этого выйди из SSH и зайди снова (группа `docker`).

```bash
cd ~/riftwarden/Server/deploy
./make-secrets.sh <домен> <почта>
./deploy.sh
```
Первый `deploy.sh` идёт 3–5 минут: качает образы, собирает модули, гоняет серверные тесты внутри сборки. Ожидаемый конец: `healthcheck: all good`. Предупреждение `warn  no backup in the last 26 h` после первой установки нормально, первый бэкап сделай сразу:
```bash
sudo ./backup.sh
```

## 3. Проверка ([C] к задаче 13.3)
На сервере:
```bash
./healthcheck.sh
```
Все строки `ok`, последняя `healthcheck: all good`. Код выхода 0.

С любой машины с Node.js 22:
```bash
cd Server
npm run smoke -- https://<домен> <NAKAMA_SERVER_KEY>
```
На самом сервере Node.js нет, там тот же тест запускается в контейнере (из `Server/deploy`):
```bash
docker run --rm -v "$PWD/..":/s -w /s node:22.23.4-alpine node test/smoke.js https://<домен> "$(grep ^NAKAMA_SERVER_KEY secrets.env | cut -d= -f2)"
```
Ожидается 14 строк `ok` и `smoke: all good`:
- healthcheck, вход устройством, приём статистики и отчётов без входа (лишнее поле → 400), `rift_window`;
- создание отряда с кодом, вход друга по коду, оба получают один матч;
- чужак не может войти в матч, создать, листать или открыть группу-отряд;
- вход в матч, второй игрок в Roster, бой идёт по тикам, подделанный урон режется капом (не больше 8 убийств за 2 с);
- `party/current` в хранилище;
- открытый отряд находится поиском и быстрым подбором;
- все вышли, отряд и код удалены. Тестовые аккаунты `smoke-…` остаются в базе, это нормально.

Полная проверка игровым кодом — два бота-друга в одном отряде на 60 с (нужен .NET 10 SDK, с любой машины):
```bash
dotnet run --project Tools/CoreBuild/Game.Sim -c Release -- bot --server https://<домен> --key <NAKAMA_SERVER_KEY> --pair --seconds 60
```
Ожидается «both linked True, same enemy True» и у обоих ботов оплаченные убийства больше нуля. Код выхода 0.

Поиск отряда так же (два терминала): первый бот создаёт открытый отряд, второй находит его через `party_list` и вступает:
```bash
dotnet run --project Tools/CoreBuild/Game.Sim -c Release -- bot --server https://<домен> --key <NAKAMA_SERVER_KEY> --create --open --seconds 60
```
```bash
dotnet run --project Tools/CoreBuild/Game.Sim -c Release -- bot --server https://<домен> --key <NAKAMA_SERVER_KEY> --find --seconds 30
```
Второй печатает «open parties N» со строками отрядов и «link Linked». Флаг `--report cheating` в тестах на боевом сервере не ставить: жалоба ляжет в `party_reports`.

Вывод обеих команд (без ключей) приложи к отчёту или PR.

### Статистика и отчёты об ошибках
```bash
./report.sh telemetry
./report.sh errors 20
```
Первая — сводка анонимной статистики (удержание, время до престижа, нажатия в минуту), вторая — частые ошибки игроков с последним текстом и стеком. В отчёт для Сергея можно вставить вывод целиком: в нём нет ни аккаунтов, ни адресов.

### Жалобы и отряды
Жалобы игроков — консоль → Storage → `party_reports` (ключ `<на кого>:<кто>`, причина, время). Отряды — консоль → Groups (имена `party-…`, в метаданных код, лидер, карточка). Удалять отряды руками без «да» Сергея нельзя.

### Пометки читеров
Хозяин боя пишет игроков, у которых кап срезал урон 20 раз и больше, в хранилище Nakama: консоль → Storage → коллекция `party_flags`. В отчёт для Сергея попадает число пометок и `playerNumber` (SteamID). Автобана нет, решает человек (coop.md §8).

## 4. Обновление
```bash
cd ~/riftwarden
git fetch && git status
git pull --ff-only
cd Server/deploy
sudo ./backup.sh
./deploy.sh
```
`deploy.sh` пересобирает образ, перезапускает изменившиеся контейнеры (Nakama сама накатывает миграции базы при старте) и проверяет здоровье. Если после обновления `healthcheck` красный:
1. `./compose.sh logs --tail 200 nakama` — найти ошибку.
2. Откатить код: `git checkout <прошлый коммит>` и `./deploy.sh`.
3. База после миграций новой Nakama может не подойти старой. Тогда без восстановления не обойтись, а для `restore.sh` нужно «да» Сергея.

## 5. Бэкапы и восстановление
- Таймер `riftwarden-backup.timer` каждый день в 03:17 UTC делает `pg_dump` в `/var/backups/riftwarden/nakama-<дата>.dump` и хранит 14 дней. Проверка: `systemctl list-timers riftwarden-backup.timer`, `journalctl -u riftwarden-backup --since yesterday`.
- Каждый дамп сразу проверяется `pg_restore --list`. Битый дамп не получит имя `.dump` и останется `.partial`.
- Восстановление (только с «да» Сергея): `sudo ./restore.sh <файл.dump> --yes`. Скрипт останавливает Nakama и Caddy, пересоздаёт базу, заливает дамп и поднимает стек.
- Переезд на новый сервер: установка по §2, но вместо `make-secrets.sh` положить старый `secrets.env` (его даёт человек), затем `./deploy.sh`, `sudo ./restore.sh <дамп> --yes`.

## 6. Как менять версии (в облачной сессии, до сервера)
1. Найти новую версию и убедиться, что тег существует:
   - Nakama: `https://api.github.com/repos/heroiclabs/nakama/releases` и образ `registry.heroiclabs.com/heroiclabs/nakama:<версия>`;
   - PostgreSQL, Caddy, Node: теги на Docker Hub (`hub.docker.com/v2/repositories/library/<имя>/tags`). Мажор PostgreSQL не менять без отдельной задачи: нужен `pg_upgrade` или дамп и восстановление;
   - Docker: пакеты в `https://download.docker.com/linux/ubuntu/dists/noble/pool/stable/amd64/`.
2. Поменять строку в `versions.env`. При изменении модулей поднять `SERVER_VERSION` и `version` в `Server/package.json` вместе: сборка падает, если они разные.
3. Поднять стенд локально (в облаке Docker есть, демон запускается командой `dockerd &`):
   ```bash
   cd Server/deploy
   ./make-secrets.sh localhost test@example.com
   ./compose.sh up -d --build --wait
   HEALTHCHECK_INSECURE=1 ./healthcheck.sh
   cd .. && NODE_TLS_REJECT_UNAUTHORIZED=0 npm run smoke -- https://localhost "$(grep ^NAKAMA_SERVER_KEY deploy/secrets.env | cut -d= -f2)"
   ```
   Особенности облака: Docker Hub отвечает 429, образы тянутся с `mirror.gcr.io/library/<имя>` и перетегиваются в обычное имя; npm внутри сборки не доверяет прокси облака, для стенда собирается локальный `node:<версия>` с сертификатом `/root/.ccr/ca-bundle.crt` (`NODE_EXTRA_CA_CERTS`). В репозиторий это не идёт.
4. Стенд остановить и удалить вместе с томами — это твой временный стенд, его можно: `./compose.sh down -v`, удалить свой `secrets.env`.
5. PR с выводом `healthcheck` и `smoke`.

## 7. Частые проблемы
| Симптом | Причина и что делать |
|---|---|
| Caddy в логах `challenge failed` | A-запись не на этот IP или порт 80 закрыт у хостера |
| Nakama перезапускается, в логе `nakama-start: … is empty` | не заполнен ключ в `secrets.env` |
| `Failed initializing runtime modules` | ошибка в `Server/src`; модули должны регистрировать только глобальные функции (`docs/dev-notes.md`, Server) |
| `pg_isready` не проходит | мало памяти: проверить `free -h` и swap |
| `healthcheck`: `warn no backup` | таймер не сработал: `journalctl -u riftwarden-backup` |
| Диск заполнен | `docker system df`; старые образы — `docker image prune` (не `volume prune`!) |

## 8. Отчёт после работы
Записать в `docs/progress.md`: дата, что сделано (установка, обновление, восстановление), коммит, версии из `versions.env`, итог `healthcheck` и `smoke`. Адрес и домен сервера в документы не писать, если Сергей не разрешил. Открытые вопросы — в `docs/todo.md` с ID задачи.
