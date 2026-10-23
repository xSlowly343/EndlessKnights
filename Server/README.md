# Сервер Nakama

Модули Nakama на TypeScript. Сейчас здесь хозяин боя отряда (authoritative match `party`, задача 13.28).

## Сборка и тесты
Нужен Node.js 22 и TypeScript 6 (`npm i -g typescript`). Зависимостей npm нет, типы Nakama описаны в `src/nakama.d.ts` (только то, что используется).

```
cd Server
npm test
```

`npm run build` собирает `build/index.js`: TypeScript в один файл ES5 (так его грузит Nakama) и в начало файла правила из `Assets/StreamingAssets/Data/party.json` и `stages.json`. Цифр баланса в коде сервера нет.

Тесты (`node --test`) прогоняют:
- `test/encounter.test.js` — сценарии из `test-vectors/party-encounter.json`, шаг за шагом сверяют состояние боя с C#;
- `test/codec.test.js` — байты пакетов из `test-vectors/party-codec.json`;
- `test/host.test.js` — приём пакетов, дубликаты, предел сообщений, пересылка `PartyOffer`.

Тест-векторы пишет C# (`PartyVectorTests`, `PartyCodecVectorTests` в `Assets/Tests/Core.Tests/Party`). После изменения `PartyEncounter`, кодека или правил запусти тесты Core с `UPDATE_PARTY_VECTORS=1`, закоммить новые файлы и прогони `npm test`. Без переменной C#-тест падает, если векторы устарели.

## Установка в Nakama
Скопировать `build/index.js` в папку модулей Nakama (`/nakama/data/modules`) и указать `runtime.js_entrypoint: "index.js"`.

- RPC `party_create` (`{ "seed": "123" }`, сид необязателен) создаёт матч и отдаёт `{ "matchId": ... }`.
- Клиент входит в матч с метаданными `{ "playerNumber": "<SteamID64>" }`: это число попадает в `Roster`, по нему клиент находит свой слот.
- Пакеты те же, что у `LoopbackTransport`: первый байт — вид сообщения, opCode равен ему же.
- Состав отряда игрока пишется в хранилище: коллекция `party`, ключ `current`, `{ matchId, slot }`.
