# Лицензии сторонних файлов в графике

Правило `blender-spec.md` §0.2: у каждого стороннего файла записаны источник и лицензия. Список ведёт агент, при новой иконке или паке строка добавляется сюда.

## Иконки game-icons.net (CC BY 3.0)

Источник: [game-icons.net](https://game-icons.net), белые силуэты 512 px. Копии исходников лежат в `art/sources/game-icons/<автор>/`, из них `art/scripts/build_ui_icons.py` собирает спрайты по списку `art/ui/icons.json`. Изменения: обрезка, масштаб, тёмный контур; у групп `artifacts`, `companions`, `sets`, `menu` и `buttons` ещё цветная заливка, свечение и фон (стиль `glow`).

Условие CC BY 3.0: в титрах игры указать авторов. Строка для Настроек (задача 12.13):

> Иконки: Lorc, Delapouite, Skoll, Caro Asercion, Sbed, Willdabeast, Guard13007, PriorBlue, John Colburn, HeavenlyDog, Quoting, Starseeker, Aussiesim, Lucas, Carl Olsen — game-icons.net, CC BY 3.0

| Автор | Файлы |
| --- | --- |
| Lorc | `anchor`, `anvil`, `anvil-impact`, `archery-target`, `arrow-flights`, `axe-swing`, `barbed-arrow`, `barbed-spear`, `battle-axe`, `battle-gear`, `beams-aura`, `belt-buckles`, `bestial-fangs`, `big-wave`, `boar-tusks`, `book-aura`, `boots`, `broadsword`, `broken-shield`, `bubbling-flask`, `bull-horns`, `burning-round-shot`, `butterfly`, `campfire`, `candle-flame`, `candle-holder`, `candlebright`, `cannon`, `cat`, `cauldron`, `cheese-wedge`, `clockwork`, `cloudy-fork`, `coffee-mug`, `cog`, `comet-spark`, `compass`, `conversation`, `corked-tube`, `cracked-glass`, `cross-mark`, `crossed-swords`, `crow-dive`, `crown`, `crown-coin`, `crystal-ball`, `crystal-cluster`, `crystal-growth`, `cut-diamond`, `cycle`, `dove`, `dragon-head`, `dragonfly`, `drill`, `drop`, `droplet-splash`, `earth-crack`, `echo-ripples`, `energy-breath`, `envelope`, `feather`, `fire-ace`, `fizzing-flask`, `flat-hammer`, `floating-crystal`, `flying-flag`, `focused-lightning`, `fox-head`, `frog`, `frozen-block`, `frozen-orb`, `gears`, `gem-pendant`, `guillotine`, `guitar`, `heart-drop`, `heart-inside`, `horned-skull`, `hourglass`, `hunting-horn`, `iron-mask`, `jeweled-chalice`, `knapsack`, `lantern-flame`, `laurel-crown`, `lightning-arc`, `lightning-branches`, `linked-rings`, `long-antennae-bug`, `love-howl`, `magic-portal`, `magnifying-glass`, `microscope-lens`, `mirror-mirror`, `molotov`, `moon`, `mouse`, `night-sky`, `open-book`, `owl`, `padlock`, `paper-lantern`, `parrot-head`, `pollen-dust`, `portal`, `potion-ball`, `pumpkin-lantern`, `punch-blast`, `quill`, `quill-ink`, `radial-balance`, `raven`, `reaper-scythe`, `return-arrow`, `ribbon`, `ringing-bell`, `round-bottom-flask`, `rune-stone`, `salamander`, `sands-of-time`, `scarab-beetle`, `scroll-unfurled`, `sewing-needle`, `shadow-follower`, `shard-sword`, `shiny-purse`, `shouting`, `skull-crack`, `skull-crossed-bones`, `sleepy`, `snake-bite`, `snowman`, `sonic-shout`, `spade`, `spark-spirit`, `sparkling-sabre`, `spectacle-lenses`, `spinning-blades`, `spotted-wound`, `sprint`, `spyglass`, `stone-sphere`, `stopwatch`, `sun`, `swallow`, `swap-bag`, `sword-smithing`, `syringe`, `tattered-banner`, `tear-tracks`, `tesla-coil`, `thunder-blade`, `thunderball`, `tornado`, `trade`, `tree-branch`, `trophy`, `turtle`, `twin-shell`, `two-feathers`, `two-shadows`, `visored-helm`, `voodoo-doll`, `walking-boot`, `wave-crest`, `wax-seal`, `wind-slap`, `wing-cloak`, `wizard-staff`, `wolf-head`, `worm-mouth` |
| Delapouite | `alarm-clock`, `all-seeing-eye`, `ancient-ruins`, `arrow-cursor`, `attack-gauge`, `balloon-dog`, `bed`, `bed-lamp`, `beer-horn`, `belt-armor`, `blindfold`, `bow-arrow`, `bow-string`, `brick-pile`, `butterfly-flower`, `caravan`, `card-joker`, `ceremonial-mask`, `chart`, `chat-bubble`, `check-mark`, `checkered-flag`, `chest-armor`, `chisel`, `clay-brick`, `cloth-jar`, `coins`, `crane`, `cricket`, `crystal-earrings`, `diamond-hilt`, `diamond-ring`, `direction-sign`, `djed-pillar`, `dream-catcher`, `drum`, `enrage`, `ermine`, `exit-door`, `expand`, `eye-target`, `fairy-wings`, `falling-star`, `fast-forward-button`, `fertilizer-bag`, `film-spool`, `fishing-pole`, `flint-spark`, `fox-tail`, `funnel`, `gloves`, `gold-nuggets`, `gold-stack`, `golem-head`, `greek-sphinx`, `griffin-shield`, `griffin-symbol`, `hamburger-menu`, `hand-of-god`, `hand-ok`, `headphones`, `heart-armor`, `heart-shield`, `heart-wings`, `herbs-bundle`, `horizon-road`, `house`, `info`, `jumping-dog`, `kangaroo`, `katana`, `key-card`, `keyboard`, `king-ju-mask`, `knight-banner`, `knot`, `lasso`, `light-projector`, `locked-box`, `lungs`, `magic-axe`, `mammoth`, `manta-ray`, `metronome`, `mighty-horn`, `mouse`, `musical-notes`, `notebook`, `old-lantern`, `padlock-open`, `paint-brush`, `painted-pottery`, `pendant-key`, `pendulum-swing`, `pin`, `pirate-hook`, `plague-doctor-profile`, `plant-seed`, `polar-star`, `pool-dive`, `position-marker`, `powder-bag`, `present`, `prism`, `pumpkin`, `quiver`, `rabbit`, `rat`, `ring`, `rod-of-asclepius`, `rolled-cloth`, `rolling-dices`, `rooster`, `rusty-sword`, `sandal`, `sandstorm`, `scabbard`, `settings-knobs`, `sewing-string`, `shaking-hands`, `share`, `shield-bash`, `shinto-shrine-mirror`, `shop`, `sickle`, `sitting-dog`, `slime`, `sniffing-dog`, `soul-vessel`, `sparkles`, `speaker`, `spiky-wing`, `split-arrows`, `spring`, `stabbed-note`, `stamper`, `star-formation`, `stone-stack`, `tally-mark-5`, `team-upgrade`, `thermometer-hot`, `thor-hammer`, `three-friends`, `token`, `trash-can`, `tree-roots`, `trophy-cup`, `two-coins`, `up-card`, `upgrade`, `valve`, `viking-helmet`, `viking-shield`, `walrus-head`, `war-bonnet`, `war-pick`, `warhammer`, `watering-can`, `windmill`, `windsock`, `worms` |
| Skoll | `open-chest`, `open-treasure-chest`, `pig`, `sound-waves`, `spotted-bug`, `trench-spade` |
| Caro Asercion | `boar`, `coinflip`, `fox`, `hedgehog`, `mole`, `ricochet` |
| Sbed | `cancel`, `help`, `poison-cloud` |
| Willdabeast | `gold-bar`, `round-shield` |
| Guard13007 | `pause-button`, `play-button` |
| PriorBlue | `battery-100`, `battery-25` |
| John Colburn | `pistol-gun` |
| HeavenlyDog | `defensive-wall` |
| Quoting | `card-pickup` |
| Starseeker | `potion-of-madness` |
| Aussiesim | `card-ace-spades` |
| Lucas | `cloak` |
| Carl Olsen | `flame` |

## Цветные иконки скиллов (08.11.2026)

Исходники уменьшены до 128 px и лежат в `art/sources/packs/<набор>/`, из них `art/scripts/build_ui_icons.py` собирает иконки группы `skills` (масштаб, скруглённые углы, рамка). Оригиналы и превью наборов — в общей папке проекта `icon-packs/`.

| Папка | Набор | Автор | Лицензия | ИИ |
| --- | --- | --- | --- | --- |
| `packs/inking/<стихия>/` | 100 Fire, Water, Earth, Wind, Lightning; 50 Dark, Light, Chaos, Space-Time skill icons — https://inking.itch.io | InKing | бесплатно для личных и коммерческих проектов, указывать автора не обязательно | да |
| `packs/painterly/` | Painterly Spell Icons 1–4 — https://opengameart.org/content/painterly-spell-icons-part-1 | J. W. Bjerk (eleazzaar), http://www.jwbjerk.com/art | CC BY 3.0, автор в титрах | нет |
| `packs/captaincatsparrow/<набор>/` | 25 FREE blood mage, druid, engineer, frost mage, hunter, necromancer skill icons; Free icon (морской); 40 Free Skill/Ability Icons Vol. 1 — https://captaincatsparrow.itch.io | CaptainCatSparrow | личные и коммерческие проекты | да |

Строка для титров (вместе со строкой game-icons):

> Иконки скиллов: InKing, J. W. Bjerk (eleazzaar, CC BY 3.0), CaptainCatSparrow

## Эффекты скиллов и ударов (08.11.2026)

| Набор | Автор | Лицензия | Что берём |
| --- | --- | --- | --- |
| [pixelfx](https://github.com/patch-gg/pixelfx) | patch-gg | MIT (код и эффекты), коммерция разрешена; часть кода взята из других MIT-проектов, подробности в `NOTICE.md` репозитория; шрифт Patch Pixel под MIT не входит | эффекты скиллов и ударов, рендер в спрайт-листы PNG + JSON |

Условие MIT: копия лицензии и строка об авторских правах должны лежать вместе с использованными эффектами (например, в папке с отрендеренными листами и в титрах/файле лицензий игры).

## Шрифты (OFL)

Rubik и Lilita One — `Assets/Game/Art/Fonts`, лицензии лежат рядом с файлами шрифтов.

## Пиксельная графика полоски (08.11.2026)

Исходники наборов лежат в общей папке проекта `art/packs/` (в репозиторий не кладём: лицензия CraftPix запрещает распространять исходные файлы отдельно от игры).

| Набор | Автор | Лицензия | Что берём |
| --- | --- | --- | --- |
| [Nature Landscapes Free Pixel Art](https://free-game-assets.itch.io/nature-landscapes-free-pixel-art) (он же [CraftPix Free Nature Backgrounds](https://craftpix.net/freebies/free-nature-backgrounds-pixel-art/)) | CraftPix | [CraftPix freebie](https://craftpix.net/file-licenses/): коммерческая игра можно, без указания автора, нельзя перепродавать файлы | фон «Зелёные холмы» (`nature_2`) |
| [Free 4 Nature Backgrounds for RPG Battle](https://craftpix.net/freebies/free-4-nature-backgrounds-for-rpg-battle/) | CraftPix | как выше | фон «Осенний лес», осенняя картинка (утверждён 08.11.2026). Слои скачал Сергей, они лежат вне репозитория |
| [Free Winter Holiday Game Backgrounds](https://craftpix.net/freebies/free-winter-holiday-game-backgrounds/) | CraftPix | как выше | фон «Снежные предгорья» (утверждён 08.11.2026). Слои прислал Сергей, они лежат вне репозитория |
| [Free Fairy Tale Game Backgrounds](https://craftpix.net/freebies/free-fairy-tale-game-backgrounds/) | CraftPix | как выше | фон «Мистический розовый лес», фон 2 (утверждён 08.11.2026). Архив прислал Сергей, он лежит вне репозитория |
| [Monsters Creatures Fantasy](https://luizmelo.itch.io/monsters-creatures-fantasy) | LuizMelo | CC0 | пробы, в состав не вошли (другой стиль) |
| [Animated Wild Animals](https://opengameart.org/content/animated-wild-animals) | ScratchIO | CC0 | пробы, в состав не вошли |
| [Ents](https://opengameart.org/content/ents) | см. страницу OGA | CC BY 4.0: указать автора в титрах | пробы, в состав не вошли |
| [Elthen's Pixel Art Shop](https://elthen.itch.io): Twig Blight, Mini Golem, Leafy Leshy, Porcupine, Spider, Ratfolk Axe, Cyclops, Goblin King, Minotaur (бесплатные) | Elthen | «Feel free to use the sprites in commercial/non-commercial projects», указывать автора не обязательно, условия — [пост на Patreon](https://www.patreon.com/posts/27430241) | враги «Зелёных холмов» (утверждены 08.11.2026) |
| [Elthen's Pixel Art Shop](https://elthen.itch.io): Leaf Elemental (перекрашен), Fox, Red Panda, Swampling, Ghoul, Giant Fly, Kobold Priest, Witch, Bandit Necromancer, Shardsoul Slayer, Goblin Mech Rider (бесплатные) | Elthen | как выше | враги «Осеннего леса» (утверждены 08.11.2026) |
| [Elthen's Pixel Art Shop](https://elthen.itch.io): Fire Elemental и Imp (перекрашены), Cobra, Ice Elemental, Ratfolk Mage, Armadillo, Dwarf, Brain Mole Monarch, Phoenixling (перекрашен) (бесплатные) | Elthen | как выше | враги «Снежных предгорий» (утверждены 08.11.2026) |
| [Elthen's Pixel Art Shop](https://elthen.itch.io): Jellyfish, Eye Monster, Squirrel, Dragonfly (перекрашен), Frog, Akaname, Intellect Devourer, Incubus, Cacodaemon (перекрашен) (бесплатные) | Elthen | как выше | враги «Мистического розового леса» (утверждены 08.11.2026) |
| [Yeti Asset Pack (Free)](https://pixelrazastudio.itch.io/yeti-asset-pack) | PixelrazaStudio | коммерция и правки разрешены, указывать автора не обязательно, перепродавать и выкладывать исходники нельзя | хозяин «Снежных предгорий» — Йети (утверждён 08.11.2026) |
| [Free Fairy Tale Game Backgrounds](https://craftpix.net/freebies/free-fairy-tale-game-backgrounds/) | CraftPix | [CraftPix freebie](https://craftpix.net/file-licenses/) | фон «Поющие каскады», фон 1 (выбран 09.11.2026). Архив прислал Сергей, он лежит вне репозитория |
| [Elthen's Pixel Art Shop](https://elthen.itch.io): Crab, Octopus, Slime, Bat, Buff Totem (бесплатные) | Elthen | как выше | враги «Поющих каскадов» (утверждены 09.11.2026) |
| [Free Predator Plant Mobs Pixel Art Pack](https://craftpix.net/freebies/free-predator-plant-mobs-pixel-art-pack/) | CraftPix | [CraftPix freebie](https://craftpix.net/file-licenses/) | хищные цветы «Поющих каскадов» (утверждены 09.11.2026) |
| [Wild Frog Boss](https://oliveiramiro2.itch.io/wild-frog-pixel-boss-animations) | oliveiramiro2 | бесплатно, на странице «This boss is available for free», формальной лицензии нет | запасной вариант хозяина «Поющих каскадов» |
| [Free Vampires Locations Battle Background Pack](https://craftpix.net/freebies/free-vampires-locations-battle-background-pack/) | CraftPix | [CraftPix freebie](https://craftpix.net/file-licenses/) | фоны «Умерший лес», «Замок», «Тронный зал» за Разломом (09.11.2026). Слои прислал Сергей, они лежат вне репозитория |
| CraftPix: [Free Vampire Pixel Sprites](https://free-game-assets.itch.io/free-vampire-pixel-art-sprite-sheets), [Free Zombie Sprite Sheet Pack](https://free-game-assets.itch.io/free-zombie-sprite-sheet-pack-pixel-art), [Free Werewolf](https://craftpix.net/freebies/free-werewolf-sprite-sheets-pixel-art/), [Free Halloween Character](https://craftpix.net/freebies/free-halloween-character-pixel-art-pack/), [Free Desert Enemy](https://craftpix.net/freebies/free-desert-enemy-sprite-sheets-pixel-art/) (стервятник) | CraftPix | [CraftPix freebie](https://craftpix.net/file-licenses/) | из первого состава За Разломом оставлены Вожак (Black Werewolf) и Рыжий оборотень (Red Werewolf), решение Сергея 09.11.2026; вампиры, зомби, хэллоуин и стервятник в состав больше не входят |
| [Monsters Creatures Fantasy](https://luizmelo.itch.io/monsters-creatures-fantasy) (Flying Eye, Goblin), [Monsters Creatures Fantasy 2](https://luizmelo.itch.io/monsters-creatures-fantasy-2) (Rat, Mimic) | LuizMelo | CC0 | враги За Разломом (крыса и мимик оставлены Сергеем 09.11.2026, око и гоблин утверждены) |
| [Skeletons Pack](https://monopixelart.itch.io/skeletons-pack), [Dark Fantasy Enemies](https://monopixelart.itch.io/dark-fantasy-enemies-asset-pack) (Bat), [Forest Monsters](https://monopixelart.itch.io/forest-monsters-pixel-art) (Mushroom) | MonoPixelArt | «use this asset pack in any project, commercial or not», нельзя перепродавать, распространять и изменять сам набор. Отражение и масштаб делаем при сборке листа, исходные кадры не перерисовываем | враги За Разломом (утверждены 09.11.2026) |
| [SHADOW Series - Hallokin](https://escape-pixel.itch.io/shadow-series-hallokin) | Escape Pixel | коммерция и правки разрешены, автора указывать не обязательно (можно «Escape Pixell») | тыквик Умершего леса (утверждён 09.11.2026) |
| [Bringer Of Death (Free)](https://clembod.itch.io/bringer-of-death-free) | Clembod | коммерция и правки разрешены, автора указывать не обязательно, нельзя перепродавать и распространять | жнец Умершего леса (утверждён 09.11.2026) |
| [Necromancer (Free)](https://creativekind.itch.io/necromancer-free), [NightBorne Warrior (Free)](https://creativekind.itch.io/nightborne-warrior) | CreativeKind | коммерция разрешена, нельзя распространять и перепродавать | некромант леса и Ночерождённый замка (утверждены 09.11.2026) |
| [Boss: Mecha-Stone Golem [FREE]](https://darkpixel-kronovi.itch.io/mecha-golem-free) | Darkpixel Kronovi | коммерция разрешена, автора указывать не обязательно, нельзя перепродавать и распространять | каменный страж Замка (утверждён 09.11.2026) |
| [Boss: Demon Slime](https://chierit.itch.io/boss-demon-slime) (FREE-версия) | Chierit | name your price; формальной лицензии на странице нет, в комментариях автор подтвердил коммерческое использование | хозяин Замка (утверждён 09.11.2026) |
| [Skeleton King](https://far20106.itch.io/skeleton-king) | far20106 | коммерция и правки разрешены, автора указывать не обязательно, нельзя перепродавать и выдавать за своё | Король Изнанки (выбран Сергеем 09.11.2026) |
| [Necromancer Skeleton Boss](https://oliveiramiro2.itch.io/necromancer-skeleton-boss-pixel-art) | oliveiramiro2 | name your price, **лицензия на странице не указана**; перед релизом спросить автора или заменить | костяной колдун Замка (утверждён 09.11.2026) |
| [Bosses - Frogger](https://admurin.itch.io/bosses-frogger) | Admurin | можно в любом проекте, в том числе коммерческом, указывать автора не обязательно; нельзя перепродавать как ассет, делать NFT и учить на нём ИИ | хозяин «Поющих каскадов» — Запевала (утверждён 09.11.2026) |
| [Evil Tree Pixel Art](https://jose-moyano.itch.io/evil-tree-pixel-art) | Jose Moyano | бесплатно (name your price); на вопрос о коммерции автор ответил «Sure!» в комментариях, формальной лицензии нет | злой пень (зеркально) |
| [Nature Landscapes Free Pixel Art](https://free-game-assets.itch.io/nature-landscapes-free-pixel-art) | CraftPix | как выше | фон «Зелёные холмы» (`nature_5`, утверждён 08.11.2026; `nature_2` — прежний вариант) |
