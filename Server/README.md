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

- RPC `party_create` (`{ "seed": "123" }`, сид необязателен) создаёт матч и отдаёт `{ "matchId": ... }`.
- RPC `rift_window` (без аргументов) отдаёт `{ serverUnixMs, open, next, announced }`: открытое окно Разлома (или `null`), следующее окно и их модификаторы по `rift.json`.
- Клиент входит в матч с метаданными `{ "playerNumber": "<SteamID64>" }`: это число попадает в `Roster`, по нему клиент находит свой слот.
- Пакеты те же, что у `LoopbackTransport`: первый байт — вид сообщения, opCode равен ему же.
- Состав отряда игрока пишется в хранилище: коллекция `party`, ключ `current`, `{ matchId, slot }`.
