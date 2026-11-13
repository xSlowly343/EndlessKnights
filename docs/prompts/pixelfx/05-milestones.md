Привет! Делаем эффекты вех через pixelfx (решения Сергея 13.11.2026, docs/pixelfx-uses.md §3).

Прочитай: CLAUDE.md; docs/pixelfx-uses.md; docs/prestige-options.md, docs/artifacts.md (открытие и пробуждение), docs/transitions.md §6.2 и §6.4 (лестница и дары Разлома), docs/ui-design-system.md (движение), docs/ui-screens.md; события ядра: LevelUp, престиж, ArtifactUnlocked/пробуждение, RiftFeatureOpened, RiftGiftOffered, ачивки и квесты.

Задача (ветка task/pixelfx-milestones, задача roadmap 14е.33):
1. Перечень вех и к какому событию ядра каждая привязана — согласуй с Сергеем (🧑).
2. Эффекты: уровень героя, престиж (Эхо), открытие и пробуждение артефакта, ступень лестницы Разлома, выбор дара, ачивка и квест; не перекрывают бой дольше 1 с.
3. Листы, вид, превью Сергею; замер CPU — [R].

Всё с нуля, стоковые `ui/*` — только референс. Заметки — dev-notes, todo, progress; без комментариев в коде; ничего не удаляй без «да».
