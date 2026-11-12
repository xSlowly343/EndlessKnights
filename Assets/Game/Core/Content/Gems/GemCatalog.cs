using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Gems
{
    public sealed class GemCatalog
    {
        public const string FileName = "skills.json";
        public const int SupportedVersion = 2;
        public const double MinBranchMultiplier = 0.01;
        public const double MaxBranchMultiplier = 100;

        private static readonly Dictionary<string, SkillType> Types = new Dictionary<string, SkillType>(StringComparer.Ordinal)
        {
            ["active"] = SkillType.Active,
            ["aura"] = SkillType.Aura,
            ["support"] = SkillType.Support
        };

        internal static readonly Dictionary<string, SkillTier> Tiers = new Dictionary<string, SkillTier>(StringComparer.Ordinal)
        {
            ["common"] = SkillTier.Common,
            ["rare"] = SkillTier.Rare,
            ["epic"] = SkillTier.Epic,
            ["legendary"] = SkillTier.Legendary,
            ["mythic"] = SkillTier.Mythic
        };

        private static readonly Dictionary<string, SkillRole> Roles = new Dictionary<string, SkillRole>(StringComparer.Ordinal)
        {
            ["fast"] = SkillRole.Fast,
            ["heavy"] = SkillRole.Heavy
        };

        private static readonly Dictionary<string, HitInput> Inputs = new Dictionary<string, HitInput>(StringComparer.Ordinal)
        {
            ["keys"] = HitInput.Keys,
            ["mouse"] = HitInput.Mouse,
            ["any"] = HitInput.Any,
            ["combo"] = HitInput.Combo
        };

        private static readonly Dictionary<string, GemColor> Colors = new Dictionary<string, GemColor>(StringComparer.Ordinal)
        {
            ["red"] = GemColor.Red,
            ["green"] = GemColor.Green,
            ["blue"] = GemColor.Blue,
            ["white"] = GemColor.White
        };

        private static readonly Dictionary<string, ArmOn> ArmOns = new Dictionary<string, ArmOn>(StringComparer.Ordinal)
        {
            ["flick"] = ArmOn.Flick,
            ["circle"] = ArmOn.Circle
        };

        private const string EvolutionsField = "evolutions";

        private readonly Dictionary<string, GemDef> _byId;
        private readonly Dictionary<int, GemDef> _byNetId;
        private readonly Dictionary<string, string> _formerIds;
        private readonly Dictionary<string, GemDef> _byPath = new Dictionary<string, GemDef>(StringComparer.Ordinal);

        private GemCatalog(List<GemDef> gems, GemLevels levels, Dictionary<string, string> formerIds)
        {
            _formerIds = formerIds;
            All = gems;
            Levels = levels;
            _byId = new Dictionary<string, GemDef>(StringComparer.Ordinal);
            _byNetId = new Dictionary<int, GemDef>();
            foreach (var gem in gems)
            {
                _byId.Add(gem.Id, gem);
                _byNetId.Add(gem.NetId, gem);
                if (gem.Ascendancy != null)
                    _byPath.Add(gem.Ascendancy, gem);
            }
        }

        public IReadOnlyList<GemDef> All { get; }
        public GemLevels Levels { get; }

        public GemDef this[string id] => _byId[id];

        public GemDef? ForPath(string? pathId) => pathId != null && _byPath.TryGetValue(pathId, out var gem) ? gem : null;

        public bool TryGet(string id, out GemDef gem) =>
            _byId.TryGetValue(id, out gem!) || (_formerIds.TryGetValue(id, out var current) && _byId.TryGetValue(current, out gem!));

        public bool TryGetByNetId(int netId, out GemDef gem) => _byNetId.TryGetValue(netId, out gem!);

        public static GemCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var gems = new List<GemDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var netIds = new Dictionary<int, string>();
            var formerIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var forms = new List<ContentNode>();
            foreach (var node in root.Field("gems").Items())
            {
                forms.Clear();
                forms.Add(node);
                if (node.HasField(EvolutionsField))
                    forms.AddRange(node.Field(EvolutionsField).Items());
                var first = gems.Count;
                foreach (var form in forms)
                {
                    var gem = ReadGem(form);
                    ReadFormerIds(form, gem.Id, formerIds);
                    if (!ids.Add(gem.Id))
                        throw form.Field("id").Error($"gem id '{gem.Id}' is used twice");
                    if (netIds.TryGetValue(gem.NetId, out var clash))
                        throw form.Field("id").Error($"gem id '{gem.Id}' has the same network hash as '{clash}', rename one of them");
                    netIds.Add(gem.NetId, gem.Id);
                    if (gem.Ascendancy != null && !paths.Add(gem.Ascendancy))
                        throw form.Field("ascendancy").Error($"path '{gem.Ascendancy}' already has a skill");
                    gems.Add(gem);
                }
                if (forms.Count > 1)
                    BindForms(gems.GetRange(first, forms.Count), forms);
            }
            foreach (var former in formerIds)
            {
                if (ids.Contains(former.Key))
                    throw root.Field("gems").Error($"former id '{former.Key}' of '{former.Value}' is still a gem id");
            }
            BindToSkills(gems, root.Field("gems"));
            return new GemCatalog(gems, GemLevels.Read(root.Field("levels")), formerIds);
        }

        private static void BindForms(List<GemDef> forms, List<ContentNode> nodes)
        {
            for (var i = 0; i < forms.Count; i++)
            {
                var form = forms[i];
                if (form.Type != SkillType.Active || form.Skill?.Weapon == null || form.Ascendancy != null || form.Locked)
                    throw nodes[i].Error("only an open active skill of a class evolves");
                if (i == 0)
                    continue;
                if (nodes[i].HasField(EvolutionsField))
                    throw nodes[i].Field(EvolutionsField).Error("an evolution lists no evolutions of its own, add it to the first form");
                if (form.Skill!.Weapon != forms[0].Skill!.Weapon)
                    throw nodes[i].Field("weapon").Error($"an evolution of '{forms[0].Id}' keeps its weapon '{forms[0].Skill!.Weapon}'");
                if (form.Skill.Tier <= forms[i - 1].Skill!.Tier)
                    throw nodes[i].Field("tier").Error($"an evolution has a higher tier than the form before it, '{forms[i - 1].Id}'");
            }
            forms[0].EvolveInto(forms);
        }

        private static void ReadFormerIds(ContentNode node, string id, Dictionary<string, string> formerIds)
        {
            if (!node.HasField("formerIds"))
                return;
            foreach (var former in node.Field("formerIds").Items())
            {
                var formerId = former.AsString();
                if (formerIds.ContainsKey(formerId))
                    throw former.Error($"former id '{formerId}' is used twice");
                formerIds.Add(formerId, id);
            }
        }

        private static GemDef ReadGem(ContentNode node)
        {
            var id = node.Field("id").AsString();
            var type = Pick(node.Field("kind"), Types, "active, aura or support");
            var color = Pick(node.Field("color"), Colors, "red, green, blue or white");
            var name = node.Field("name");
            var tags = node.HasField("tags") ? ModifierReader.ReadTags(node.Field("tags")) : StatTags.None;
            if ((tags & StatTags.ClassSkill) != 0)
                throw node.Field("tags").Error("ClassSkill is given to every skill with a weapon, do not list it");
            if (type != SkillType.Support && node.HasField("weapon"))
                tags |= StatTags.ClassSkill;
            var nameRu = name.Field("ru").AsString();
            var nameEn = name.Field("en").AsString();
            if (node.HasField("requires"))
                throw node.Field("requires").Error("skills have no attribute requirements: the class attribute strengthens the class feature");
            if (type != SkillType.Active && type != SkillType.Aura && node.HasField("branches"))
                throw node.Field("branches").Error("only active skills and auras have branches");
            if (type != SkillType.Support)
                RejectSupportFields(node);
            switch (type)
            {
                case SkillType.Active:
                case SkillType.Aura:
                    return ReadSkillGem(node, id, type, color, nameRu, nameEn, tags);
                default:
                    return new GemDef(id, GemKind.Support, color, nameRu, nameEn, tags, supportEffect: node.Field("effect").Field("type").AsString(),
                        support: SupportDef.Read(node.Field("effect")), fits: node.HasField("forSkill") ? SkillMechanic.None : SkillMechanics.Read(node.Field("fits")),
                        damageMultiplier: node.Field("damageMult").AsNumber(MinBranchMultiplier, MaxBranchMultiplier),
                        manaMultiplier: node.Field("manaMult").AsNumber(MinBranchMultiplier, MaxBranchMultiplier),
                        unlockLevel: node.Field("level").AsInt(1, 1000),
                        socket: node.HasField("socket") ? node.Field("socket").AsInt(1, Sim.SkillSlot.BuiltInSocket) : 0,
                        forSkill: node.HasField("forSkill") ? node.Field("forSkill").AsString() : null);
            }
        }

        private static void BindToSkills(List<GemDef> gems, ContentNode node)
        {
            var bySkill = new HashSet<string>(StringComparer.Ordinal);
            foreach (var support in gems)
            {
                if (support.ForSkill == null)
                    continue;
                var skill = gems.Find(gem => gem.Id == support.ForSkill);
                if (skill == null || skill.Kind != GemKind.Skill || skill.Ascendancy == null)
                    throw node.Error($"support '{support.Id}' is for '{support.ForSkill}', which is not a skill of a path");
                if (!bySkill.Add(support.ForSkill))
                    throw node.Error($"skill '{support.ForSkill}' already has a great support");
                support.ForPath = skill.Ascendancy;
            }
        }

        private static void RejectSupportFields(ContentNode node)
        {
            foreach (var field in new[] { "fits", "damageMult", "manaMult", "socket", "forSkill" })
            {
                if (node.HasField(field))
                    throw node.Field(field).Error("only a support has this field");
            }
        }

        private static IReadOnlyList<SkillBranchTier> ReadBranches(ContentNode skill, double durationSeconds, List<ContentNode?> overrides)
        {
            if (!skill.HasField("branches"))
                return Array.Empty<SkillBranchTier>();
            var node = skill.Field("branches");
            var items = node.Items();
            if (items.Count != SkillBranchTier.Count)
                throw node.Error($"a skill has {SkillBranchTier.Count} branch tiers, got {items.Count}");
            var tiers = new List<SkillBranchTier>(items.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var previousLevel = 0;
            foreach (var item in items)
            {
                var level = item.Field("level").AsInt(1, 1000);
                if (level <= previousLevel)
                    throw item.Field("level").Error($"tier level {level} must be above the previous tier level {previousLevel}");
                previousLevel = level;
                tiers.Add(new SkillBranchTier(level, ReadOptions(skill, item.Field("options"), ids, durationSeconds, overrides)));
            }
            return tiers;
        }

        private static IReadOnlyList<SkillBranch> ReadOptions(ContentNode skill, ContentNode node, HashSet<string> ids, double durationSeconds, List<ContentNode?> overrides)
        {
            var items = node.Items();
            if (items.Count != SkillBranchTier.OptionCount)
                throw node.Error($"a tier has {SkillBranchTier.OptionCount} branches, got {items.Count}");
            var options = new List<SkillBranch>(items.Count);
            foreach (var item in items)
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"branch id '{id}' is used twice in this skill");
                var name = item.Field("name");
                overrides.Add(item.HasField("effect") ? item.Field("effect") : (ContentNode?)null);
                options.Add(new SkillBranch(id, name.Field("ru").AsString(), name.Field("en").AsString(),
                    BranchMultiplier(item, "damage"), BranchMultiplier(item, "cooldown"), BranchMultiplier(item, "cost"), BranchMultiplier(item, "duration"),
                    item.HasField("effect") ? ReadBranchEffect(skill, item.Field("effect"), durationSeconds) : null));
            }
            return options;
        }

        private static double BranchMultiplier(ContentNode node, string name) =>
            node.HasField(name) ? node.Field(name).AsNumber(MinBranchMultiplier, MaxBranchMultiplier) : 1;

        private static SkillEffect ReadBranchEffect(ContentNode skill, ContentNode overrides, double durationSeconds)
        {
            if (!skill.HasField("effect"))
                throw overrides.Error("a locked skill has no effect to change");
            if (overrides.HasField("type"))
                throw overrides.Field("type").Error("a branch changes the numbers of the effect, not its type");
            return ReadEffect(skill.Field("effect").MergedWith(overrides), durationSeconds);
        }

        private static GemDef ReadSkillGem(ContentNode node, string id, SkillType type, GemColor color, string nameRu, string nameEn, StatTags tags)
        {
            var ascendancy = node.HasField("ascendancy") ? node.Field("ascendancy").AsString() : null;
            var locked = node.HasField("locked") && node.Field("locked").AsBool();
            if (ascendancy != null && !node.HasField("weapon"))
                throw node.Field("ascendancy").Error("a path skill is a class skill and needs a weapon");
            if (locked && !node.HasField("weapon"))
                throw node.Field("locked").Error("only a class skill can wait for its mechanic");
            if (locked == node.HasField("effect"))
                throw node.Error(locked ? "a locked skill has no effect yet" : "a skill needs an effect");
            if (node.HasField("weapon") == node.HasField("level"))
                throw node.Error(node.HasField("weapon") ? "a class skill opens by its tier, it has no level" : "a common skill needs the hero level it opens at");
            var level = node.HasField("level") ? node.Field("level").AsInt(1, 1000) : 1;
            var skill = ReadSkill(node, locked);
            if (!locked && (skill.Effect is AuraEffect) != (type == SkillType.Aura))
                throw node.Field("kind").Error(type == SkillType.Aura ? "an aura needs an aura effect" : "a skill with an aura effect has kind aura");
            var overrides = new List<ContentNode?>();
            var branches = ReadBranches(node, skill.DurationSeconds, overrides);
            if (branches.Count == 0)
                return new GemDef(id, GemKind.Skill, color, nameRu, nameEn, tags, skill: skill, ascendancy: ascendancy, locked: locked, type: type, unlockLevel: level);
            var (variants, damage) = BuildVariants(node, skill, branches, overrides);
            return new GemDef(id, GemKind.Skill, color, nameRu, nameEn, tags, skill: skill, ascendancy: ascendancy, locked: locked,
                type: type, branches: branches, variants: variants, branchDamage: damage, unlockLevel: level);
        }

        private static (SkillDef[] Variants, double[] Damage) BuildVariants(ContentNode node, SkillDef skill, IReadOnlyList<SkillBranchTier> tiers, List<ContentNode?> overrides)
        {
            var variants = new SkillDef[SkillBranches.Combos];
            var damage = new double[SkillBranches.Combos];
            for (var combo = 0; combo < SkillBranches.Combos; combo++)
            {
                double power = 1, cooldown = 1, cost = 1, duration = 1;
                var effect = node.HasField("effect") ? node.Field("effect") : (ContentNode?)null;
                var changed = false;
                for (var tier = 0; tier < tiers.Count; tier++)
                {
                    var choice = SkillBranches.Choice(combo, tier);
                    if (choice == SkillBranches.NoChoice)
                        continue;
                    var branch = tiers[tier].Options[choice];
                    power *= branch.DamageMultiplier;
                    cooldown *= branch.CooldownMultiplier;
                    cost *= branch.CostMultiplier;
                    duration *= branch.DurationMultiplier;
                    var change = overrides[tier * SkillBranchTier.OptionCount + choice];
                    if (change != null && effect != null)
                    {
                        effect = effect.Value.MergedWith(change.Value);
                        changed = true;
                    }
                }
                var seconds = skill.DurationSeconds * duration;
                variants[combo] = skill.With(skill.ResourceCost * cost, skill.CooldownSeconds * cooldown, seconds,
                    changed ? ReadEffect(effect!.Value, seconds) : skill.Effect);
                damage[combo] = power;
            }
            return (variants, damage);
        }

        private static SkillDef ReadSkill(ContentNode node, bool locked)
        {
            var cost = node.Field("cost");
            var resource = cost.HasField("resource") ? cost.Field("resource").AsNumber(0, 1e6) : 0;
            var duration = node.HasField("durationSeconds") ? node.Field("durationSeconds").AsNumber(0, 3600) : 0;
            var effect = locked ? new PassiveEffect(Array.Empty<Modifier>(), false) : ReadEffect(node.Field("effect"), duration);
            if (effect is AuraEffect aura && !aura.Reserves && duration <= 0)
                throw node.Error("an aura needs durationSeconds or reserve");
            if ((effect is KeyRepeatEffect || effect is StreakStrikeEffect || effect is InputHitsEffect || effect is TrailEffect || (effect is StacksEffect stacks && !stacks.Passive)) && duration <= 0)
                throw node.Error($"a {node.Field("effect").Field("type").AsString()} skill needs durationSeconds");
            var weapon = node.HasField("weapon") ? node.Field("weapon").AsString() : null;
            if (effect is AuraEffect classAura && weapon != null && !classAura.Reserves)
                throw node.Field("effect").Error("a class aura reserves resource: add reserve");
            var native = node.HasField("native") ? ReadNative(node.Field("native")) : null;
            if (native != null && weapon == null)
                throw node.Field("native").Error("a native trait needs a weapon: only class skills have one");
            var family = node.HasField("family") ? node.Field("family").AsString() : null;
            var armOn = node.HasField("armOn") ? Pick(node.Field("armOn"), ArmOns, "flick or circle") : ArmOn.None;
            if (weapon == null && (node.HasField("tier") || node.HasField("role")))
                throw node.Error("tier and role belong to class skills: add a weapon");
            var tier = node.HasField("tier") ? Pick(node.Field("tier"), Tiers, "common, rare, epic, legendary or mythic") : SkillTier.Common;
            var role = node.HasField("role") ? Pick(node.Field("role"), Roles, "fast or heavy") : SkillRole.Other;
            return new SkillDef(resource, node.Field("cooldownSeconds").AsNumber(0, 3600), duration, effect, weapon, native, family, armOn, tier, role);
        }

        private static StrikeEffect ReadStrike(ContentNode node, double multiplier)
        {
            return new StrikeEffect(
                multiplier,
                node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0,
                node.HasField("skipCurrent") && node.Field("skipCurrent").AsBool(),
                node.HasField("killRefundShare") ? node.Field("killRefundShare").AsNumber(0, 1) : 0,
                Number(node, "queueMultiplier", 1e6),
                Number(node, "executeBelow", 1),
                node.HasField("coinFlip") && node.Field("coinFlip").AsBool(),
                Number(node, "critChance", 1),
                node.HasField("arrow") && node.Field("arrow").AsBool())
            {
                LowHpBelow = Number(node, "lowHpBelow", 1),
                LowHpMultiplier = Number(node, "lowHpMultiplier", 1e6),
                BossMultiplier = Number(node, "bossMultiplier", 1000),
                LastKeyHit = node.HasField("lastKeyHit") && node.Field("lastKeyHit").AsBool(),
                PauseSeconds = Number(node, "pauseSeconds", 3600),
                PauseMultiplier = Number(node, "pauseMultiplier", 1e6),
                BossTimerStopSeconds = Number(node, "bossTimerStopSeconds", 600),
                Stacks = node.HasField("stacks") ? ReadCastStacks(node.Field("stacks")) : null,
                ConsumeMinion = node.HasField("consumeMinion") && node.Field("consumeMinion").AsBool(),
                BurnStacks = Number(node, "burnStacks", 1000),
                Runes = node.HasField("runes") ? node.Field("runes").AsInt(0, 100) : 0,
                Word = node.HasField("word") && node.Field("word").AsBool(),
                Blades = node.HasField("blades") ? node.Field("blades").AsInt(0, 100) : 0,
                BladeRain = node.HasField("bladeRain") && node.Field("bladeRain").AsBool(),
                WhirlSeconds = Number(node, "whirlSeconds", 600),
                Breach = node.HasField("breach") && node.Field("breach").AsBool(),
                Touches = node.HasField("touches") ? node.Field("touches").AsInt(0, 100) : 0,
                PhantomSeconds = Number(node, "phantomSeconds", 600),
                Tension = Number(node, "tension", 1000),
                Gusts = node.HasField("gusts") ? node.Field("gusts").AsInt(0, 100) : 0,
                CalmSeconds = Number(node, "calmSeconds", 600),
                Shot = node.HasField("shot") && node.Field("shot").AsBool(),
                Crystals = node.HasField("crystals") ? node.Field("crystals").AsInt(0, 100) : 0,
                Shatter = Number(node, "shatter", 100),
                Grow = node.HasField("grow") ? node.Field("grow").AsInt(0, 100) : 0,
                Crack = node.HasField("crack") && node.Field("crack").AsBool(),
                PrisonSeconds = Number(node, "prisonSeconds", 600),
                HeartSeconds = Number(node, "heartSeconds", 600),
                Resonance = Number(node, "resonance", 1000),
                PerCrystal = Number(node, "perCrystal", 100),
                Sand = Number(node, "sand", 1000),
                Pour = node.HasField("pour") ? node.Field("pour").AsInt(0, 100_000) : 0,
                Camp = node.HasField("camp") && node.Field("camp").AsBool(),
                Mirages = node.HasField("mirages") ? node.Field("mirages").AsInt(0, 100) : 0,
                TopHit = Number(node, "topHit", 100),
                AfterStep = Number(node, "afterStep", 100),
                StepHit = Number(node, "stepHit", 100),
                BlindSeconds = Number(node, "blindSeconds", 600),
                TimerSeconds = Number(node, "timerSeconds", 600),
                Lines = node.HasField("lines") ? node.Field("lines").AsInt(0, 100) : 0,
                Notes = node.HasField("notes") ? node.Field("notes").AsInt(0, 100) : 0,
                Tune = node.HasField("tune") && node.Field("tune").AsBool(),
                ClimaxHit = Number(node, "climaxHit", 100),
                Finale = Number(node, "finale", 100),
                HymnSeconds = Number(node, "hymnSeconds", 600),
                HymnPower = Number(node, "hymnPower", 10),
                SoloSeconds = Number(node, "soloSeconds", 600),
                Dream = Number(node, "dream", 1000),
                CaughtHit = Number(node, "caughtHit", 100),
                Nets = node.HasField("nets") ? node.Field("nets").AsInt(0, 100) : 0,
                AlbumAdd = node.HasField("album") ? node.Field("album").AsInt(0, 1000) : 0,
                Bait = node.HasField("bait") ? node.Field("bait").AsInt(0, 100_000) : 0,
                DozeSeconds = Number(node, "dozeSeconds", 600),
                LucidSeconds = Number(node, "lucidSeconds", 600),
                LitHit = Number(node, "litHit", 100),
                Oil = Number(node, "oil", 1000),
                Light = Number(node, "light", 1),
                Fireflies = node.HasField("fireflies") ? node.Field("fireflies").AsInt(0, 100) : 0,
                Flash = node.HasField("flash") && node.Field("flash").AsBool(),
                GlowSeconds = Number(node, "glowSeconds", 600),
                GlowPerOil = Number(node, "glowPerOil", 1),
                Curse = Number(node, "curse", 10),
                CurseSeconds = Number(node, "curseSeconds", 600),
                CurseHit = Number(node, "curseHit", 1000),
                ExtendCurse = Number(node, "extendCurse", 600),
                Whisper = Number(node, "whisper", 1000),
                Knots = node.HasField("knots") ? node.Field("knots").AsInt(0, 7) : 0,
                Seventh = node.HasField("seventh") && node.Field("seventh").AsBool(),
                NameHits = node.HasField("nameHits") ? node.Field("nameHits").AsInt(0, 100_000) : 0,
                Hush = node.HasField("hush") ? node.Field("hush").AsInt(0, 100) : 0,
                Wind = Number(node, "wind", 1000),
                WindHit = Number(node, "windHit", 1000),
                HoldSeconds = Number(node, "holdSeconds", 600),
                CutCooldowns = Number(node, "cutCooldowns", 600),
                WindBurst = Number(node, "windBurst", 100),
                Rewind = node.HasField("rewind") && node.Field("rewind").AsBool(),
                GearTick = node.HasField("gearTick") && node.Field("gearTick").AsBool(),
                Reagents = node.HasField("reagents") ? node.Field("reagents").AsInt(0, 10) : 0,
                Flask = node.HasField("flask") && node.Field("flask").AsBool(),
                Layers = node.HasField("layers") ? node.Field("layers").AsInt(0, 20) : 0,
                Elixir = node.HasField("elixir") && node.Field("elixir").AsBool(),
                Corrode = Number(node, "corrode", 10),
                CorrodeSeconds = Number(node, "corrodeSeconds", 600),
                VeinHits = Number(node, "veinHits", 1000),
                Luck = Number(node, "luck", 1),
                Nuggets = node.HasField("nuggets") ? node.Field("nuggets").AsInt(0, 100) : 0,
                GoldHit = Number(node, "goldHit", 1000),
                VeinGold = Number(node, "veinGold", 1000),
                Souls = node.HasField("souls") ? node.Field("souls").AsInt(0, 16) : 0,
                SpiritsHit = Number(node, "spiritsHit", 1000),
                ReleaseAll = node.HasField("releaseAll") && node.Field("releaseAll").AsBool(),
                Lend = node.HasField("lend") && node.Field("lend").AsBool(),
                Breathe = node.HasField("breathe") && node.Field("breathe").AsBool(),
                Cannons = node.HasField("cannons") ? node.Field("cannons").AsInt(0, 20) : 0,
                Volley = node.HasField("volley") && node.Field("volley").AsBool(),
                Sailors = node.HasField("sailors") ? node.Field("sailors").AsInt(0, 100) : 0,
                CrewHit = Number(node, "crewHit", 1000),
                ParrotRepeat = node.HasField("parrotRepeat") && node.Field("parrotRepeat").AsBool(),
                Moths = node.HasField("moths") ? node.Field("moths").AsInt(0, 200) : 0,
                SwarmHit = Number(node, "swarmHit", 1000),
                Wake = node.HasField("wake") && node.Field("wake").AsBool(),
                Cards = node.HasField("cards") ? node.Field("cards").AsInt(0, 20) : 0,
                Arcana = node.HasField("arcana") && node.Field("arcana").AsBool(),
                Reveal = node.HasField("reveal") && node.Field("reveal").AsBool(),
                HandHit = Number(node, "handHit", 1000),
                Forecast = node.HasField("forecast") && node.Field("forecast").AsBool(),
                Seeds = node.HasField("seeds") ? node.Field("seeds").AsInt(0, 20) : 0,
                Bloom = node.HasField("bloom") && node.Field("bloom").AsBool(),
                Venom = Number(node, "venom", 1000),
                VenomBurst = Number(node, "venomBurst", 100),
                Drink = node.HasField("drink") && node.Field("drink").AsBool(),
                SealHits = node.HasField("sealHits") ? node.Field("sealHits").AsInt(0, 1000) : 0,
                GolemHit = Number(node, "golemHit", 1000),
                GolemSeconds = Number(node, "golemSeconds", 600),
                CompleteSeal = node.HasField("seal") && node.Field("seal").AsBool(),
                GolemRunes = node.HasField("golemRunes") ? node.Field("golemRunes").AsInt(0, 100) : 0,
                Golemlings = node.HasField("golemlings") ? node.Field("golemlings").AsInt(0, 20) : 0
            };
        }

        private static SkillEffect ReadEffect(ContentNode node, double durationSeconds)
        {
            var typeNode = node.Field("type");
            switch (typeNode.AsString())
            {
                case "chainHits":
                    return new ChainHitsEffect(node.Field("hits").AsInt(1, 100000), node.Field("nextEnemyShare").AsNumber(0, 100));
                case "strike":
                    return ReadStrike(node, node.Field("multiplier").AsNumber(0, 1e6));
                case "aura":
                    return new AuraEffect(
                        ModifierReader.ReadList(node.Field("modifiers")),
                        node.HasField("party") && node.Field("party").AsBool(),
                        node.HasField("reserve") ? node.Field("reserve").AsNumber(0, 100) : 0,
                        node.HasField("focusLockAfterSeconds") ? node.Field("focusLockAfterSeconds").AsNumber(0, 3600) : 0,
                        node.HasField("keylessStrike") ? node.Field("keylessStrike").Field("everySeconds").AsNumber(0.1, 3600) : 0,
                        node.HasField("keylessStrike") ? node.Field("keylessStrike").Field("multiplier").AsNumber(0, 1e6) : 0)
                    {
                        IdleOffSeconds = Number(node, "idleOffSeconds", 3600),
                        SummonEveryKeys = node.HasField("summonEveryKeys") ? node.Field("summonEveryKeys").AsInt(1, 100000) : 0,
                        AllyResource = Number(node, "allyResource", 1000)
                    };
                case "keyRepeat":
                    return new KeyRepeatEffect(node.Field("every").AsInt(1, 10000), node.Field("multiplier").AsNumber(0, 1000))
                    {
                        Hits = node.HasField("hits") ? node.Field("hits").AsInt(1, 10000) : 0,
                        QueueTargets = node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0,
                        Input = node.HasField("input") ? Pick(node.Field("input"), Inputs, "keys, mouse or any") : HitInput.Keys,
                        BurnStacks = Number(node, "burnStacks", 1000)
                    };
                case "streakStrike":
                    return new StreakStrikeEffect(
                        node.Field("hits").AsInt(1, 10000),
                        node.Field("maxGapSeconds").AsNumber(0.1, 3600),
                        node.Field("multiplier").AsNumber(0, 1e6));
                case "flow":
                    return new FlowEffect(
                        node.Field("afterSeconds").AsNumber(0.1, 86400),
                        node.Field("maxGapSeconds").AsNumber(0.1, 3600),
                        ModifierReader.ReadList(node.Field("modifiers")));
                case "flurry":
                    return new FlurryEffect(
                        node.Field("hits").AsInt(1, 10000),
                        node.Field("seconds").AsNumber(0.1, 3600),
                        node.Field("multiplier").AsNumber(0, 1e6),
                        node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0)
                    {
                        Cast = node.HasField("cast") ? ReadStrike(node.Field("cast"), 0) : null
                    };
                case "inputHits":
                    return new InputHitsEffect(
                        Number(node, "pxPerHit", 1e6),
                        Number(node, "pxShare", 1000),
                        Number(node, "circle", 1000),
                        Number(node, "flick", 1000),
                        Number(node, "reversal", 1000),
                        node.HasField("maxHits") ? node.Field("maxHits").AsInt(1, 10000) : 0)
                    {
                        QueueTargets = node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0
                    };
                case "distanceStacks":
                    return new DistanceStacksEffect(
                        node.Field("pxPerStack").AsNumber(1, 1e9),
                        node.Field("windowSeconds").AsInt(1, 3600),
                        new StacksEffect(StackTrigger.Kill, ModifierReader.Read(node.Field("perStack")), node.Field("max").AsNumber(0, 1e6), 0));
                case "trail":
                    return new TrailEffect(node.Field("pxPerMultiplier").AsNumber(1, 1e9));
                case "bossHunt":
                    return new BossHuntEffect(
                        node.Field("bonus").AsNumber(0, 1000),
                        node.Field("lastSeconds").AsNumber(0, 3600),
                        node.Field("lastBonus").AsNumber(0, 1000));
                case "composure":
                    return new ComposureEffect(node.Field("multiplier").AsNumber(1, 1000));
                case "passive":
                    return new PassiveEffect(
                        ModifierReader.ReadList(node.Field("modifiers")),
                        node.HasField("perPartyMember") && node.Field("perPartyMember").AsBool())
                    {
                        When = node.HasField("when") ? ReadCondition(node.Field("when")) : null
                    };
                case "breakBonus":
                    return new BreakBonusEffect(
                        node.Field("breakSeconds").AsNumber(1, 86400),
                        node.Field("seconds").AsNumber(0.1, 86400),
                        ModifierReader.ReadList(node.Field("modifiers")));
                case "gainResource":
                    return new ResourceGainEffect(node.Field("amount").AsNumber(0, 1e6));
                case "echo":
                    return new EchoEffect(node.Field("power").AsNumber(0, 10));
                case "nextKillReward":
                    return new NextKillRewardEffect(
                        node.Field("multiplier").AsNumber(1, 1000),
                        Number(node, "shardChance", 1),
                        Number(node, "shards", 1e6));
                case "idleStrike":
                    return new IdleStrikeEffect(
                        node.Field("chargePerSecond").AsNumber(0, 1000),
                        node.Field("maxChargeSeconds").AsNumber(0.1, 86400),
                        node.Field("minIdleSeconds").AsNumber(0, 86400));
                default:
                    throw typeNode.Error($"unknown skill effect '{typeNode.AsString()}', expected chainHits, strike, aura, keyRepeat, streakStrike, flow, flurry, inputHits, distanceStacks, trail, bossHunt, composure, passive, breakBonus, gainResource, echo, nextKillReward or idleStrike");
            }
        }

        private static StacksEffect ReadCastStacks(ContentNode node) =>
            new StacksEffect(
                StackTrigger.KeyHit,
                ModifierReader.Read(node.Field("perStack")),
                node.Field("max").AsNumber(0, 1e6),
                node.Field("stackSeconds").AsNumber(0.1, 3600))
            {
                Party = node.HasField("party") && node.Field("party").AsBool()
            };

        private static NativeTrait ReadNative(ContentNode node) =>
            new NativeTrait
            {
                Modifiers = node.HasField("modifiers") ? ModifierReader.ReadList(node.Field("modifiers")) : Array.Empty<Modifier>(),
                HoldDecaySeconds = Number(node, "holdDecaySeconds", 3600),
                SpendAllPerPoint = node.HasField("spendAll") ? node.Field("spendAll").Field("perPoint").AsNumber(0, 1000) : 0,
                SpendAllMax = node.HasField("spendAll") ? node.Field("spendAll").Field("max").AsNumber(0, 1e6) : 0,
                ResourcePerKeyHit = Number(node, "resourcePerKeyHit", 100),
                WoundedSpawnResource = Number(node, "woundedSpawnResource", 100),
                HoldStacksAboveResource = Number(node, "holdStacksAboveResource", 100),
                FullResourceDurationMultiplier = Number(node, "fullResourceDurationMultiplier", 100),
                ExtraTargetWhileFamily = node.HasField("extraTargetWhileFamily") ? node.Field("extraTargetWhileFamily").AsString() : null,
                RefreshFamily = node.HasField("refreshFamily") ? node.Field("refreshFamily").AsString() : null,
                FreeAboveCombo = Number(node, "freeAboveCombo", 1e6),
                CarryOn = node.HasField("carryOn") && node.Field("carryOn").AsBool(),
                HoldCombo = node.HasField("holdCombo") && node.Field("holdCombo").AsBool(),
                FullResourceBonus = Number(node, "fullResourceBonus", 1000),
                RepeatAboveCombo = Number(node, "repeatAboveCombo", 1e6),
                BossReleaseShare = Number(node, "bossReleaseShare", 100),
                ResourceOnCast = Number(node, "resourceOnCast", 1000),
                ResourceOnKill = Number(node, "resourceOnKill", 1000),
                ResourceOnHit = Number(node, "resourceOnHit", 1000),
                ResourceOnCrit = Number(node, "resourceOnCrit", 1000),
                ResourcePerPoisonTick = Number(node, "resourcePerPoisonTick", 1000),
                MouseEverySeconds = node.HasField("mouseEverySeconds") ? node.Field("mouseEverySeconds").AsNumber(0.1, 3600) : 0,
                ResourceOnEnd = Number(node, "resourceOnEnd", 1000),
                WeakenedGold = Number(node, "weakenedGold", 100),
                StackSeconds = Number(node, "stackSeconds", 3600),
                CarryOver = node.HasField("carryOver") && node.Field("carryOver").AsBool(),
                WindowSeconds = Number(node, "windowSeconds", Sim.DamageWindow.Seconds),
                StackGrowth = Number(node, "stackGrowth", 10),
                ResourceRelief = Number(node, "resourceRelief", 1000),
                PerMinion = Number(node, "perMinion", 10),
                Barrels = node.HasField("barrels") ? node.Field("barrels").AsInt(1, 100) : 0,
                RespawnMinion = node.HasField("respawnMinion") && node.Field("respawnMinion").AsBool(),
                RepeatEvery = node.HasField("repeatEvery") ? node.Field("repeatEvery").AsInt(1, 10000) : 0,
                WhenResource = node.HasField("when") ? Number(node.Field("when"), "resource", 1000) : 0,
                ResourcePerSavedSecond = Number(node, "resourcePerSavedSecond", 1000),
                RestoresFocus = node.HasField("restoresFocus") && node.Field("restoresFocus").AsBool(),
                BossTimerSeconds = Number(node, "bossTimerSeconds", 60),
                LastsAboveResource = Number(node, "lastsAboveResource", 1000),
                WeakenShare = Number(node, "weakenShare", 0.99),
                PartyStacks = node.HasField("partyStacks") && node.Field("partyStacks").AsBool(),
                ExtraSecondsPerPoint = Number(node, "extraSecondsPerPoint", 100),
                BestOfTwo = node.HasField("bestOfTwo") && node.Field("bestOfTwo").AsBool(),
                QueueTargets = node.HasField("queueTargets") ? node.Field("queueTargets").AsInt(0, Sim.GameState.QueueDepth) : 0,
                ResetWithinSeconds = Number(node, "resetWithinSeconds", 600),
                RepeatInput = node.HasField("repeatInput") ? Pick(node.Field("repeatInput"), Inputs, "keys, mouse or any") : (HitInput?)null,
                ExtraHitPerMousePx = Number(node, "extraHitPerMousePx", 1e6),
                MaxHits = node.HasField("maxHits") ? node.Field("maxHits").AsInt(1, 1000) : 0,
                ReleaseBonusPx = node.HasField("releaseBonus") ? node.Field("releaseBonus").Field("px").AsNumber(1, 1e6) : 0,
                ReleaseBonus = node.HasField("releaseBonus") ? node.Field("releaseBonus").Field("bonus").AsNumber(0, 100) : 0,
                ComboOnCast = node.HasField("comboOnCast") ? node.Field("comboOnCast").AsInt(0, 10000) : 0,
                ComboPerHit = node.HasField("comboPerHit") ? node.Field("comboPerHit").AsInt(0, 10000) : 0,
                ComboPerKeyHit = node.HasField("comboPerKeyHit") ? node.Field("comboPerKeyHit").AsInt(0, 10000) : 0,
                DurationMultiplier = Number(node, "durationMultiplier", 100),
                When = node.HasField("when") ? ReadCondition(node.Field("when")) : null,
                WhenModifiers = node.HasField("when") && node.Field("when").HasField("modifiers") ? ModifierReader.ReadList(node.Field("when").Field("modifiers")) : Array.Empty<Modifier>(),
                WhenMultiplier = node.HasField("when") ? Number(node.Field("when"), "multiplier", 1000) : 0,
                WhenExecuteBelow = node.HasField("when") ? Number(node.Field("when"), "executeBelow", 1) : 0,
                WhenFree = node.HasField("when") && node.Field("when").HasField("free") && node.Field("when").Field("free").AsBool(),
                WhenDurationMultiplier = node.HasField("when") ? Number(node.Field("when"), "durationMultiplier", 100) : 0,
                WhenExtraTargets = node.HasField("when") && node.Field("when").HasField("extraTargets") ? node.Field("when").Field("extraTargets").AsInt(0, Sim.GameState.QueueDepth) : 0
            };

        private static SkillCondition ReadCondition(ContentNode node) =>
            new SkillCondition
            {
                ResourceAtLeast = Number(node, "resourceAtLeast", 1000),
                ResourceAtMost = Number(node, "resourceAtMost", 1000),
                ComboAbove = node.HasField("comboAbove") ? node.Field("comboAbove").AsInt(0, 1000000) : 0,
                BossLastSeconds = Number(node, "bossLastSeconds", 3600),
                Boss = node.HasField("boss") && node.Field("boss").AsBool(),
                BossRetry = node.HasField("bossRetry") && node.Field("bossRetry").AsBool(),
                FamilyActive = node.HasField("familyActive") ? node.Field("familyActive").AsString() : null,
                MouseMoving = node.HasField("mouseMoving") && node.Field("mouseMoving").AsBool(),
                Reading = node.HasField("reading") && node.Field("reading").AsBool(),
                DotStacksAtLeast = Number(node, "dotStacksAtLeast", 1e6),
                MinionsAtLeast = Count(node, "minionsAtLeast"),
                AurasAtLeast = Count(node, "aurasAtLeast")
            };

        private static int Count(ContentNode node, string field) =>
            node.HasField(field) ? node.Field(field).AsInt(0, 1000) : 0;

        private static double Number(ContentNode node, string field, double max) =>
            node.HasField(field) ? node.Field(field).AsNumber(0, max) : 0;

        private static T Pick<T>(ContentNode node, Dictionary<string, T> names, string expected)
        {
            var text = node.AsString();
            if (!names.TryGetValue(text, out var value))
                throw node.Error($"unknown value '{text}', expected {expected}");
            return value;
        }
    }
}
