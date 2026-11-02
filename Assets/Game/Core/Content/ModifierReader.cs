using System;
using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public static class ModifierReader
    {
        private static readonly Dictionary<string, ModifierKind> Kinds = new Dictionary<string, ModifierKind>(StringComparer.Ordinal)
        {
            ["flat"] = ModifierKind.Flat,
            ["increased"] = ModifierKind.Increased,
            ["more"] = ModifierKind.More
        };

        private static readonly Dictionary<string, CoopTarget> Targets = new Dictionary<string, CoopTarget>(StringComparer.Ordinal)
        {
            ["self"] = CoopTarget.Self,
            ["party"] = CoopTarget.Party,
            ["allies"] = CoopTarget.Allies,
            ["enemy"] = CoopTarget.Enemy
        };

        public static Modifier Read(ContentNode node)
        {
            if (node.HasField("to"))
                return ReadCoop(node);
            var kind = ReadKind(node.Field("kind"));
            var tags = node.HasField("tags") ? ReadTags(node.Field("tags")) : StatTags.None;
            var stat = new StatId(node.Field("stat").AsString());
            if (node.HasField("skill"))
                stat = StatId.ForSkill(stat, node.Field("skill").AsString());
            var when = node.HasField("when") ? ReadCondition(node.Field("when")) : ModifierCondition.Always;
            return new Modifier(stat, kind, node.Field("value").AsNumber(-1e6, 1e6), tags, ModifierPool.General, when);
        }

        private static Modifier ReadCoop(ContentNode node)
        {
            var line = ReadCoopLine(node);
            var when = node.HasField("when") ? ReadCondition(node.Field("when")) : ModifierCondition.Always;
            return new Modifier(line.Source, ModifierKind.Flat, node.Field("value").AsNumber(-1e6, 1e6), StatTags.None, ModifierPool.General, when);
        }

        public static CoopLine ReadCoopLine(ContentNode node)
        {
            if (node.HasField("tags") || node.HasField("skill"))
                throw node.Error("a party buff line takes no tags or skill");
            var toNode = node.Field("to");
            if (!Targets.TryGetValue(toNode.AsString(), out var to))
                throw toNode.Error($"unknown party buff target '{toNode.AsString()}', expected self, party, allies or enemy");
            var target = new StatId(node.Field("stat").AsString());
            var kind = ReadKind(node.Field("kind"));
            if (to == CoopTarget.Enemy && !(CoopRules.IsEnemyStat(target) && kind == ModifierKind.Flat))
                throw node.Field("stat").Error($"an enemy line takes flat {CoopRules.EnemyDamageTaken} or {CoopRules.EnemyTimerSlow}");
            if (to != CoopTarget.Enemy && target == CoopRules.EnemyDamageTaken)
                throw node.Field("stat").Error($"{CoopRules.EnemyDamageTaken} goes only to the enemy");
            var perSignal = CoopSignal.None;
            var perStep = 0.0;
            var perCap = 0;
            if (node.HasField("per"))
            {
                var per = node.Field("per");
                perSignal = ReadSignal(per.Field("signal"));
                if (CoopSignals.IsEvent(perSignal))
                    throw per.Field("signal").Error("'per' counts a gauge, events go to 'after'");
                perStep = per.Field("step").AsNumber(1e-6, 1e9);
                perCap = per.Field("cap").AsInt(1, 100000);
            }
            var afterSignal = CoopSignal.None;
            var afterSeconds = 0.0;
            var afterStacks = 0;
            if (node.HasField("after"))
            {
                var after = node.Field("after");
                afterSignal = ReadSignal(after.Field("signal"));
                if (!CoopSignals.IsEvent(afterSignal))
                    throw after.Field("signal").Error("'after' waits for an event, gauges go to 'per'");
                afterSeconds = after.Field("seconds").AsNumber(0.1, 3600);
                afterStacks = after.HasField("stacks") ? after.Field("stacks").AsInt(1, 100) : 1;
            }
            StatId? shareStat = null;
            var shareKind = ModifierKind.Flat;
            if (node.HasField("share"))
            {
                var share = node.Field("share");
                shareStat = new StatId(share.Field("stat").AsString());
                shareKind = ReadKind(share.Field("kind"));
            }
            var whileSignal = CoopSignal.None;
            if (node.HasField("while"))
            {
                whileSignal = ReadSignal(node.Field("while"));
                if (CoopSignals.IsEvent(whileSignal))
                    throw node.Field("while").Error("'while' holds on a gauge, events go to 'after'");
            }
            return CoopLine.Register(to, target, kind, perSignal, perStep, perCap, afterSignal, afterSeconds, afterStacks, shareStat, shareKind, whileSignal);
        }

        private static CoopSignal ReadSignal(ContentNode node)
        {
            if (!CoopSignals.TryParse(node.AsString(), out var signal))
                throw node.Error($"unknown party buff signal '{node.AsString()}'");
            return signal;
        }

        public static ModifierCondition ReadCondition(ContentNode node)
        {
            var name = node.AsString();
            if (!ModifierConditions.TryParse(name, out var condition))
                throw node.Error($"unknown condition '{name}'");
            return condition;
        }

        public static ModifierKind ReadKind(ContentNode node)
        {
            var kindName = node.AsString();
            if (!Kinds.TryGetValue(kindName, out var kind))
                throw node.Error($"unknown modifier kind '{kindName}', expected flat, increased or more");
            return kind;
        }

        public static IReadOnlyList<Modifier> ReadList(ContentNode node)
        {
            var modifiers = new List<Modifier>();
            foreach (var item in node.Items())
                modifiers.Add(Read(item));
            return modifiers;
        }

        public static StatTags ReadTags(ContentNode node)
        {
            var tags = StatTags.None;
            foreach (var tagNode in node.Items())
            {
                var tagName = tagNode.AsString();
                if (!Enum.TryParse<StatTags>(tagName, false, out var tag) || tag == StatTags.None)
                    throw tagNode.Error($"unknown tag '{tagName}'");
                tags |= tag;
            }
            return tags;
        }
    }
}
