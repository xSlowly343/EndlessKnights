# Сервер Nakama

Модули Nakama на TypeScript. Сейчас здесь хозяин боя отряда (authoritative match `party`, задача 13.28).

## Сборка и тесты
Нужен Node.js 22 и TypeScript 6 (`npm i -g typescript`), точные версии — в `deploy/versions.env`. Зависимостей npm нет, типы Nakama описаны в `src/nakama.d.ts` (только то, что используется).

```
cd Server
npm test
```

`npm run build` собирает `build/index.js`: TypeScript в один файл ES5 (так его грузит Nakama) и в начало файла правила из `Assets/StreamingAssets/Data/party.json` и `stages.json`. Цифр баланса в коде сервера нет.

Тесты (`node --test`) прогоняют:
- `test/encounter.test.js` — сценарии из `test-vectors/party-encounter.json`, шаг за шагом сверяют состояние боя с C#;
- `test/codec.test.js` — байты пакетов из `test-vectors/party-codec.json`;
- `test/host.test.js` — приём пакетов, дубликаты, предел сообщений, пересылка `PartyOffer`;
- `test/match.test.js` — обработчики матча, когда состояние между вызовами проходит через Go, как в настоящей Nakama (копия через JSON): пакеты должны совпасть с хозяином без копирования байт в байт.

`npm run smoke -- https://<сервер> <server key>` — проверка живого сервера как у игрока: вход, оба RPC, матч из двух игроков, хранилище (`test/smoke.js`, в `npm test` не входит).

Тест-векторы пишет C# (`PartyVectorTests`, `PartyCodecVectorTests` в `Assets/Tests/Core.Tests/Party`). После изменения `PartyEncounter`, кодека или правил запусти тесты Core с `UPDATE_PARTY_VECTORS=1`, закоммить новые файлы и прогони `npm test`. Без переменной C#-тест падает, если векторы устарели.

## Установка в Nakama
Сервер разворачивается из `deploy/` (Docker, Ubuntu 24.04): `deploy/README.md` для человека, `docs/server-deploy.md` для агента. Образ сам собирает `build/index.js` и кладёт его в `/nakama/data/modules`, `runtime.js_entrypoint: "index.js"`.

- Отряд (13.11б, coop.md §2.2–2.3). Все RPC берут JSON и возвращают JSON. Ошибки — текстом: `already in a party`, `not in a party`, `party is full`, `unknown code`, `party not found`, `only the leader can do that`, `not a member`, `bad request`.

  | RPC | Запрос | Ответ |
  |---|---|---|
  | `party_create` | `{ access?: "invite"\|"open", title?, language?, wanted? }` | вид отряда: `{ id, code, access, title, language, wanted, leader, maxMembers, matchId, members: [{ userId, name, steamId, leader, slot }] }`, `slot` — место в живом матче отряда или −1 |
  | `party_mine` | `{}` | `{ party: вид \| null }` |
  | `party_connect` | `{}` | `{ matchId }` — живой матч отряда, при необходимости новый |
  | `party_join` | `{ code }` (любой регистр, с дефисом) или `{ partyId }` (только открытый) | вид |
  | `party_leave` | `{}` | `{ party: null }` |
  | `party_kick`, `party_promote` | `{ userId }` (только лидер) | вид |
  | `party_update` | `{ access?, title?, language?, wanted? }` (только лидер) | вид |
  | `party_list` | `{ language?, role?, wanted?, slot?, utcOffsetMinutes? }` | `{ parties: [{ id, title, language, wanted, members, maxMembers, activeNow, usuallyOnline, wantsYou }] }`, без кодов |
  | `party_quick` | `{ language, role, utcOffsetMinutes? }` | вид (вступил или создал открытый) |
  | `party_block`, `party_unblock` | `{ userId }` | `{ blocked: [...] }` |
  | `party_report` | `{ userId, reason }` (`search.reportReasons`) | `{ ok: true }` |
  | `rift_board` | `{ riftId }` | `{ riftId, endUnixMs, settled, parties, top: [{ place, title, depth, members, yours }], own: { place, depth, reward } \| null }` (первые `board.shown`) |
  | `mail_list` | `{}` | `{ letters: [{ id, kind, at, place, of, depth, title, reward: { shards, uncutGems, ingots }, claimed, claimedAt }], unread }` (невзятые первыми, до 50) |
  | `mail_claim` | `{ id }` | `{ letter }`; ошибки `no such letter`, `already claimed` — каждое письмо забирается один раз |
  | `party_chronicle` | `{}` | `{ partyId, banner, titles, stats: { bossesTogether, hoursTogether, fullOrchestra }, feats: [{ id, kind, tracked, levels, level, at, progress }], records: [{ at, kind, name, feat, level }] }` (новые записи первыми) |

  Значения `title`, `language`, `wanted`, `role`, `slot` берутся из `party.json` → `search`.
