using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StripTransitionTests
    {
        private static readonly PortalRule Plain = new PortalRule(0.5, 0.4, 0.5, 0.4, 0.4, false, 0, 0);
        private static readonly PortalRule RiftLike = new PortalRule(0.2, 0.2, 0.2, 0.2, 0.2, true, 1.0, 3.0);

        private static int Run(StripTransition transition, int limit = 1000)
        {
            var ticks = 0;
            while (transition.Active && ticks < limit)
            {
                transition.Tick();
                ticks++;
            }
            return ticks;
        }

        [Test]
        public void EveryDestinationHasItsOwnPortalInPanelJson()
        {
            var rules = TestWorld.Data.Panel.Scene;
            foreach (StripPlace place in System.Enum.GetValues(typeof(StripPlace)))
                Assert.That(rules.Portal(place).Open, Is.GreaterThan(0), place.ToString());
            Assert.That(rules.Portal(StripPlace.Rift).Loads, Is.True);
            Assert.That(rules.Portal(StripPlace.Rift).Group, Is.True);
            Assert.That(rules.Portal(StripPlace.Dungeon).Group, Is.True);
            Assert.That(rules.Portal(StripPlace.Town).Group, Is.False);
        }

        [Test]
        public void PhasesGoInOrderAndTheStripSwapsHalfwayThroughTheSwap()
        {
            var transition = new StripTransition();
            transition.Start(StripPlace.Home, StripPlace.Town, Plain);
            var seen = new List<TransitionPhase>();
            var swappedIn = TransitionPhase.None;
            while (transition.Active)
            {
                if (seen.Count == 0 || seen[seen.Count - 1] != transition.Phase)
                    seen.Add(transition.Phase);
                if (swappedIn == TransitionPhase.None && transition.Shown == StripPlace.Town)
                    swappedIn = transition.Phase;
                transition.Tick();
            }
            Assert.That(seen, Is.EqualTo(new[] { TransitionPhase.Open, TransitionPhase.Enter, TransitionPhase.Swap, TransitionPhase.Arrive, TransitionPhase.Exit }));
            Assert.That(swappedIn, Is.EqualTo(TransitionPhase.Swap));
            Assert.That(transition.Phase, Is.EqualTo(TransitionPhase.None));
            Assert.That(transition.Shown, Is.EqualTo(StripPlace.Town));
        }

        [Test]
        public void TheLengthFollowsTheRules()
        {
            var transition = new StripTransition();
            transition.Start(StripPlace.Home, StripPlace.Town, Plain);
            Assert.That(Run(transition), Is.EqualTo(22));
            transition.Start(StripPlace.Town, StripPlace.Home, PortalRule.None);
            Assert.That(transition.Active, Is.False, "no portal means an instant swap");
        }

        [Test]
        public void TheRiftHoldsOnLoadingUntilTheViewIsReadyButNotForever()
        {
            var transition = new StripTransition();
            transition.Start(StripPlace.Home, StripPlace.Rift, RiftLike);
            Assert.That(Run(transition), Is.EqualTo(2 + 2 + 30 + 2 + 2), "never ready: the cap ends the wait");

            transition.Start(StripPlace.Home, StripPlace.Rift, RiftLike);
            while (transition.Phase != TransitionPhase.Swap)
                transition.Tick();
            Assert.That(TransitionLook.ShowsLoading(transition), Is.True);
            transition.MarkLoaded();
            Assert.That(Run(transition) + 4, Is.EqualTo(2 + 2 + 10 + 2 + 2), "ready at once: the minimum load still shows");
        }

        [Test]
        public void TheHeroFadesIntoThePortalAndOutOfTheNextOne()
        {
            var transition = new StripTransition();
            transition.Start(StripPlace.Home, StripPlace.Hideout, Plain);
            Assert.That(TransitionLook.Portal(transition), Is.EqualTo(PortalPart.Open));
            Assert.That(TransitionLook.PortalPlace(transition), Is.EqualTo(StripPlace.Hideout));
            Assert.That(TransitionLook.HeroAlpha(transition), Is.EqualTo(1));
            while (transition.Phase != TransitionPhase.Swap)
                transition.Tick();
            Assert.That(TransitionLook.HeroAlpha(transition), Is.EqualTo(0));
            Assert.That(TransitionLook.VeilAlpha(transition), Is.EqualTo(1));
            while (transition.Phase != TransitionPhase.Exit)
                transition.Tick();
            Assert.That(TransitionLook.Portal(transition), Is.EqualTo(PortalPart.Close));
            Assert.That(TransitionLook.PortalPlace(transition), Is.EqualTo(StripPlace.Home), "the closing portal is the one we came out of");
            Assert.That(TransitionLook.HeroAlpha(transition), Is.EqualTo(1));
        }

        [Test]
        public void EnteringAPlaceStartsItsPortal()
        {
            var loop = TestWorld.Loop();
            loop.Tick();
            Assert.That(loop.Peaceful.Enter(PeacefulPlace.Town), Is.EqualTo(PeacefulResult.Done));
            loop.Tick();
            Assert.That(loop.Scene.Transition.Active, Is.True);
            Assert.That(loop.Scene.Transition.From, Is.EqualTo(StripPlace.Home));
            Assert.That(loop.Scene.Transition.To, Is.EqualTo(StripPlace.Town));
            for (var i = 0; i < 100; i++)
                loop.Tick();
            Assert.That(loop.Scene.Transition.Active, Is.False);
        }

        [Test]
        public void EveryPlaceHasItsOwnSoundForEachPhase()
        {
            var keys = new HashSet<string>();
            foreach (StripPlace place in System.Enum.GetValues(typeof(StripPlace)))
            {
                foreach (var phase in new[] { TransitionPhase.Open, TransitionPhase.Enter, TransitionPhase.Arrive, TransitionPhase.Exit })
                    Assert.That(keys.Add(TransitionSound.Key(place, phase)), Is.True, place + " " + phase);
            }
            Assert.That(TransitionSound.Key(StripPlace.Town, TransitionPhase.Open), Is.EqualTo("portal_town_open"));

            var transition = new StripTransition();
            transition.Start(StripPlace.Home, StripPlace.Rift, RiftLike);
            Assert.That(TransitionSound.Cue(transition), Is.EqualTo("portal_rift_open"));
            while (transition.Phase != TransitionPhase.Swap)
                transition.Tick();
            Assert.That(TransitionSound.Cue(transition), Is.EqualTo(TransitionSound.RiftLoading));
        }

        [Test]
        public void TheRiftLoadingShowsTheWindowModifiersAndWhoLiftsThem()
        {
            var loop = TestWorld.Loop();
            var window = loop.Rift.Rules.Schedule.Next(0);
            Assert.That(loop.Rift.Enter(window.StartUnixMs + 1), Is.EqualTo(Game.Core.Sim.Rift.RiftEntry.Entered));
            var lines = RiftLoadingText.Lines(loop);
            Assert.That(lines.Count, Is.EqualTo(loop.Rift.WindowModifiers.Count + 3), "modifiers, the window set, its mechanic and the softened modifier");
            foreach (var line in lines)
                Assert.That(line, Does.Not.StartWith("rift."), "every line has a text");
        }
    }
}
