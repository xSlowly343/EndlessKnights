using System;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core.Party;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class PartyTrialVectorTests
    {
        private const int Fighters = 3;
        private const int Every = 100;
        private static readonly string VectorPath = Path.Combine("Server", "test-vectors", "party-trial.json");
        private static readonly int[] Tiers = { 1, 3, 6 };

        private static PartyTrialRules Rules => TestWorld.Data.Party.Growth.Trials;

        [Test]
        public void ServerTrialVectorsMatchTheCoreFight()
        {
            var json = Build();
            var path = PartyVectorTests.RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, json);
                Assert.Pass("vectors written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(json), $"vectors are stale, run the tests with {PartyVectorTests.UpdateVariable}=1");
        }

        public static PartyTrialHit HitAt(long tick, int fighter) => new PartyTrialHit(
            ((tick * 7 + fighter * 3) % 11) * 0.000025,
            ((tick * 5 + fighter) % 7) * 0.000017,
            ((tick * 3 + fighter * 5) % 13) * 0.0000125,
            ((tick + fighter * 2) % 5) * 0.0000085,
            (tick + fighter) % 17 != 0);

        private static string Build()
        {
            var text = new StringBuilder("{\n  \"version\": 1,\n  \"fighters\": " + Fighters + ",\n  \"every\": " + Every + ",\n  \"scenarios\": [\n");
            var first = true;
            foreach (var trial in Rules.List)
            {
                for (var t = 0; t < Tiers.Length; t++)
                {
                    if (!first)
                        text.Append(",\n");
                    first = false;
                    Scenario(text, trial, Tiers[t], t % 2 == 1, (t == 2 ? 0.7 : 1) * (trial.Rule == PartyTrialRule.Marathon ? 12 : 1));
                }
            }
            return text.Append("\n  ],\n  \"state\": { \"trial\": \"shields\", \"tier\": 4, \"night\": true, \"keys\": 0.05, \"hex\": \"").Append(StateHex()).Append("\" }\n}\n").ToString();
        }

        private static string StateHex()
        {
            var encounter = new PartyEncounter(TestWorld.Data.Party, TestWorld.Data.Stages, 42) { Trials = Rules, Defense = TestWorld.Data.Combat.Defense };
            encounter.Join(76561198000000001UL);
            encounter.Join(76561198000000002UL);
            encounter.ProposeWorld(PartyWorld.Trial, PartyTrialId.Pack(Rules.Find("shields")!.Index, 4, true));
            var switchTicks = (int)System.Math.Round(TestWorld.Data.Party.WorldSwitchSeconds * Game.Core.Sim.GameLoop.TicksPerSecond);
            for (var i = 0; i < switchTicks; i++)
                encounter.Advance();
            encounter.ReportInput(0);
            encounter.ReportInput(1);
            encounter.AddTrialDamage(0, 0.05, 0, 0, 0);
            encounter.Advance();
            var buffer = new byte[Game.Core.Party.Wire.PartyCodec.MaxPacketBytes];
            var length = Game.Core.Party.Wire.PartyCodec.Encode(buffer, Game.Core.Party.Wire.TrialStateMessage.Of(encounter));
            var hex = new StringBuilder(length * 2);
            for (var i = 0; i < length; i++)
                hex.Append(buffer[i].ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }

        private static void Scenario(StringBuilder text, PartyTrialDef trial, int tier, bool night, double power)
        {
            var fight = new PartyTrialFight();
            fight.Start(Rules, TestWorld.Data.Combat.Defense, trial, tier, Fighters, power, night);
            var hits = new PartyTrialHit[Fighters];
            text.Append("    { \"trial\": ").Append(trial.Index).Append(", \"tier\": ").Append(tier).Append(", \"night\": ").Append(night ? "true" : "false")
                .Append(", \"power\": ").Append(N(power)).Append(", \"checks\": [");
            var checks = 0;
            while (fight.Running)
            {
                for (var f = 0; f < Fighters; f++)
                    hits[f] = HitAt(fight.Tick, f);
                fight.Advance(hits, Fighters);
                if (fight.Tick % Every != 0 && fight.Running)
                    continue;
                text.Append(checks++ == 0 ? "\n" : ",\n").Append("      ").Append(Check(fight));
            }
            text.Append("\n    ], \"result\": ").Append(fight.ResultTier).Append(" }");
        }

        private static string Check(PartyTrialFight fight) =>
            "{ \"tick\": " + fight.Tick +
            ", \"outcome\": " + (int)fight.Outcome +
            ", \"hpLeft\": " + N(fight.HpLeft) +
            ", \"hpFull\": " + N(fight.HpFull) +
            ", \"shields\": [" + N(fight.ShieldLeft(PartyTrialDamage.Keys)) + ", " + N(fight.ShieldLeft(PartyTrialDamage.Mouse)) + ", " +
            N(fight.ShieldLeft(PartyTrialDamage.Skills)) + ", " + N(fight.ShieldLeft(PartyTrialDamage.Minions)) + "]" +
            ", \"skillShield\": " + N(fight.SkillShieldLeft) +
            ", \"holder\": " + fight.Holder +
            ", \"resisted\": " + fight.Resisted +
            ", \"chain\": " + fight.Chain +
            ", \"wave\": " + fight.Wave +
            ", \"wavesCleared\": " + fight.WavesCleared +
            ", \"armor\": " + N(fight.Armor) + " }";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