- Хранилище: `party_codes/<код>` и `party_match/<partyId>` у системного пользователя, `party/current` у игрока (`{ partyId, matchId, slot, seen }`), `party_blocks/list` у игрока, `party_reports/<кого>:<кто>` у системного пользователя, `party_chronicle/<partyId>` (Хроника отряда) у системного пользователя, `party_feats/<partyId>` (копия подвигов отряда) у каждого участника, `rift_party/open` (общий Разлом, в который сейчас входят отряды: id, окно, начало, конец, вход до), `rift_board/<riftId>` (таблица Разлома) и `rift_board/pending` (Разломы без итога) у системного пользователя, `mail/<id>` (письма с наградами, итог Разлома — `rift-<riftId>`) у каждого игрока.
- Клиентские вызовы групп (создать группу с именем `party-…`, вступить, выйти, исключить, повысить, изменить, удалить отряд, листать группы) закрыты before-хуками: отрядом управляют только RPC.
- RPC `rift_window` (без аргументов) отдаёт `{ serverUnixMs, open, next, announced }`: открытое окно Разлома (или `null`), следующее окно и их модификаторы по `rift.json`.
- Клиент входит в матч с метаданными `{ "playerNumber": "<SteamID64>" }`: это число попадает в `Roster`, по нему клиент находит свой слот. Матч отряда пускает только участников этого отряда.
- Пакеты те же, что у `LoopbackTransport`: первый байт — вид сообщения, opCode равен ему же.
- Состав отряда игрока пишется в хранилище: коллекция `party`, ключ `current`, `{ matchId, slot }`.
- Капы урона и ударов (`party.json` → `caps`, coop.md §8): срезанный урон не засчитывается. Каждые 20 пометок игрока пишутся в лог и в хранилище: коллекция `party_flags`, ключ — id игрока, `{ flags, matchId, playerNumber, at }`, читается только из консоли.
- В `EnemyKilled` последним байтом идёт маска награды (бит на слот), в `Roster` — запас отдыха каждого слота в секундах (coop.md §3.5).

## Приём статистики и отчётов (14.1, 14.2)
Клиент шлёт JSON без входа в аккаунт на `https://<сервер>/intake/telemetry` и `/intake/errors`. Caddy переписывает эти два пути в RPC `telemetry_push` и `error_report_push` и сам подставляет `http_key`, поэтому ключ в клиент не попадает и пакет не связан ни с каким пользователем. Лимит тела: 16 КБ и 64 КБ.

- Проверка пакета строгая: поля ровно как у `TelemetryJson` / `ErrorReportJson` схемы 1, лишнее поле → 400. Образцы пишет C#-тест `IntakeVectorTests` в `test-vectors/intake-samples.json`.
- Ответы: 200 — принят, 400 — пакет плохой (клиент его выбрасывает), 429 — дневной лимит, 403 — вызов из сессии игрока (запрещён).
- Хранение у системного пользователя: `telemetry/<installId>:<session>` (черновик после падения не перетирает закрытую сессию), `error_reports/<время>:<случайное>` с `fingerprint` для группировки. Сроки, лимиты и обрезка — `config/intake.json`. Первый пакет дня удаляет устаревшие.
- Сводка для Сергея — служебные RPC, только с `http_key`: `telemetry_summary` (установки, удержание D1/D7/D30, время до первого престижа, нажатия и клики в минуту, длина сессии, рекорд этапа, версии, персонажи) и `error_report_list` (`{ limit }`, группы одинаковых ошибок). На сервере: `Server/deploy/report.sh telemetry` или `report.sh errors 20`.
