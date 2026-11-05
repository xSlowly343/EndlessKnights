using Game.Core.Content.Synergy;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Party
{
    public class SynergyMechanicsTests
    {
        private static SynergyRules Rules => TestWorld.Data.Synergy;

        private static int Mark(string mark) => Rules.MaskOf(new[] { mark });

        private static GameLoop Hero(HeroClass heroClass)
        {
            var loop = TestWorld.Loop();
            loop.State.Class = heroClass;
            return loop;
        }

        private static GameLoop Poisoned(HeroClass heroClass, double stacks)
        {
            var loop = Hero(heroClass);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.State.PoisonStacks = stacks;
            return loop;
        }

        [Test]
        public void DowserSparkAddsBurnEverySecondOnlyToABurningEnemy()
        {
            var loop = Hero(HeroClass.Dowser);
            loop.Hero.Add(new Game.Core.Stats.Modifier(CombatStats.BurnStacksFromAllyLightning, Game.Core.Stats.ModifierKind.Flat, 1));
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.Synergy.SetAlly(0, true, HeroClass.Shardcaller, Mark("lightning"));
            for (var tick = 0; tick < 20; tick++)
                loop.Tick();
            Assert.That(loop.SynergyStrikes.Sparks, Is.Zero, "nothing burns yet");

            loop.State.BurnStacks = 5;
            for (var tick = 0; tick < 30; tick++)
                loop.Tick();
            Assert.That(loop.SynergyStrikes.Sparks, Is.EqualTo(3));
            Assert.That(loop.State.BurnStacks, Is.EqualTo(8).Within(1e-9));
        }

        [Test]
        public void DreamcatcherCritsUnderTheHunterMarkGiveOmens()
        {
            var with = Hero(HeroClass.Dreamcatcher);
            var without = Hero(HeroClass.Dreamcatcher);
            with.Hero.Add(new Game.Core.Stats.Modifier(CombatStats.OmenPerPartyCrit, Game.Core.Stats.ModifierKind.Flat, 1));
            with.Synergy.SetAlly(0, true, HeroClass.Windshot, Mark("hunterMark"));
            with.Tick();
            without.Tick();

            with.State.Crits += 4;
            without.State.Crits += 4;
            with.Tick();
            without.Tick();

            Assert.That(with.SynergyStrikes.OmensFromCrits, Is.EqualTo(4).Within(1e-9));
            Assert.That(without.SynergyStrikes.OmensFromCrits, Is.Zero);
            Assert.That(with.State.Resource, Is.GreaterThan(without.State.Resource));
        }

        private static GameLoop Casting(HeroClass heroClass, int allyMarks)
        {
            var loop = Hero(heroClass);
            var gem = FirstLightningGem();
            loop.Skills.Equip(gem);
            loop.Tick();
            loop.State.EnemyHp = Game.Core.Math.BigNumber.FromDouble(1e30);
            loop.Synergy.SetAlly(0, true, HeroClass.Alchemist, allyMarks);
            loop.Tick();
            loop.State.Resource = 1000;
            loop.RequestCast(0);
            loop.Tick();
            Assert.That(loop.State.Skills[0].LastCastTick, Is.EqualTo(loop.State.Tick - 1), "the lightning skill was cast");
            return loop;
        }

        private static Game.Core.Content.Gems.GemDef FirstLightningGem()
        {
            foreach (var gem in TestWorld.Data.Gems.All)
            {
                if (gem.Skill != null && gem.Skill.Marks.Count > 0 && gem.Skill.Marks[0] == "lightning" && gem.Ascendancy == null)
                    return gem;
            }
            throw new System.InvalidOperationException("no lightning gem");
        }
    }
}
