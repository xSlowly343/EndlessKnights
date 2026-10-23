using System.Collections.Generic;
using System.Globalization;

namespace Game.Core.Stats
{
    public static class ModifierText
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["damage"] = "урон",
            ["resourceGain"] = "накопление ресурса",
            ["keyHitLightning"] = "молния от каждого удара",
            ["offlineGold"] = "золото офлайн",
            ["gold"] = "золото",
            ["comboTimeoutSeconds"] = "окно комбо",
            ["critChance"] = "шанс крита",
            ["critMultiplier"] = "множитель крита",
            ["auraEffect"] = "сила аур",
            ["focusGain"] = "набор Фокуса",
            ["auraDuration"] = "длительность аур",
            ["comboStrikeMultiplier"] = "сила удара комбо",
            ["strength"] = "Сила",
            ["dexterity"] = "Ловкость",
            ["intelligence"] = "Интеллект",
            ["blessingEffect"] = "сила благословений",
            ["blessingSeconds"] = "длительность благословений",
            ["blessingCost"] = "цена благословений",
            ["corrosionPerHit"] = "Коррозия за удар",
            ["corrosionMax"] = "предел Коррозии",
            ["corrosionSeconds"] = "длительность Коррозии",
            ["corrosionSplash"] = "Коррозия на соседей",
            ["heatPerHit"] = "Жар за удар",
            ["heatThreshold"] = "порог Жара",
            ["offlineCapHours"] = "предел офлайна",
            ["seriesGain"] = "набор Серии",
            ["auraAllyEffect"] = "сила аур на союзников",
            ["burnDamage"] = "урон горения",
            ["burnSeconds"] = "длительность горения",
            ["curseEffect"] = "сила проклятий",
            ["curseSeconds"] = "длительность проклятий",
            ["cursesOnEnemy"] = "проклятий на враге",
            ["curseOnBossStart"] = "проклятие в начале боя с боссом",
            ["hexPerHit"] = "Порча за удар",
            ["windupGain"] = "набор Замаха",
            ["windupKeptAfterCrush"] = "Замах после Сокрушения",
            ["partyGold"] = "золото группы",
            ["partyResource"] = "ресурс союзникам",
            ["partyDamage"] = "урон группы",
            ["partyCritChance"] = "шанс крита группы",
            ["partyCritMultiplier"] = "множитель крита группы",
            ["partyContribution"] = "вклад в группу",
            ["volleyDamage"] = "урон Залпа",
            ["volleyEvery"] = "удары до Залпа",
            ["volleySplash"] = "Залп по соседям",
            ["enterDamage"] = "урон по Enter",
            ["damageVsBoss"] = "урон по боссам",
            ["dotDamage"] = "урон со временем",
            ["dotDuration"] = "длительность урона со временем",
            ["dotSpread"] = "распространение урона со временем",
            ["crushMultiplier"] = "сила Сокрушения",
            ["crushCleave"] = "Сокрушение по соседям",
            ["flareMultiplier"] = "сила Вспышки",
            ["flareSplash"] = "Вспышка по соседям",
            ["flareDisabled"] = "Вспышка отключена",
            ["focusResetSeconds"] = "время до сброса Фокуса",
            ["focusMax"] = "предел Фокуса",
            ["squallHits"] = "удары Шквала",
            ["squallNeedsVortices"] = "вихрей для Шквала",
            ["doomedCritChance"] = "шанс крита по обречённым",
            ["doomedCritMultiplier"] = "множитель крита по обречённым",
            ["staggerBonus"] = "бонус Оглушения",
            ["staggerSeconds"] = "длительность Оглушения",
            ["pierceShare"] = "доля пробивания",
            ["mouseDistance"] = "урон от пути мыши",
            ["doubleStrike"] = "шанс двойного удара",
            ["gemDrop"] = "шанс выпадения гемов",
            ["idleStartSeconds"] = "время до Затишья",
            ["damagePerSector"] = "урон за сектор",
            ["concurrentBlessings"] = "благословений одновременно",
            ["concurrentAuras"] = "аур одновременно",
            ["arrowDamage"] = "урон стрел",
            ["bossTimerSeconds"] = "таймер босса",
            ["keyStrikeEvery"] = "нажатий до сильного удара",
            ["keyStrikeMultiplier"] = "сила сильного удара",
            ["rageBurnDelaySeconds"] = "задержка сгорания Ярости",
            ["chainTargets"] = "целей цепной молнии",
            ["overloadMultiplier"] = "сила Перегрузки",
            ["overloadDisabled"] = "Перегрузка отключена",
            ["songMouseSeconds"] = "продление Песни от мыши",
            ["readingGain"] = "скорость чтения знаков",
            ["auraReserve"] = "резерв аур",
            ["bannerAuraBonus"] = "бонус аур у знамени",
            ["flickCurseBonus"] = "бонус проклятий от росчерка",
            ["bossTimerSlow"] = "замедление таймера босса",
            ["skillCooldown"] = "перезарядка скиллов",
            ["loopShare"] = "доля повтора Петли",
            ["poisonStacks"] = "предел стаков яда",
            ["pressureDecay"] = "сброс Давления",
            ["overheatSeconds"] = "время Перегрева",
            ["overheatDisabled"] = "Перегрев отключён",
            ["minionCount"] = "призывов одновременно",
            ["minionSpeed"] = "скорость призывов",
            ["minionSeconds"] = "длительность призывов",
            ["minionExplode"] = "взрыв призыва в конце",
            ["cannonCount"] = "пушек одновременно",
            ["resourceMax"] = "предел ресурса",
            ["skillSlots"] = "слотов скиллов",
            ["mouseCountsAsKeys"] = "мышь считается клавишами",
            ["damagePerKill"] = "урон за убийство подряд",
            ["idleKillPenalty"] = "потеря урона в Затишье",
            ["critDamageBuff"] = "урон после крита",
            ["firstHitsBonus"] = "урон первых ударов по врагу",
            ["laterHitsPenalty"] = "урон следующих ударов"
        };

        private static readonly HashSet<string> FlatPercent = new HashSet<string>
        {
            "critChance", "critMultiplier", "comboStrikeMultiplier", "crushCleave", "crushMultiplier", "damagePerSector",
            "doomedCritChance", "doomedCritMultiplier", "dotSpread", "doubleStrike", "flareMultiplier", "flareSplash",
            "focusMax", "partyContribution", "partyCritChance", "partyCritMultiplier", "pierceShare", "volleySplash",
            "windupKeptAfterCrush", "gemDrop", "damagePerKill", "idleKillPenalty", "critDamageBuff", "firstHitsBonus",
            "laterHitsPenalty", "minionExplode"
        };

        private static readonly Dictionary<StatTags, string> TagLabels = new Dictionary<StatTags, string>
        {
            [StatTags.Keyboard] = "от клавиш",
            [StatTags.Mouse] = "от мыши",
            [StatTags.Click] = "от кликов",
            [StatTags.Idle] = "в Затишье",
            [StatTags.Skill] = "скиллов",
            [StatTags.Aura] = "аур",
            [StatTags.DoT] = "со временем",
            [StatTags.Crit] = "критов",
            [StatTags.Minion] = "призывов"
        };

        public static string Label(StatId stat) => Labels.TryGetValue(stat.Name, out var label) ? label : stat.Name;

        public static string Describe(Modifier modifier)
        {
            var label = Label(modifier.Stat) + TagSuffix(modifier.Tags);
            return modifier.Kind switch
            {
                ModifierKind.Increased => $"{Signed(modifier.Value * 100)}% {label}",
                ModifierKind.More => $"ещё {Signed(modifier.Value * 100)}% {label}",
                _ => $"{FlatValue(modifier)} {label}"
            };
        }

        private static string FlatValue(Modifier modifier)
        {
            var name = modifier.Stat.Name;
            if (FlatPercent.Contains(name))
                return Signed(modifier.Value * 100) + "%";
            if (name.EndsWith("Seconds"))
                return Signed(modifier.Value) + " с";
            if (name.EndsWith("Hours"))
                return Signed(modifier.Value) + " ч";
            return Signed(modifier.Value);
        }

        private static string TagSuffix(StatTags tags)
        {
            var suffix = string.Empty;
            foreach (var pair in TagLabels)
                if ((tags & pair.Key) != 0)
                    suffix += " " + pair.Value;
            return suffix;
        }

        private static string Signed(double value)
        {
            var text = System.Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
            return value >= 0 ? "+" + text : text;
        }
    }
}
