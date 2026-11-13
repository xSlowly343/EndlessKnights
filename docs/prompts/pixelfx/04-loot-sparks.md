Привет! Делаем эффекты добычи через pixelfx (решения Сергея 13.11.2026, docs/pixelfx-uses.md §3).

Прочитай: CLAUDE.md; docs/pixelfx-uses.md; docs/items.md (11 редкостей и их цвета), docs/ui-design-system.md (цвета `UiTheme`, движение); docs/transitions.md §6.3б (Искры Разлома); код: DropSystem/ItemDrop, Presentation/Battle/CoinBurst.cs, DamageNumbers.cs.

Задача (ветка task/pixelfx-loot, задача roadmap 14е.32):
1. Столб добычи над выпавшей вещью цвета её редкости (11 вариантов из одного эффекта, сила растёт с редкостью), отдельно сет и уникальная.
2. Открытие сундука и письма с наградой; монеты и гемы — заменить фигуры `CoinBurst` листами.
3. Искры Разлома летят с босса в счётчик.
4. Листы, вид в Presentation (пул, без аллокаций), превью Сергею; замер CPU — [R].

Всё с нуля, стоковые `pickups/*` — только референс. Заметки — dev-notes, todo, progress; без комментариев в коде; ничего не удаляй без «да».
