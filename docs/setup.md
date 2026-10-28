# Что нужно для разработки: программы, аккаунты, доступы

Один список всего, что нужно поставить и подготовить, чтобы агенты (кодовый и Blender) могли делать игру. Подробности по версиям лежат в `engine-spec.md` §1 и `blender-spec.md` §1. Цены и условия лицензий проверить на сайтах перед покупкой.

## 1. Компьютер Сергея (Windows 10/11 x64): ссылки и вес
Ссылки и версии проверены на официальных сайтах 19.10.2026. «Вес на диске» — оценка после установки, если сайт не даёт точной цифры; точный размер Unity Hub и установщик Visual Studio показывают перед установкой.

| Что | Ссылка | Скачать | На диске |
|---|---|---|---|
| **Git for Windows 2.56** (Git LFS входит в установщик) | [git-scm.com](https://git-scm.com/install/windows), прямая: [Git-2.56.0.2-64-bit.exe](https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/Git-2.56.0.2-64-bit.exe) | ~70 МБ | ~0.4 ГБ |
| **Unity Hub** | [unity.com/download](https://unity.com/download) | ~0.1 ГБ | ~0.4 ГБ |
| **Unity 6.6** (ставится через Hub) + модуль **Windows Build Support (IL2CPP)** | через Unity Hub → Installs → Install Editor → 6000.6.6f1 (версия проекта) | ~3–5 ГБ | **~6–10 ГБ** |
| **Visual Studio 2026 Community** (текущая версия; Unity нужен VS 2019 или новее с C++) | [visualstudio.microsoft.com/downloads](https://visualstudio.microsoft.com/downloads/) | ~5–10 ГБ | **~15–25 ГБ** (Microsoft: обычная установка 20–50 ГБ) |
| **Blender 5.2.2 LTS** (если Blender MCP стоит на 4.5 LTS, оставить её) | [blender-5.2.2-windows-x64.msi](https://www.blender.org/download/release/Blender5.2/blender-5.2.2-windows-x64.msi/) | 348 МБ | ~1 ГБ |
| **uv** (для MCP for Unity) | `winget install --id=astral-sh.uv -e` ([документация](https://docs.astral.sh/uv/getting-started/installation/)) | ~20 МБ | ~0.1 ГБ |
| **Claude Code** | PowerShell: `irm https://claude.ai/install.ps1 \| iex` ([документация](https://code.claude.com/docs/en/setup)) | ~0.1 ГБ | ~0.3 ГБ |
| **Steam** | [SteamSetup.exe](https://cdn.fastly.steamstatic.com/client/installer/SteamSetup.exe) | ~2 МБ, потом докачивает | ~1 ГБ |
| **MCP for Unity** (пакет внутри проекта) | по шагам в `mcp-setup.md` | — | меньше 0.1 ГБ |
| **Blender MCP** | уже установлен | — | — |

**Наборы в установщике Visual Studio:** «Разработка игр на Unity» и «Разработка классических приложений на C++». В C++ должен быть Windows SDK 10.0.19041.0 или новее (требование Unity для IL2CPP). Signtool для подписи exe входит в Windows SDK.

**Сам проект со временем:**
- папка `Library` у Unity: 1–3 ГБ (кэш, в git не идёт);
- репозиторий с артом в LFS (`.blend`, спрайт-листы, звуки): к MVP примерно 2–5 ГБ;
- сборки игры: ~100–200 МБ каждая.

**Итого: ~30–45 ГБ** на программы и проект. Лучше держать **60 ГБ свободных на SSD**: Unity и Visual Studio заметно медленнее на HDD.

**Порядок установки:** Git → Visual Studio (с наборами выше) → Unity Hub → Unity 6.6 с модулем IL2CPP → Blender → uv → Claude Code → Steam → MCP for Unity (после создания проекта, roadmap 0.2).

| Что ещё | Зачем | Примечание |
|---|---|---|
| **GitHub** (аккаунт и приватный репозиторий) | код, PR, CI | агент работает через ветки и PR (CLAUDE.md) |
| Лицензия **Unity Personal** | бесплатна до порога выручки | концепт §5 |
| Подписка Claude Pro или Max | Claude Code требует платный аккаунт | бесплатный план не подходит |

## 2. Пакеты Unity (агент ставит сам)
Список пакетов — в `engine-spec.md` §1, версии фиксируются в `Packages/manifest.json`. MCP for Unity — по `mcp-setup.md`.

## 3. Сервер (уже есть VPS 2 ядра, 2 ГБ)
- **Docker** и **docker compose**.
- **Nakama** + **PostgreSQL** в контейнерах, swap 2 ГБ, ежедневный бэкап базы (concept §5а).
- Домен и HTTPS-сертификат (Let's Encrypt), чтобы клиент ходил на сервер по TLS.
- Агенту нужны адрес и SSH-доступ, а лучше отдельный пользователь без прав root.
- Всё это ставят скрипты из `Server/deploy` (Ubuntu 24.04, версии в `Server/deploy/versions.env`): для человека — `Server/deploy/README.md`, для агента — `docs/server-deploy.md`.

## 4. Аккаунты и покупки (делает человек)
| Что | Когда | Примечание |
|---|---|---|
| **Steamworks Partner** + взнос Steam Direct ($100 за игру) | до фазы 13 (кооп через Steam) | App ID нужен для Steam-авторизации и лобби; до него можно тестировать на тестовом App ID 480 |
| **Сертификат подписи кода** (OV или EV) | до фазы 2 (проверка антивирусами), решение в roadmap 0.6 | без подписи антивирусы чаще ругаются на Raw Input |
| **Аккаунт VirusTotal** | фаза 2 | проверка сборок |
| **CI-лицензия Unity** (если CI через GitHub Actions + GameCI) | фаза 0.4 | GameCI требует секрет с лицензией; можно обойтись локальным скриптом |

## 5. Инструменты для ассетов (по желанию)
- Звуки-заглушки: jsfxr или sfxr (генерация), Audacity (правка).
- Шрифты только с лицензией на использование в играх (например, из Google Fonts с OFL).
- Базовые модели и анимации для Blender: решение Сергея (blender-spec §9, «гибрид»).

## 6. Что агент спросит у человека
См. `engine-spec.md` §14 и `blender-spec.md` §11.
