# Заметки разработки

Все заметки, решения и объяснения «почему так» по модулям. В коде заметок нет (см. CLAUDE.md).

## Input

## Window

## Core
- Слои сборок (0.3): Core → Platform.Windows, Presentation, Net → Bootstrap → Editor. Ссылаться можно только на слой ниже, соседей по слою не связываем. Это проверяет `Game.Architecture.Tests` по файлам asmdef и по скомпилированной Core (нет ссылок на `Unity*`). Новую сборку в `Assets/Game` нужно добавить в таблицу слоёв теста, иначе он упадёт.
- Пустые asmdef Unity не компилирует, поэтому сборки без кода появятся в Test Runner и в решении только с первым файлом.
- `Game.Platform.Windows` собирается только для редактора и Windows x64, в нём разрешён `unsafe` под P/Invoke.
- `Game.Core` собирается и тестируется без Unity: `Tools/test-core.ps1` (проекты в `Tools/CoreBuild`). Исходники лежат в `Assets/Game/Core` и `Assets/Tests/Core.Tests`, csproj только подключают их. Это даёт быстрые тесты до установки Unity и пригодится для CI (0.4) и сервера.
- Сборка Core ограничена тем, что умеет Unity 6: `netstandard2.1` и C# 9. Если в Core появится API новее, сборка упадёт раньше, чем в Unity. `Nullable` включён через `csc.rsp` рядом с asmdef.
- Предупреждения в Core считаются ошибками.
- `BigNumber` (3.1–3.2): мантисса по модулю в [1, 10), знак хранится в мантиссе, потому что HP минус урон может уйти в минус. Ноль всегда `0e0`, `default` тоже ноль. Слагаемое меньше в 10^17 раз и более отбрасывается (точность double около 16 знаков). `Pow` разрешён только для неотрицательных чисел; степень считается через логарифм, а целая часть показателя отделяется заранее, чтобы не терять точность на огромных показателях. `Log10` ведёт себя как `Math.Log10`: ноль даёт −∞, отрицательное — NaN. Равенство точное по полям, поэтому в тестах значения после арифметики сравниваются с допуском.

## Sim

## Presentation

## Net

## Data и баланс

## Save

## Server

## Tools и MCP
- Проект на **Unity 6000.6.6f1** вместо 6.3 LTS из engine-spec: Сергей поставил 6.6, решили остаться на ней (19.10.2026). Переход на следующую LTS с 6.6 простой, откат на 6.3 не нужен.
- Проект создан из встроенного шаблона `2d-cross-platform` (URP 2D Renderer, `Assets/Settings`). Лишние пакеты шаблона убраны из `manifest.json`: tilemap, spriteshape, aseprite, psdimporter, animation, timeline, visual scripting, Rider, collab, обучающий фреймворк. 2D Animation вернём, только если понадобятся кости (engine-spec §1).
- Input System остался из шаблона: он нужен для ввода в редакторе (2.13), а в сборке ввод идёт через Raw Input.
- Настройки игрока задаёт код, а не ручная правка: `Game/Apply Project Settings` (`ProjectConfigurator`) — IL2CPP, .NET Standard, Windows x64, работа в фоне, оконный режим, имя продукта «Endless Knights», компания `EndlessKnights` (по имени репозитория). Сборка: `Game/Build Windows` или батч-режим `-executeMethod Game.Editor.WindowsBuild.Build`, exe в `Builds/Windows`.
- Для IL2CPP нужен Visual Studio с набором «Разработка классических приложений на C++» и Windows SDK; без него сборка падает с `ToolchainNotFoundException`. Первая сборка IL2CPP идёт около 3 минут.
- `build.ps1` в корне (0.5): `powershell -ExecutionPolicy Bypass -File build.ps1` собирает exe, с `-Tests` сначала гоняет EditMode-тесты. Unity ищется по версии из `ProjectVersion.txt` в папке Unity Hub, другой путь задаётся `-UnityPath` или переменной `UNITY_EDITOR`. Логи и результаты тестов в `Builds/Logs`. Пока открыт редактор с этим проектом, батч-режим Unity не запустится.

## Art
