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
  | `party_create` | `{ access?: "invite"\|"open", title?, language?, wanted? }` | вид отряда: `{ id, code, access, title, language, wanted, leader, maxMembers, matchId, members: [{ userId, name, steamId, leader }] }` |
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

  Значения `title`, `language`, `wanted`, `role`, `slot` берутся из `party.json` → `search`.
- Хранилище: `party_codes/<код>` и `party_match/<partyId>` у системного пользователя, `party/current` у игрока (`{ partyId, matchId, slot, seen }`), `party_blocks/list` у игрока, `party_reports/<кого>:<кто>` у системного пользователя.
- Клиентские вызовы групп (создать группу с именем `party-…`, вступить, выйти, исключить, повысить, изменить, удалить отряд, листать группы) закрыты before-хуками: отрядом управляют только RPC.
- RPC `rift_window` (без аргументов) отдаёт `{ serverUnixMs, open, next, announced }`: открытое окно Разлома (или `null`), следующее окно и их модификаторы по `rift.json`.
- Клиент входит в матч с метаданными `{ "playerNumber": "<SteamID64>" }`: это число попадает в `Roster`, по нему клиент находит свой слот. Матч отряда пускает только участников этого отряда.
- Пакеты те же, что у `LoopbackTransport`: первый байт — вид сообщения, opCode равен ему же.
- Состав отряда игрока пишется в хранилище: коллекция `party`, ключ `current`, `{ matchId, slot }`.
- Капы урона и ударов (`party.json` → `caps`, coop.md §8): срезанный урон не засчитывается. Каждые 20 пометок игрока пишутся в лог и в хранилище: коллекция `party_flags`, ключ — id игрока, `{ flags, matchId, playerNumber, at }`, читается только из консоли.
- В `EnemyKilled` последним байтом идёт маска награды (бит на слот), в `Roster` — запас отдыха каждого слота в секундах (coop.md §3.5).
