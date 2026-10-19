# Подключение MCP: Unity и Blender

Blender MCP у Сергея уже установлен. Ниже подключение **MCP for Unity** (CoplayDev/unity-mcp, лицензия MIT, поддерживает Unity 2021.3 LTS – 6.x) к Claude Code на Windows. Проверено по документации проекта 19.10.2026, последняя версия на тот день v10.3.0.

## 1. Что поставить один раз
1. **Git** (уже нужен для репозитория, см. setup.md). Без него Unity не скачает пакет по Git URL.
2. **uv** (менеджер Python, через него запускается сервер MCP). В PowerShell:
   ```powershell
   winget install --id=astral-sh.uv -e
   ```
3. **Claude Code**, если ещё нет. В PowerShell:
   ```powershell
   irm https://claude.ai/install.ps1 | iex
   claude doctor
   ```

## 2. Пакет в Unity
1. Открыть проект игры в Unity.
2. `Window → Package Manager → + → Add package from git URL...`
3. Вставить и нажать Add:
   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
   ```
   Чтобы версия не менялась сама, можно закрепить тег, например `#v10.3.0`. Агент записывает выбранную версию в `docs/dev-notes.md`.

## 3. Связать с Claude Code
**Способ A (проще):** в Unity `Window → MCP for Unity → Configure All Detected Clients` (или Auto-Setup). Должен загореться зелёный статус «Connected ✓».

**Способ B (вручную, HTTP — режим по умолчанию).** Unity с открытым проектом должен быть запущен. В папке репозитория:
```powershell
claude mcp add --scope project --transport http UnityMCP http://localhost:8080/mcp
claude mcp list
```
`--scope project` записывает настройку в `.mcp.json` в корне репозитория, поэтому она общая для всех, кто работает с проектом. `claude mcp list` должен показать `UnityMCP` как подключённый.

**Порядок запуска каждый раз:** сначала Unity с проектом, потом Claude Code.

## 4. Если не работает
- **Порт 8080 занят** (например, Tailscale): в окне `MCP for Unity` сменить порт на 8090 и заново выполнить `claude mcp add` с новым адресом.
- **Сервер не стартует:** в окне `MCP for Unity` нажать «Choose UV Install Location» и указать `%LOCALAPPDATA%\Microsoft\WinGet\Links\uv.exe`.
- **Сменили транспорт (http ↔ stdio):** перезапустить Claude Code.
- **Список инстансов пустой:** `Window → MCP for Unity → Restart Server`.
- **Ошибка git в Package Manager:** поставить git и перезапустить Unity.
- Открыт **только один** Unity с этим проектом: серверу нужен ровно один подходящий `Unity.exe`.

## 5. Что агент делает через Unity MCP
Инструменты: чтение консоли, управление сценой, объектами, ассетами, шейдерами, пункты меню редактора, правка и проверка скриптов.

**Правила для агента:**
- Код пишется **файлами в репозитории**, как обычно. MCP нужен, чтобы видеть консоль с ошибками, собирать сцены и префабы, запускать пункты меню и Play Mode.
- **Удалять через MCP** (ассеты, объекты, скрипты, сцены) **нельзя без спроса**, как и любые файлы (CLAUDE.md, жёсткие правила).
- Сборки и тесты всё равно запускаются из командной строки (`build.ps1`, Unity batchmode), чтобы CI и локальный результат совпадали. MCP не замена этим проверкам.
- Если MCP недоступен, работа не стоит: агент продолжает через файлы и командную строку, а в `docs/todo.md` пишет, что проверить в редакторе руками.

## 6. Blender MCP (уже есть)
- Агент графики использует его для работы с открытым Blender. Пакетный рендер всё равно идёт headless: `blender -b ... -P render_entity.py` (blender-spec.md §1, §6).
- Те же правила: не удалять `.blend`, рендеры и скрипты без спроса.

## Источники
- [CoplayDev/unity-mcp на GitHub](https://github.com/CoplayDev/unity-mcp)
- [Документация MCP for Unity](https://coplaydev.github.io/unity-mcp/)
- [Транспорты: HTTP по умолчанию, порт 8080](https://coplaydev.github.io/unity-mcp/architecture/transports)
- [Устранение неполадок](https://coplaydev.github.io/unity-mcp/guides/troubleshooting)
