# Rift Warden

Фоновая кооп-игра для Windows на Unity 6.6. Бой идёт в маленькой полоске над панелью задач (480×180), урон дают нажатия клавиш и движение мыши в любых приложениях, а скиллы висят на ЛКМ и ПКМ. Сеттинг — яркое фэнтези в духе Tap Titans 2: Страж Разлома против Колоссов. Билды как в Path of Exile.

## Коротко об игре
- **14 персонажей** в духе Hero Siege, у каждого свой ресурс вместо общей Энергии и по 3 ascendancy (`docs/class-resources.md`, `docs/concept.md` §2.2).
- **Атрибуты и цвета:** 🔴 Сила, 🟢 Ловкость, 🔵 Интеллект. Пассивное дерево в три сектора, 14 стартов, Кочевник в центре, около 2600 узлов (`docs/concept.md` §2.5, `docs/tree-effects.md` §7).
- **Скиллы и гемы:** классовые скиллы требуют оружие своего типа, всё оружие одноручное, на герое две руки. Гемы живут в Книге умений, поддержки сидят на самом геме (`docs/skills.md`).
- **Предметы:** 9 слотов, 11 редкостей, аффиксы с тирами, уникальные вещи, сеты и крафт (`docs/items.md`).
- **Кооп в первую очередь:** общий бой отряда, Пристанище отряда до 4 игроков, кланы и рейды (`docs/coop.md`, `docs/party-hideout.md`).
- **Мирный мир:** убежище, общий город Кромка, ремёсла (`docs/peaceful-world.md`).
- Ввод только через Raw Input, наружу выходят только счётчики, коды клавиш нигде не хранятся.

## Статус (23.10.2026)
Готов играбельный прототип: окно-полоска, ввод, ядро боя на тике 10 Гц, отображение боя, сейвы и офлайн, большая часть скиллов, дерева, Книги умений и престижа. В процессе настройки и онбординг (фаза 11). Впереди графика (12), сеть и кооп (13), плейтест (14) и механики по одной (14а).

В коде 14 персонажей со своими ресурсами (свои у Викинга и Металлиста, у остальных пока общая заглушка), скиллы на ЛКМ и ПКМ и дерево на 2623 узла под 14 стартов. Название в коде — Rift Warden. Что сделано по дням — `docs/progress.md`.

## С чего начать
- `CLAUDE.md` — правила работы для ИИ-агента, `PROMPT.md` — первые сообщения агентам (код и Blender).
- `docs/progress.md` — журнал и с чего продолжать.
- `docs/roadmap.md` — план задач с быстрыми тестами.
- `docs/setup.md` — что установить: Git с LFS, Unity 6.6 с модулем IL2CPP, Visual Studio.

## Документы в `docs/`
| Тема | Файлы |
|---|---|
| План и журнал | `roadmap.md`, `progress.md`, `todo.md`, `dev-notes.md` |
| Техника | `engine-spec.md` (код), `blender-spec.md` (графика), `setup.md`, `mcp-setup.md` |
| Дизайн целиком | `concept.md` |
| Персонажи, дерево, скиллы | `class-resources.md`, `tree-effects.md`, `skills.md`, `ascendancy-keystones.md`, `ascendancy-missing.md` |
| Предметы и престиж | `items.md`, `prestige-options.md`, `artifacts.md` |
| Кооп и отряд | `coop.md`, `party-hideout.md` |
| Мирный мир | `peaceful-world.md` |
| Интерфейс и стиль | `ui-layout.md`, `ui-mockups/`, `visual-style.md`, `tbh-reference.md`, `tt2-combat-reference.md` |
| Контент и сеттинг | `content-plan.md`, `content-draft.md`, `setting-options.md` |
| Черновики и история | `monetization-p2w.md` (не реализуется), `archive/` (выбор классов, первый прототип, старый концепт мирных активностей; для новых задач не используется) |

При противоречии приоритет такой: `engine-spec.md` / `blender-spec.md` → `roadmap.md` → `content-draft.md` и `prestige-options.md` → `concept.md` → остальное (подробно в `CLAUDE.md`).

## Клонирование
Арт хранится в Git LFS, поэтому перед клонированием нужен установленный LFS:

```
git lfs install
git clone https://github.com/xSlowly343/EndlessKnights.git
```

## Структура
```
Assets/           проект Unity: Game/ (Core, Platform, Presentation, Net…), Scenes/, StreamingAssets/ (данные JSON), Tests/
Packages/         пакеты Unity
ProjectSettings/  настройки проекта Unity
Tools/            скрипты PowerShell и сборка Game.Core вне Unity (тесты, симулятор баланса, проверка сейвов)
docs/             документация
build.ps1         сборка игры
```
Папки `art/` (исходники Blender, фаза 12) и `Server/` (модули Nakama, фаза 13) появятся позже.
