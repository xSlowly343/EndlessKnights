using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public static class TraitStateJson
    {
        public static JsonValue Write(GameState state) =>
            Object(
                ("runedancer", Object(
                    ("runes", JsonValue.Of(state.Runedancer.Runes)),
                    ("runeIdleTicks", JsonValue.Of(state.Runedancer.RuneIdleTicks)),
                    ("doubleRunesLeft", JsonValue.Of(state.Runedancer.DoubleRunesLeft)),
                    ("wordOnEnemy", JsonValue.Of(state.Runedancer.WordOnEnemy)),
                    ("words", Long(state.Runedancer.Words)),
                    ("sealTicks", JsonValue.Of(state.Runedancer.SealTicks)),
                    ("rains", Long(state.Runedancer.Rains)),
                    ("whirlFull", JsonValue.Of(state.Runedancer.WhirlFull)),
                    ("bladeProgress", JsonValue.Of(state.Runedancer.BladeProgress)),
                    ("blades", JsonValue.Of(state.Runedancer.Blades)),
                    ("bladeFallTicks", JsonValue.Of(state.Runedancer.BladeFallTicks)),
                    ("whirlBlades", JsonValue.Of(state.Runedancer.WhirlBlades)),
                    ("whirlLingerTicks", JsonValue.Of(state.Runedancer.WhirlLingerTicks)),
                    ("whirlSpinTicks", JsonValue.Of(state.Runedancer.WhirlSpinTicks)),
                    ("whirlCarry", JsonValue.Of(state.Runedancer.WhirlCarry)),
                    ("shadowCarry", JsonValue.Of(state.Runedancer.ShadowCarry)),
                    ("newEnemy", JsonValue.Of(state.Runedancer.NewEnemy)),
                    ("runesBefore", JsonValue.Of(state.Runedancer.RunesBefore)),
                    ("bladesBefore", JsonValue.Of(state.Runedancer.BladesBefore)),
                    ("hitsThisTick", JsonValue.Of(state.Runedancer.HitsThisTick)),
                    ("critsThisTick", JsonValue.Of(state.Runedancer.CritsThisTick)))),
                ("duelist", Object(
                    ("breachWaitTicks", JsonValue.Of(state.Duelist.BreachWaitTicks)),
                    ("breachOpenTicks", JsonValue.Of(state.Duelist.BreachOpenTicks)),
                    ("breachPressTicks", JsonValue.Of(state.Duelist.BreachPressTicks)),
                    ("lungesInBreach", JsonValue.Of(state.Duelist.LungesInBreach)),
                    ("lunges", Long(state.Duelist.Lunges)),
                    ("touchProgress", JsonValue.Of(state.Duelist.TouchProgress)),
                    ("marks", JsonValue.Of(state.Duelist.Marks)),
                    ("marksBefore", JsonValue.Of(state.Duelist.MarksBefore)),
                    ("carryMarks", JsonValue.Of(state.Duelist.CarryMarks)),
                    ("touches", Long(state.Duelist.Touches)),
                    ("phantomSwitchTicks", JsonValue.Of(state.Duelist.PhantomSwitchTicks)),
                    ("phantomLingerTicks", JsonValue.Of(state.Duelist.PhantomLingerTicks)),
                    ("phantomPauseCarry", JsonValue.Of(state.Duelist.PhantomPauseCarry)),
                    ("phantomFighting", JsonValue.Of(state.Duelist.PhantomFighting)),
                    ("newEnemy", JsonValue.Of(state.Duelist.NewEnemy)),
                    ("hitsThisTick", JsonValue.Of(state.Duelist.HitsThisTick)))),
                ("windshot", Object(
                    ("shotProgress", JsonValue.Of(state.Windshot.ShotProgress)),
                    ("shots", Long(state.Windshot.Shots)),
                    ("lastHitTick", Long(state.Windshot.LastHitTick)),
                    ("autoShotTicks", JsonValue.Of(state.Windshot.AutoShotTicks)),
                    ("stillTicks", JsonValue.Of(state.Windshot.StillTicks)),
                    ("forcedCalmTicks", JsonValue.Of(state.Windshot.ForcedCalmTicks)),
                    ("wasCalm", JsonValue.Of(state.Windshot.WasCalm)),
                    ("calmFirstPending", JsonValue.Of(state.Windshot.CalmFirstPending)),
                    ("stormHitsLeft", JsonValue.Of(state.Windshot.StormHitsLeft)),
                    ("gustPxCarry", JsonValue.Of(state.Windshot.GustPxCarry)),
                    ("gusts", JsonValue.Of(state.Windshot.Gusts)),
                    ("gustHitsLeft", JsonValue.Of(state.Windshot.GustHitsLeft)),
                    ("gustStormTicks", JsonValue.Of(state.Windshot.GustStormTicks)),
                    ("gustTimerTicks", JsonValue.Of(state.Windshot.GustTimerTicks)),
                    ("calm", JsonValue.Of(state.Windshot.Calm)),
                    ("gustsStarted", Long(state.Windshot.GustsStarted)))),
                ("shardcaller", Object(
                    ("crystals", JsonValue.Of(state.Shardcaller.Crystals)),
                    ("size", JsonValue.Of(state.Shardcaller.Size)),
                    ("strikeProgress", JsonValue.Of(state.Shardcaller.StrikeProgress)),
                    ("growTicks", JsonValue.Of(state.Shardcaller.GrowTicks)),
                    ("lostCrystals", JsonValue.Of(state.Shardcaller.LostCrystals)),
                    ("lostSize", JsonValue.Of(state.Shardcaller.LostSize)),
                    ("spawnPending", JsonValue.Of(state.Shardcaller.SpawnPending)),
                    ("detonations", Long(state.Shardcaller.Detonations)),
                    ("pulses", Long(state.Shardcaller.Pulses)),
                    ("blooms", Long(state.Shardcaller.Blooms)),
                    ("shellTicks", JsonValue.Of(state.Shardcaller.ShellTicks)),
                    ("crackTicks", JsonValue.Of(state.Shardcaller.CrackTicks)),
                    ("cracks", Long(state.Shardcaller.Cracks)),
                    ("shatter", JsonValue.Of(state.Shardcaller.Shatter)),
                    ("prisonTicks", JsonValue.Of(state.Shardcaller.PrisonTicks)),
                    ("heartTicks", JsonValue.Of(state.Shardcaller.HeartTicks)))),
                ("sandwalker", Object(
                    ("bottom", JsonValue.Of(state.Sandwalker.Bottom)),
                    ("grains", JsonValue.Of(state.Sandwalker.Grains)),
                    ("stepGrainPx", JsonValue.Of(state.Sandwalker.StepGrainPx)),
                    ("flips", Long(state.Sandwalker.Flips)),
                    ("roadPx", JsonValue.Of(state.Sandwalker.RoadPx)),
                    ("camps", Long(state.Sandwalker.Camps)),
                    ("hazePx", JsonValue.Of(state.Sandwalker.HazePx)),
                    ("mirageHits", JsonValue.Of(state.Sandwalker.MirageHits)),
                    ("mirageStrikeTicks", JsonValue.Of(state.Sandwalker.MirageStrikeTicks)),
                    ("miragesRaised", Long(state.Sandwalker.MiragesRaised)),
                    ("miragesFaded", Long(state.Sandwalker.MiragesFaded)),
                    ("ticksSinceStep", JsonValue.Of(state.Sandwalker.TicksSinceStep)),
                    ("stepSinceCastPx", JsonValue.Of(state.Sandwalker.StepSinceCastPx)),
                    ("partyTimeTicks", JsonValue.Of(state.Sandwalker.PartyTimeTicks)),
                    ("partyStopTicks", JsonValue.Of(state.Sandwalker.PartyStopTicks)),
                    ("spawnPending", JsonValue.Of(state.Sandwalker.SpawnPending)))),
                ("minstrel", Object(
                    ("lineHits", JsonValue.Of(state.Minstrel.LineHits)),
                    ("lines", JsonValue.Of(state.Minstrel.Lines)),
                    ("verses", JsonValue.Of(state.Minstrel.Verses)),
                    ("row", JsonValue.Of(state.Minstrel.Row)),
                    ("verseTicks", JsonValue.Of(state.Minstrel.VerseTicks)),
                    ("chorusTicks", JsonValue.Of(state.Minstrel.ChorusTicks)),
                    ("choruses", Long(state.Minstrel.Choruses)),
                    ("encoreUsed", JsonValue.Of(state.Minstrel.EncoreUsed)),
                    ("songHits", JsonValue.Of(state.Minstrel.SongHits)),
                    ("melody", JsonValue.Of(state.Minstrel.Melody)),
                    ("melodyChanges", Long(state.Minstrel.MelodyChanges)),
                    ("songAllTicks", JsonValue.Of(state.Minstrel.SongAllTicks)),
                    ("songAllWaitTicks", JsonValue.Of(state.Minstrel.SongAllWaitTicks)),
                    ("marchCarry", JsonValue.Of(state.Minstrel.MarchCarry)),
                    ("noteHits", JsonValue.Of(state.Minstrel.NoteHits)),
                    ("notes", JsonValue.Of(state.Minstrel.Notes)),
                    ("sleepTicks", JsonValue.Of(state.Minstrel.SleepTicks)),
                    ("sleeps", Long(state.Minstrel.Sleeps)),
                    ("dream", SaveValues.Of(state.Minstrel.Dream)),
                    ("hymnTicks", JsonValue.Of(state.Minstrel.HymnTicks)),
                    ("hymnPower", JsonValue.Of(state.Minstrel.HymnPower)),
                    ("partyVerse", JsonValue.Of(state.Minstrel.PartyVerse)),
                    ("partySong", JsonValue.Of(state.Minstrel.PartySong)),
                    ("partySleep", JsonValue.Of(state.Minstrel.PartySleep)),
                    ("chorus", JsonValue.Of(state.Minstrel.Chorus)),
                    ("soloTicks", JsonValue.Of(state.Minstrel.SoloTicks)),
                    ("spawnPending", JsonValue.Of(state.Minstrel.SpawnPending)))),
                ("dreamcatcher", Object(
                    ("nets", JsonValue.Of(state.Dreamcatcher.Nets)),
                    ("netPx", JsonValue.Of(state.Dreamcatcher.NetPx)),
                    ("caughtTicks", JsonValue.Of(state.Dreamcatcher.CaughtTicks)),
                    ("catches", Long(state.Dreamcatcher.Catches)),
                    ("trophies", Long(state.Dreamcatcher.Trophies)),
                    ("enemyIndex", JsonValue.Of(state.Dreamcatcher.EnemyIndex)),
                    ("enemyBiome", JsonValue.Of(state.Dreamcatcher.EnemyBiome)),
                    ("specimens", Long(state.Dreamcatcher.Specimens)),
                    ("prophecyTicks", JsonValue.Of(state.Dreamcatcher.ProphecyTicks)),
                    ("prophecies", Long(state.Dreamcatcher.Prophecies)),
                    ("seenPrestiges", JsonValue.Of(state.Dreamcatcher.SeenPrestiges)),
                    ("bait", JsonValue.Of(state.Dreamcatcher.Bait)),
                    ("spiritsPending", JsonValue.Of(state.Dreamcatcher.SpiritsPending)),
                    ("spiritAlive", JsonValue.Of(state.Dreamcatcher.SpiritAlive)),
                    ("spirits", Long(state.Dreamcatcher.Spirits)),
                    ("spiritKills", Long(state.Dreamcatcher.SpiritKills)),
                    ("hornTicks", JsonValue.Of(state.Dreamcatcher.HornTicks)),
                    ("dozeTicks", JsonValue.Of(state.Dreamcatcher.DozeTicks)),
                    ("lucidTicks", JsonValue.Of(state.Dreamcatcher.LucidTicks)),
                    ("spawnPending", JsonValue.Of(state.Dreamcatcher.SpawnPending)),
                    ("album", Ints(state.Dreamcatcher.Album)),
                    ("prophesied", Bools(state.Dreamcatcher.Prophesied)))),
                ("lanternbearer", Object(
                    ("light", JsonValue.Of(state.Lanternbearer.Light)),
                    ("shadowTicks", JsonValue.Of(state.Lanternbearer.ShadowTicks)),
                    ("litFull", JsonValue.Of(state.Lanternbearer.LitFull)),
                    ("litOilGiven", JsonValue.Of(state.Lanternbearer.LitOilGiven)),
                    ("flareTicks", JsonValue.Of(state.Lanternbearer.FlareTicks)),
                    ("fullLights", Long(state.Lanternbearer.FullLights)),
                    ("fireflyHits", JsonValue.Of(state.Lanternbearer.FireflyHits)),
                    ("firefliesSent", Long(state.Lanternbearer.FirefliesSent)),
                    ("beaconTicks", JsonValue.Of(state.Lanternbearer.BeaconTicks)),
                    ("flashes", Long(state.Lanternbearer.Flashes)),
                    ("fireflies", JsonValue.Of(state.Lanternbearer.Fireflies)),
                    ("strikeTicks", JsonValue.Of(state.Lanternbearer.StrikeTicks)),
                    ("glowTicks", JsonValue.Of(state.Lanternbearer.GlowTicks)),
                    ("glowPower", JsonValue.Of(state.Lanternbearer.GlowPower)),
                    ("spawnPending", JsonValue.Of(state.Lanternbearer.SpawnPending)))),
                ("whisperer", Object(
                    ("knots", JsonValue.Of(state.Whisperer.Knots)),
                    ("knotHits", JsonValue.Of(state.Whisperer.KnotHits)),
                    ("knotsTied", Long(state.Whisperer.KnotsTied)),
                    ("bossHits", JsonValue.Of(state.Whisperer.BossHits)),
                    ("named", JsonValue.Of(state.Whisperer.Named)),
                    ("rememberedBoss", state.Whisperer.RememberedBoss == null ? JsonValue.Null() : JsonValue.Of(state.Whisperer.RememberedBoss.Id)),
                    ("names", Long(state.Whisperer.Names)),
                    ("silentHits", JsonValue.Of(state.Whisperer.SilentHits)),
                    ("queueHits", JsonValue.Of(state.Whisperer.QueueHits)),
                    ("grow", JsonValue.Of(state.Whisperer.Grow)),
                    ("breakTicks", JsonValue.Of(state.Whisperer.BreakTicks)),
                    ("breakCooldownTicks", JsonValue.Of(state.Whisperer.BreakCooldownTicks)),
                    ("hushesSent", Long(state.Whisperer.HushesSent)),
                    ("hushHitsBack", Long(state.Whisperer.HushHitsBack)),
                    ("curseSlots", Numbers(state.Whisperer.CurseSlots)),
                    ("curseSlotTicks", Ints(state.Whisperer.CurseSlotTicks)),
                    ("curseSlotGems", Ints(state.Whisperer.CurseSlotGems)),
                    ("spawnPending", JsonValue.Of(state.Whisperer.SpawnPending)))),
                ("clockwright", Object(
                    ("holdTicks", JsonValue.Of(state.Clockwright.HoldTicks)),
                    ("burstTicks", JsonValue.Of(state.Clockwright.BurstTicks)),
                    ("bursts", Long(state.Clockwright.Bursts)),
                    ("loopTicks", JsonValue.Of(state.Clockwright.LoopTicks)),
                    ("loops", Long(state.Clockwright.Loops)),
                    ("gearHits", JsonValue.Of(state.Clockwright.GearHits)),
                    ("gearTicks", Long(state.Clockwright.GearTicks)),
                    ("meshes", Long(state.Clockwright.Meshes)),
                    ("idleTicks", JsonValue.Of(state.Clockwright.IdleTicks)),
                    ("ownCastTick", Long(state.Clockwright.OwnCastTick)),
                    ("previousOwnCastTick", Long(state.Clockwright.PreviousOwnCastTick)),
                    ("lastMeshTick", Long(state.Clockwright.LastMeshTick)),
                    ("partySlow", JsonValue.Of(state.Clockwright.PartySlow)))),
                ("alchemist", Object(
                    ("reagentHits", JsonValue.Of(state.Alchemist.ReagentHits)),
                    ("reagent", JsonValue.Of(state.Alchemist.Reagent)),
                    ("inFlask", JsonValue.Of(state.Alchemist.InFlask)),
                    ("mixes", Long(state.Alchemist.Mixes)),
                    ("lastRecipe", JsonValue.Of(state.Alchemist.LastRecipe)),
                    ("mixDamageTaken", JsonValue.Of(state.Alchemist.MixDamageTaken)),
                    ("mixTicks", JsonValue.Of(state.Alchemist.MixTicks)),
                    ("mixCrit", JsonValue.Of(state.Alchemist.MixCrit)),
                    ("mixCritTicks", JsonValue.Of(state.Alchemist.MixCritTicks)),
                    ("mixGold", JsonValue.Of(state.Alchemist.MixGold)),
                    ("mixGoldTicks", JsonValue.Of(state.Alchemist.MixGoldTicks)),
                    ("layersLeft", JsonValue.Of(state.Alchemist.LayersLeft)),
                    ("layerProgress", JsonValue.Of(state.Alchemist.LayerProgress)),
                    ("carryLayers", JsonValue.Of(state.Alchemist.CarryLayers)),
                    ("layersDissolved", Long(state.Alchemist.LayersDissolved)),
                    ("elixirHits", JsonValue.Of(state.Alchemist.ElixirHits)),
                    ("elixirColor", JsonValue.Of(state.Alchemist.ElixirColor)),
                    ("elixirsSent", Long(state.Alchemist.ElixirsSent)),
                    ("mixResendTicks", JsonValue.Of(state.Alchemist.MixResendTicks)),
                    ("solventResendTicks", JsonValue.Of(state.Alchemist.SolventResendTicks)),
                    ("spawnPending", JsonValue.Of(state.Alchemist.SpawnPending)),
                    ("flask", Ints(state.Alchemist.Flask)))),
                ("dowser", Object(
                    ("veinAt", JsonValue.Of(state.Dowser.VeinAt)),
                    ("secondVeinAt", JsonValue.Of(state.Dowser.SecondVeinAt)),
                    ("hits", JsonValue.Of(state.Dowser.Hits)),
                    ("veinsStruck", JsonValue.Of(state.Dowser.VeinsStruck)),
                    ("veins", Long(state.Dowser.Veins)),
                    ("critTicks", JsonValue.Of(state.Dowser.CritTicks)),
                    ("luck", JsonValue.Of(state.Dowser.Luck)),
                    ("finds", Long(state.Dowser.Finds)),
                    ("luckGold", JsonValue.Of(state.Dowser.LuckGold)),
                    ("hourTicks", JsonValue.Of(state.Dowser.HourTicks)),
                    ("nuggets", JsonValue.Of(state.Dowser.Nuggets)),
                    ("chests", Long(state.Dowser.Chests)),
                    ("spawnPending", JsonValue.Of(state.Dowser.SpawnPending)))),
                ("soulbinder", Object(
                    ("souls", JsonValue.Of(state.Soulbinder.Souls)),
                    ("soulsTaken", Long(state.Soulbinder.SoulsTaken)),
                    ("spiritCarry", JsonValue.Of(state.Soulbinder.SpiritCarry)),
                    ("releaseTicks", JsonValue.Of(state.Soulbinder.ReleaseTicks)),
                    ("breaths", Long(state.Soulbinder.Breaths)),
                    ("coldTicks", JsonValue.Of(state.Soulbinder.ColdTicks)),
                    ("coldHits", JsonValue.Of(state.Soulbinder.ColdHits)),
                    ("coldResendTicks", JsonValue.Of(state.Soulbinder.ColdResendTicks)),
                    ("lendHits", JsonValue.Of(state.Soulbinder.LendHits)),
                    ("lends", Long(state.Soulbinder.Lends)),
                    ("soulsBack", Long(state.Soulbinder.SoulsBack)),
                    ("strength", Numbers(state.Soulbinder.Strength)),
                    ("sourceHp", Bigs(state.Soulbinder.SourceHp)))),
                ("ghostcaptain", Object(
                    ("loaded", JsonValue.Of(state.Ghostcaptain.Loaded)),
                    ("loadHits", JsonValue.Of(state.Ghostcaptain.LoadHits)),
                    ("mixed", JsonValue.Of(state.Ghostcaptain.Mixed)),
                    ("volleys", Long(state.Ghostcaptain.Volleys)),
                    ("breachTicks", JsonValue.Of(state.Ghostcaptain.BreachTicks)),
                    ("breachResendTicks", JsonValue.Of(state.Ghostcaptain.BreachResendTicks)),
                    ("sailors", JsonValue.Of(state.Ghostcaptain.Sailors)),
                    ("bossSailors", JsonValue.Of(state.Ghostcaptain.BossSailors)),
                    ("dawnTicks", JsonValue.Of(state.Ghostcaptain.DawnTicks)),
                    ("crewCarry", JsonValue.Of(state.Ghostcaptain.CrewCarry)),
                    ("dawns", Long(state.Ghostcaptain.Dawns)),
                    ("mateTicks", JsonValue.Of(state.Ghostcaptain.MateTicks)),
                    ("matePower", JsonValue.Of(state.Ghostcaptain.MatePower)),
                    ("mateShots", Long(state.Ghostcaptain.MateShots)),
                    ("parrotCasts", JsonValue.Of(state.Ghostcaptain.ParrotCasts)),
                    ("repeats", Long(state.Ghostcaptain.Repeats)),
                    ("lastSkillPower", JsonValue.Of(state.Ghostcaptain.LastSkillPower)))),
                ("mothcaller", Object(
                    ("moths", JsonValue.Of(state.Mothcaller.Moths)),
                    ("swarmCarry", JsonValue.Of(state.Mothcaller.SwarmCarry)),
                    ("decayTicks", JsonValue.Of(state.Mothcaller.DecayTicks)),
                    ("pollenRelayTicks", JsonValue.Of(state.Mothcaller.PollenRelayTicks)),
                    ("mothTicks", JsonValue.Of(state.Mothcaller.MothTicks)),
                    ("awakeTicks", JsonValue.Of(state.Mothcaller.AwakeTicks)),
                    ("mothCarry", JsonValue.Of(state.Mothcaller.MothCarry)),
                    ("wakes", Long(state.Mothcaller.Wakes)),
                    ("bossSeen", JsonValue.Of(state.Mothcaller.BossSeen)))),
                ("cartomancer", Object(
                    ("drawHits", JsonValue.Of(state.Cartomancer.DrawHits)),
                    ("draws", Long(state.Cartomancer.Draws)),
                    ("arcana", Long(state.Cartomancer.Arcana)),
                    ("coinsApplied", JsonValue.Of(state.Cartomancer.CoinsApplied)),
                    ("arcanaSecondTicks", JsonValue.Of(state.Cartomancer.ArcanaSecondTicks)),
                    ("termTicks", JsonValue.Of(state.Cartomancer.TermTicks)),
                    ("goldAdded", JsonValue.Of(state.Cartomancer.GoldAdded)),
                    ("exactPaid", JsonValue.Of(state.Cartomancer.ExactPaid)),
                    ("penalty", JsonValue.Of(state.Cartomancer.Penalty)),
                    ("streak", JsonValue.Of(state.Cartomancer.Streak)),
                    ("wins", Long(state.Cartomancer.Wins)),
                    ("losses", Long(state.Cartomancer.Losses)),
                    ("bossSeen", JsonValue.Of(state.Cartomancer.BossSeen)),
                    ("layHits", JsonValue.Of(state.Cartomancer.LayHits)),
                    ("lays", Long(state.Cartomancer.Lays)),
                    ("revealHits", JsonValue.Of(state.Cartomancer.RevealHits)),
                    ("revealSuit", JsonValue.Of(state.Cartomancer.RevealSuit)),
                    ("revealTicks", JsonValue.Of(state.Cartomancer.RevealTicks)),
                    ("reveals", Long(state.Cartomancer.Reveals)),
                    ("cupsCarry", JsonValue.Of(state.Cartomancer.CupsCarry)),
                    ("effectResendTicks", JsonValue.Of(state.Cartomancer.EffectResendTicks)),
                    ("spawned", JsonValue.Of(state.Cartomancer.Spawned)),
                    ("hand", Ints(state.Cartomancer.Hand)),
                    ("arcanaTicks", Ints(state.Cartomancer.ArcanaTicks)),
                    ("laid", Ints(state.Cartomancer.Laid)))),
                ("herbwitch", Object(
                    ("vines", JsonValue.Of(state.Herbwitch.Vines)),
                    ("seedHits", JsonValue.Of(state.Herbwitch.SeedHits)),
                    ("seeds", Long(state.Herbwitch.Seeds)),
                    ("flowers", Long(state.Herbwitch.Flowers)),
                    ("vineCarry", JsonValue.Of(state.Herbwitch.VineCarry)),
                    ("gardenResendTicks", JsonValue.Of(state.Herbwitch.GardenResendTicks)),
                    ("venom", JsonValue.Of(state.Herbwitch.Venom)),
                    ("venomCarryOver", JsonValue.Of(state.Herbwitch.VenomCarryOver)),
                    ("venomTickCarry", JsonValue.Of(state.Herbwitch.VenomTickCarry)),
                    ("brew", JsonValue.Of(state.Herbwitch.Brew)),
                    ("drinkHits", JsonValue.Of(state.Herbwitch.DrinkHits)),
                    ("drinks", Long(state.Herbwitch.Drinks)),
                    ("partyPoisonTicks", JsonValue.Of(state.Herbwitch.PartyPoisonTicks)),
                    ("poisonCarry", JsonValue.Of(state.Herbwitch.PoisonCarry)),
                    ("spawned", JsonValue.Of(state.Herbwitch.Spawned)),
                    ("flowerCarry", JsonValue.Of(state.Herbwitch.FlowerCarry)),
                    ("vineStage", Ints(state.Herbwitch.VineStage)),
                    ("vineGrowTicks", Ints(state.Herbwitch.VineGrowTicks)))),
                ("golemancer", Object(
                    ("sealHits", JsonValue.Of(state.Golemancer.SealHits)),
                    ("seals", Long(state.Golemancer.Seals)),
                    ("golemTicks", JsonValue.Of(state.Golemancer.GolemTicks)),
                    ("secondGolemTicks", JsonValue.Of(state.Golemancer.SecondGolemTicks)),
                    ("golemPower", JsonValue.Of(state.Golemancer.GolemPower)),
                    ("golemCarry", JsonValue.Of(state.Golemancer.GolemCarry)),
                    ("runes", JsonValue.Of(state.Golemancer.Runes)),
                    ("runeHits", JsonValue.Of(state.Golemancer.RuneHits)),
                    ("eternalCarry", JsonValue.Of(state.Golemancer.EternalCarry)),
                    ("brand", JsonValue.Of(state.Golemancer.Brand)),
                    ("brandHits", JsonValue.Of(state.Golemancer.BrandHits)),
                    ("brandPops", Long(state.Golemancer.BrandPops)),
                    ("brandResendTicks", JsonValue.Of(state.Golemancer.BrandResendTicks)),
                    ("minorHits", JsonValue.Of(state.Golemancer.MinorHits)),
                    ("minors", JsonValue.Of(state.Golemancer.Minors)),
                    ("minorCarry", JsonValue.Of(state.Golemancer.MinorCarry)),
                    ("spawned", JsonValue.Of(state.Golemancer.Spawned)),
                    ("minorTicks", Ints(state.Golemancer.MinorTicks)))),
                ("boons", Object(
                    ("fireflies", JsonValue.Of(state.Boons.Fireflies)),
                    ("fireflyTicks", JsonValue.Of(state.Boons.FireflyTicks)),
                    ("sparkPower", JsonValue.Of(state.Boons.SparkPower)),
                    ("sparkEvery", JsonValue.Of(state.Boons.SparkEvery)),
                    ("burstPower", JsonValue.Of(state.Boons.BurstPower)),
                    ("strikesSinceSpark", JsonValue.Of(state.Boons.StrikesSinceSpark)),
                    ("sparks", Long(state.Boons.Sparks)),
                    ("beaconCharges", JsonValue.Of(state.Boons.BeaconCharges)),
                    ("beaconPower", JsonValue.Of(state.Boons.BeaconPower)),
                    ("hushes", JsonValue.Of(state.Boons.Hushes)),
                    ("hushPower", JsonValue.Of(state.Boons.HushPower)),
                    ("hushHits", Long(state.Boons.HushHits)),
                    ("acidTicks", JsonValue.Of(state.Boons.AcidTicks)),
                    ("redTicks", JsonValue.Of(state.Boons.RedTicks)),
                    ("blueTicks", JsonValue.Of(state.Boons.BlueTicks)),
                    ("goldTicks", JsonValue.Of(state.Boons.GoldTicks)),
                    ("elixirPower", JsonValue.Of(state.Boons.ElixirPower)),
                    ("elixirGold", JsonValue.Of(state.Boons.ElixirGold)),
                    ("redEvery", JsonValue.Of(state.Boons.RedEvery)),
                    ("redStrikes", JsonValue.Of(state.Boons.RedStrikes)),
                    ("elixirStreak", JsonValue.Of(state.Boons.ElixirStreak)),
                    ("elixirs", Long(state.Boons.Elixirs)),
                    ("lendTicks", JsonValue.Of(state.Boons.LendTicks)),
                    ("lendPower", JsonValue.Of(state.Boons.LendPower)),
                    ("lentHits", Long(state.Boons.LentHits)),
                    ("pollen", JsonValue.Of(state.Boons.Pollen)),
                    ("pollenRate", JsonValue.Of(state.Boons.PollenRate)),
                    ("pollenTicks", JsonValue.Of(state.Boons.PollenTicks)),
                    ("pollenBurnSeconds", JsonValue.Of(state.Boons.PollenBurnSeconds)),
                    ("pollenEvery", JsonValue.Of(state.Boons.PollenEvery)),
                    ("pollenQueue", JsonValue.Of(state.Boons.PollenQueue)),
                    ("pollenLight", JsonValue.Of(state.Boons.PollenLight)),
                    ("pollenCrits", JsonValue.Of(state.Boons.PollenCrits)),
                    ("pollenBurn", JsonValue.Of(state.Boons.PollenBurn)),
                    ("pollenBurnTicks", JsonValue.Of(state.Boons.PollenBurnTicks)),
                    ("pollenBurnCarry", JsonValue.Of(state.Boons.PollenBurnCarry)),
                    ("pollenShakes", Long(state.Boons.PollenShakes)),
                    ("petalEvery", JsonValue.Of(state.Boons.PetalEvery)),
                    ("petalPower", JsonValue.Of(state.Boons.PetalPower)),
                    ("petalTicks", JsonValue.Of(state.Boons.PetalTicks)),
                    ("petalMax", JsonValue.Of(state.Boons.PetalMax)),
                    ("petalHits", JsonValue.Of(state.Boons.PetalHits)),
                    ("petalSeeds", JsonValue.Of(state.Boons.PetalSeeds)),
                    ("petalCarry", JsonValue.Of(state.Boons.PetalCarry)),
                    ("petalStings", Long(state.Boons.PetalStings)))));

        public static void Read(ContentNode traits, GameState state, EnemyCatalog enemies)
        {
            if (traits.HasField("runedancer"))
                ReadRunedancer(traits.Field("runedancer"), state, enemies);
            if (traits.HasField("duelist"))
                ReadDuelist(traits.Field("duelist"), state, enemies);
            if (traits.HasField("windshot"))
                ReadWindshot(traits.Field("windshot"), state, enemies);
            if (traits.HasField("shardcaller"))
                ReadShardcaller(traits.Field("shardcaller"), state, enemies);
            if (traits.HasField("sandwalker"))
                ReadSandwalker(traits.Field("sandwalker"), state, enemies);
            if (traits.HasField("minstrel"))
                ReadMinstrel(traits.Field("minstrel"), state, enemies);
            if (traits.HasField("dreamcatcher"))
                ReadDreamcatcher(traits.Field("dreamcatcher"), state, enemies);
            if (traits.HasField("lanternbearer"))
                ReadLanternbearer(traits.Field("lanternbearer"), state, enemies);
            if (traits.HasField("whisperer"))
                ReadWhisperer(traits.Field("whisperer"), state, enemies);
            if (traits.HasField("clockwright"))
                ReadClockwright(traits.Field("clockwright"), state, enemies);
            if (traits.HasField("alchemist"))
                ReadAlchemist(traits.Field("alchemist"), state, enemies);
            if (traits.HasField("dowser"))
                ReadDowser(traits.Field("dowser"), state, enemies);
            if (traits.HasField("soulbinder"))
                ReadSoulbinder(traits.Field("soulbinder"), state, enemies);
            if (traits.HasField("ghostcaptain"))
                ReadGhostcaptain(traits.Field("ghostcaptain"), state, enemies);
            if (traits.HasField("mothcaller"))
                ReadMothcaller(traits.Field("mothcaller"), state, enemies);
            if (traits.HasField("cartomancer"))
                ReadCartomancer(traits.Field("cartomancer"), state, enemies);
            if (traits.HasField("herbwitch"))
                ReadHerbwitch(traits.Field("herbwitch"), state, enemies);
            if (traits.HasField("golemancer"))
                ReadGolemancer(traits.Field("golemancer"), state, enemies);
            if (traits.HasField("boons"))
                ReadBoons(traits.Field("boons"), state, enemies);
        }

        private static void ReadRunedancer(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Runedancer.Runes = Number(node, "runes", state.Runedancer.Runes);
            state.Runedancer.RuneIdleTicks = Int(node, "runeIdleTicks", state.Runedancer.RuneIdleTicks);
            state.Runedancer.DoubleRunesLeft = Int(node, "doubleRunesLeft", state.Runedancer.DoubleRunesLeft);
            state.Runedancer.WordOnEnemy = Bool(node, "wordOnEnemy", state.Runedancer.WordOnEnemy);
            state.Runedancer.Words = Long(node, "words", state.Runedancer.Words);
            state.Runedancer.SealTicks = Int(node, "sealTicks", state.Runedancer.SealTicks);
            state.Runedancer.Rains = Long(node, "rains", state.Runedancer.Rains);
            state.Runedancer.WhirlFull = Bool(node, "whirlFull", state.Runedancer.WhirlFull);
            state.Runedancer.BladeProgress = Number(node, "bladeProgress", state.Runedancer.BladeProgress);
            state.Runedancer.Blades = Int(node, "blades", state.Runedancer.Blades);
            state.Runedancer.BladeFallTicks = Int(node, "bladeFallTicks", state.Runedancer.BladeFallTicks);
            state.Runedancer.WhirlBlades = Int(node, "whirlBlades", state.Runedancer.WhirlBlades);
            state.Runedancer.WhirlLingerTicks = Int(node, "whirlLingerTicks", state.Runedancer.WhirlLingerTicks);
            state.Runedancer.WhirlSpinTicks = Int(node, "whirlSpinTicks", state.Runedancer.WhirlSpinTicks);
            state.Runedancer.WhirlCarry = Number(node, "whirlCarry", state.Runedancer.WhirlCarry);
            state.Runedancer.ShadowCarry = Number(node, "shadowCarry", state.Runedancer.ShadowCarry);
            state.Runedancer.NewEnemy = Bool(node, "newEnemy", state.Runedancer.NewEnemy);
            state.Runedancer.RunesBefore = Number(node, "runesBefore", state.Runedancer.RunesBefore);
            state.Runedancer.BladesBefore = Int(node, "bladesBefore", state.Runedancer.BladesBefore);
            state.Runedancer.HitsThisTick = Number(node, "hitsThisTick", state.Runedancer.HitsThisTick);
            state.Runedancer.CritsThisTick = Int(node, "critsThisTick", state.Runedancer.CritsThisTick);
        }

        private static void ReadDuelist(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Duelist.BreachWaitTicks = Int(node, "breachWaitTicks", state.Duelist.BreachWaitTicks);
            state.Duelist.BreachOpenTicks = Int(node, "breachOpenTicks", state.Duelist.BreachOpenTicks);
            state.Duelist.BreachPressTicks = Int(node, "breachPressTicks", state.Duelist.BreachPressTicks);
            state.Duelist.LungesInBreach = Int(node, "lungesInBreach", state.Duelist.LungesInBreach);
            state.Duelist.Lunges = Long(node, "lunges", state.Duelist.Lunges);
            state.Duelist.TouchProgress = Int(node, "touchProgress", state.Duelist.TouchProgress);
            state.Duelist.Marks = Int(node, "marks", state.Duelist.Marks);
            state.Duelist.MarksBefore = Int(node, "marksBefore", state.Duelist.MarksBefore);
            state.Duelist.CarryMarks = Int(node, "carryMarks", state.Duelist.CarryMarks);
            state.Duelist.Touches = Long(node, "touches", state.Duelist.Touches);
            state.Duelist.PhantomSwitchTicks = Int(node, "phantomSwitchTicks", state.Duelist.PhantomSwitchTicks);
            state.Duelist.PhantomLingerTicks = Int(node, "phantomLingerTicks", state.Duelist.PhantomLingerTicks);
            state.Duelist.PhantomPauseCarry = Number(node, "phantomPauseCarry", state.Duelist.PhantomPauseCarry);
            state.Duelist.PhantomFighting = Bool(node, "phantomFighting", state.Duelist.PhantomFighting);
            state.Duelist.NewEnemy = Bool(node, "newEnemy", state.Duelist.NewEnemy);
            state.Duelist.HitsThisTick = Number(node, "hitsThisTick", state.Duelist.HitsThisTick);
        }

        private static void ReadWindshot(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Windshot.ShotProgress = Int(node, "shotProgress", state.Windshot.ShotProgress);
            state.Windshot.Shots = Long(node, "shots", state.Windshot.Shots);
            state.Windshot.LastHitTick = Long(node, "lastHitTick", state.Windshot.LastHitTick);
            state.Windshot.AutoShotTicks = Int(node, "autoShotTicks", state.Windshot.AutoShotTicks);
            state.Windshot.StillTicks = Int(node, "stillTicks", state.Windshot.StillTicks);
            state.Windshot.ForcedCalmTicks = Int(node, "forcedCalmTicks", state.Windshot.ForcedCalmTicks);
            state.Windshot.WasCalm = Bool(node, "wasCalm", state.Windshot.WasCalm);
            state.Windshot.CalmFirstPending = Bool(node, "calmFirstPending", state.Windshot.CalmFirstPending);
            state.Windshot.StormHitsLeft = Int(node, "stormHitsLeft", state.Windshot.StormHitsLeft);
            state.Windshot.GustPxCarry = Number(node, "gustPxCarry", state.Windshot.GustPxCarry);
            state.Windshot.Gusts = Int(node, "gusts", state.Windshot.Gusts);
            state.Windshot.GustHitsLeft = Int(node, "gustHitsLeft", state.Windshot.GustHitsLeft);
            state.Windshot.GustStormTicks = Int(node, "gustStormTicks", state.Windshot.GustStormTicks);
            state.Windshot.GustTimerTicks = Int(node, "gustTimerTicks", state.Windshot.GustTimerTicks);
            state.Windshot.Calm = Bool(node, "calm", state.Windshot.Calm);
            state.Windshot.GustsStarted = Long(node, "gustsStarted", state.Windshot.GustsStarted);
        }

        private static void ReadShardcaller(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Shardcaller.Crystals = Int(node, "crystals", state.Shardcaller.Crystals);
            state.Shardcaller.Size = Int(node, "size", state.Shardcaller.Size);
            state.Shardcaller.StrikeProgress = Int(node, "strikeProgress", state.Shardcaller.StrikeProgress);
            state.Shardcaller.GrowTicks = Int(node, "growTicks", state.Shardcaller.GrowTicks);
            state.Shardcaller.LostCrystals = Int(node, "lostCrystals", state.Shardcaller.LostCrystals);
            state.Shardcaller.LostSize = Int(node, "lostSize", state.Shardcaller.LostSize);
            state.Shardcaller.SpawnPending = Bool(node, "spawnPending", state.Shardcaller.SpawnPending);
            state.Shardcaller.Detonations = Long(node, "detonations", state.Shardcaller.Detonations);
            state.Shardcaller.Pulses = Long(node, "pulses", state.Shardcaller.Pulses);
            state.Shardcaller.Blooms = Long(node, "blooms", state.Shardcaller.Blooms);
            state.Shardcaller.ShellTicks = Int(node, "shellTicks", state.Shardcaller.ShellTicks);
            state.Shardcaller.CrackTicks = Int(node, "crackTicks", state.Shardcaller.CrackTicks);
            state.Shardcaller.Cracks = Long(node, "cracks", state.Shardcaller.Cracks);
            state.Shardcaller.Shatter = Bool(node, "shatter", state.Shardcaller.Shatter);
            state.Shardcaller.PrisonTicks = Int(node, "prisonTicks", state.Shardcaller.PrisonTicks);
            state.Shardcaller.HeartTicks = Int(node, "heartTicks", state.Shardcaller.HeartTicks);
        }

        private static void ReadSandwalker(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Sandwalker.Bottom = Bool(node, "bottom", state.Sandwalker.Bottom);
            state.Sandwalker.Grains = Int(node, "grains", state.Sandwalker.Grains);
            state.Sandwalker.StepGrainPx = Number(node, "stepGrainPx", state.Sandwalker.StepGrainPx);
            state.Sandwalker.Flips = Long(node, "flips", state.Sandwalker.Flips);
            state.Sandwalker.RoadPx = Number(node, "roadPx", state.Sandwalker.RoadPx);
            state.Sandwalker.Camps = Long(node, "camps", state.Sandwalker.Camps);
            state.Sandwalker.HazePx = Number(node, "hazePx", state.Sandwalker.HazePx);
            state.Sandwalker.MirageHits = Int(node, "mirageHits", state.Sandwalker.MirageHits);
            state.Sandwalker.MirageStrikeTicks = Int(node, "mirageStrikeTicks", state.Sandwalker.MirageStrikeTicks);
            state.Sandwalker.MiragesRaised = Long(node, "miragesRaised", state.Sandwalker.MiragesRaised);
            state.Sandwalker.MiragesFaded = Long(node, "miragesFaded", state.Sandwalker.MiragesFaded);
            state.Sandwalker.TicksSinceStep = Int(node, "ticksSinceStep", state.Sandwalker.TicksSinceStep);
            state.Sandwalker.StepSinceCastPx = Number(node, "stepSinceCastPx", state.Sandwalker.StepSinceCastPx);
            state.Sandwalker.PartyTimeTicks = Int(node, "partyTimeTicks", state.Sandwalker.PartyTimeTicks);
            state.Sandwalker.PartyStopTicks = Int(node, "partyStopTicks", state.Sandwalker.PartyStopTicks);
            state.Sandwalker.SpawnPending = Bool(node, "spawnPending", state.Sandwalker.SpawnPending);
        }

        private static void ReadMinstrel(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Minstrel.LineHits = Int(node, "lineHits", state.Minstrel.LineHits);
            state.Minstrel.Lines = Int(node, "lines", state.Minstrel.Lines);
            state.Minstrel.Verses = Int(node, "verses", state.Minstrel.Verses);
            state.Minstrel.Row = Int(node, "row", state.Minstrel.Row);
            state.Minstrel.VerseTicks = Int(node, "verseTicks", state.Minstrel.VerseTicks);
            state.Minstrel.ChorusTicks = Int(node, "chorusTicks", state.Minstrel.ChorusTicks);
            state.Minstrel.Choruses = Long(node, "choruses", state.Minstrel.Choruses);
            state.Minstrel.EncoreUsed = Bool(node, "encoreUsed", state.Minstrel.EncoreUsed);
            state.Minstrel.SongHits = Int(node, "songHits", state.Minstrel.SongHits);
            state.Minstrel.Melody = Int(node, "melody", state.Minstrel.Melody);
            state.Minstrel.MelodyChanges = Long(node, "melodyChanges", state.Minstrel.MelodyChanges);
            state.Minstrel.SongAllTicks = Int(node, "songAllTicks", state.Minstrel.SongAllTicks);
            state.Minstrel.SongAllWaitTicks = Int(node, "songAllWaitTicks", state.Minstrel.SongAllWaitTicks);
            state.Minstrel.MarchCarry = Number(node, "marchCarry", state.Minstrel.MarchCarry);
            state.Minstrel.NoteHits = Int(node, "noteHits", state.Minstrel.NoteHits);
            state.Minstrel.Notes = Int(node, "notes", state.Minstrel.Notes);
            state.Minstrel.SleepTicks = Int(node, "sleepTicks", state.Minstrel.SleepTicks);
            state.Minstrel.Sleeps = Long(node, "sleeps", state.Minstrel.Sleeps);
            state.Minstrel.Dream = Big(node, "dream", state.Minstrel.Dream);
            state.Minstrel.HymnTicks = Int(node, "hymnTicks", state.Minstrel.HymnTicks);
            state.Minstrel.HymnPower = Number(node, "hymnPower", state.Minstrel.HymnPower);
            state.Minstrel.PartyVerse = Number(node, "partyVerse", state.Minstrel.PartyVerse);
            state.Minstrel.PartySong = Number(node, "partySong", state.Minstrel.PartySong);
            state.Minstrel.PartySleep = Number(node, "partySleep", state.Minstrel.PartySleep);
            state.Minstrel.Chorus = Bool(node, "chorus", state.Minstrel.Chorus);
            state.Minstrel.SoloTicks = Int(node, "soloTicks", state.Minstrel.SoloTicks);
            state.Minstrel.SpawnPending = Bool(node, "spawnPending", state.Minstrel.SpawnPending);
        }

        private static void ReadDreamcatcher(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Dreamcatcher.Nets = Int(node, "nets", state.Dreamcatcher.Nets);
            state.Dreamcatcher.NetPx = Number(node, "netPx", state.Dreamcatcher.NetPx);
            state.Dreamcatcher.CaughtTicks = Int(node, "caughtTicks", state.Dreamcatcher.CaughtTicks);
            state.Dreamcatcher.Catches = Long(node, "catches", state.Dreamcatcher.Catches);
            state.Dreamcatcher.Trophies = Long(node, "trophies", state.Dreamcatcher.Trophies);
            state.Dreamcatcher.EnemyIndex = Int(node, "enemyIndex", state.Dreamcatcher.EnemyIndex);
            state.Dreamcatcher.EnemyBiome = Int(node, "enemyBiome", state.Dreamcatcher.EnemyBiome);
            state.Dreamcatcher.Specimens = Long(node, "specimens", state.Dreamcatcher.Specimens);
            state.Dreamcatcher.ProphecyTicks = Int(node, "prophecyTicks", state.Dreamcatcher.ProphecyTicks);
            state.Dreamcatcher.Prophecies = Long(node, "prophecies", state.Dreamcatcher.Prophecies);
            state.Dreamcatcher.SeenPrestiges = Int(node, "seenPrestiges", state.Dreamcatcher.SeenPrestiges);
            state.Dreamcatcher.Bait = Int(node, "bait", state.Dreamcatcher.Bait);
            state.Dreamcatcher.SpiritsPending = Int(node, "spiritsPending", state.Dreamcatcher.SpiritsPending);
            state.Dreamcatcher.SpiritAlive = Bool(node, "spiritAlive", state.Dreamcatcher.SpiritAlive);
            state.Dreamcatcher.Spirits = Long(node, "spirits", state.Dreamcatcher.Spirits);
            state.Dreamcatcher.SpiritKills = Long(node, "spiritKills", state.Dreamcatcher.SpiritKills);
            state.Dreamcatcher.HornTicks = Int(node, "hornTicks", state.Dreamcatcher.HornTicks);
            state.Dreamcatcher.DozeTicks = Int(node, "dozeTicks", state.Dreamcatcher.DozeTicks);
            state.Dreamcatcher.LucidTicks = Int(node, "lucidTicks", state.Dreamcatcher.LucidTicks);
            state.Dreamcatcher.SpawnPending = Bool(node, "spawnPending", state.Dreamcatcher.SpawnPending);
            ReadInts(node, "album", state.Dreamcatcher.Album);
            ReadBools(node, "prophesied", state.Dreamcatcher.Prophesied);
        }

        private static void ReadLanternbearer(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Lanternbearer.Light = Number(node, "light", state.Lanternbearer.Light);
            state.Lanternbearer.ShadowTicks = Int(node, "shadowTicks", state.Lanternbearer.ShadowTicks);
            state.Lanternbearer.LitFull = Bool(node, "litFull", state.Lanternbearer.LitFull);
            state.Lanternbearer.LitOilGiven = Bool(node, "litOilGiven", state.Lanternbearer.LitOilGiven);
            state.Lanternbearer.FlareTicks = Int(node, "flareTicks", state.Lanternbearer.FlareTicks);
            state.Lanternbearer.FullLights = Long(node, "fullLights", state.Lanternbearer.FullLights);
            state.Lanternbearer.FireflyHits = Int(node, "fireflyHits", state.Lanternbearer.FireflyHits);
            state.Lanternbearer.FirefliesSent = Long(node, "firefliesSent", state.Lanternbearer.FirefliesSent);
            state.Lanternbearer.BeaconTicks = Int(node, "beaconTicks", state.Lanternbearer.BeaconTicks);
            state.Lanternbearer.Flashes = Long(node, "flashes", state.Lanternbearer.Flashes);
            state.Lanternbearer.Fireflies = Int(node, "fireflies", state.Lanternbearer.Fireflies);
            state.Lanternbearer.StrikeTicks = Int(node, "strikeTicks", state.Lanternbearer.StrikeTicks);
            state.Lanternbearer.GlowTicks = Int(node, "glowTicks", state.Lanternbearer.GlowTicks);
            state.Lanternbearer.GlowPower = Number(node, "glowPower", state.Lanternbearer.GlowPower);
            state.Lanternbearer.SpawnPending = Bool(node, "spawnPending", state.Lanternbearer.SpawnPending);
        }

        private static void ReadWhisperer(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Whisperer.Knots = Int(node, "knots", state.Whisperer.Knots);
            state.Whisperer.KnotHits = Number(node, "knotHits", state.Whisperer.KnotHits);
            state.Whisperer.KnotsTied = Long(node, "knotsTied", state.Whisperer.KnotsTied);
            state.Whisperer.BossHits = Int(node, "bossHits", state.Whisperer.BossHits);
            state.Whisperer.Named = Bool(node, "named", state.Whisperer.Named);
            state.Whisperer.RememberedBoss = Enemy(node, "rememberedBoss", enemies);
            state.Whisperer.Names = Long(node, "names", state.Whisperer.Names);
            state.Whisperer.SilentHits = Int(node, "silentHits", state.Whisperer.SilentHits);
            state.Whisperer.QueueHits = Int(node, "queueHits", state.Whisperer.QueueHits);
            state.Whisperer.Grow = Number(node, "grow", state.Whisperer.Grow);
            state.Whisperer.BreakTicks = Int(node, "breakTicks", state.Whisperer.BreakTicks);
            state.Whisperer.BreakCooldownTicks = Int(node, "breakCooldownTicks", state.Whisperer.BreakCooldownTicks);
            state.Whisperer.HushesSent = Long(node, "hushesSent", state.Whisperer.HushesSent);
            state.Whisperer.HushHitsBack = Long(node, "hushHitsBack", state.Whisperer.HushHitsBack);
            ReadNumbers(node, "curseSlots", state.Whisperer.CurseSlots);
            ReadInts(node, "curseSlotTicks", state.Whisperer.CurseSlotTicks);
            ReadInts(node, "curseSlotGems", state.Whisperer.CurseSlotGems);
            if (node.HasField("curse"))
            {
                state.Whisperer.CurseSlots[0] = Number(node, "curse", 0);
                state.Whisperer.CurseSlotTicks[0] = Int(node, "curseTicks", 0);
            }
            state.Whisperer.SpawnPending = Bool(node, "spawnPending", state.Whisperer.SpawnPending);
        }

        private static void ReadClockwright(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Clockwright.HoldTicks = Int(node, "holdTicks", state.Clockwright.HoldTicks);
            state.Clockwright.BurstTicks = Int(node, "burstTicks", state.Clockwright.BurstTicks);
            state.Clockwright.Bursts = Long(node, "bursts", state.Clockwright.Bursts);
            state.Clockwright.LoopTicks = Int(node, "loopTicks", state.Clockwright.LoopTicks);
            state.Clockwright.Loops = Long(node, "loops", state.Clockwright.Loops);
            state.Clockwright.GearHits = Int(node, "gearHits", state.Clockwright.GearHits);
            state.Clockwright.GearTicks = Long(node, "gearTicks", state.Clockwright.GearTicks);
            state.Clockwright.Meshes = Long(node, "meshes", state.Clockwright.Meshes);
            state.Clockwright.IdleTicks = Int(node, "idleTicks", state.Clockwright.IdleTicks);
            state.Clockwright.OwnCastTick = Long(node, "ownCastTick", state.Clockwright.OwnCastTick);
            state.Clockwright.PreviousOwnCastTick = Long(node, "previousOwnCastTick", state.Clockwright.PreviousOwnCastTick);
            state.Clockwright.LastMeshTick = Long(node, "lastMeshTick", state.Clockwright.LastMeshTick);
            state.Clockwright.PartySlow = Number(node, "partySlow", state.Clockwright.PartySlow);
        }

        private static void ReadAlchemist(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Alchemist.ReagentHits = Int(node, "reagentHits", state.Alchemist.ReagentHits);
            state.Alchemist.Reagent = Int(node, "reagent", state.Alchemist.Reagent);
            state.Alchemist.InFlask = Int(node, "inFlask", state.Alchemist.InFlask);
            state.Alchemist.Mixes = Long(node, "mixes", state.Alchemist.Mixes);
            state.Alchemist.LastRecipe = Int(node, "lastRecipe", state.Alchemist.LastRecipe);
            state.Alchemist.MixDamageTaken = Number(node, "mixDamageTaken", state.Alchemist.MixDamageTaken);
            state.Alchemist.MixTicks = Int(node, "mixTicks", state.Alchemist.MixTicks);
            state.Alchemist.MixCrit = Number(node, "mixCrit", state.Alchemist.MixCrit);
            state.Alchemist.MixCritTicks = Int(node, "mixCritTicks", state.Alchemist.MixCritTicks);
            state.Alchemist.MixGold = Number(node, "mixGold", state.Alchemist.MixGold);
            state.Alchemist.MixGoldTicks = Int(node, "mixGoldTicks", state.Alchemist.MixGoldTicks);
            state.Alchemist.LayersLeft = Int(node, "layersLeft", state.Alchemist.LayersLeft);
            state.Alchemist.LayerProgress = Number(node, "layerProgress", state.Alchemist.LayerProgress);
            state.Alchemist.CarryLayers = Int(node, "carryLayers", state.Alchemist.CarryLayers);
            state.Alchemist.LayersDissolved = Long(node, "layersDissolved", state.Alchemist.LayersDissolved);
            state.Alchemist.ElixirHits = Int(node, "elixirHits", state.Alchemist.ElixirHits);
            state.Alchemist.ElixirColor = Int(node, "elixirColor", state.Alchemist.ElixirColor);
            state.Alchemist.ElixirsSent = Long(node, "elixirsSent", state.Alchemist.ElixirsSent);
            state.Alchemist.MixResendTicks = Int(node, "mixResendTicks", state.Alchemist.MixResendTicks);
            state.Alchemist.SolventResendTicks = Int(node, "solventResendTicks", state.Alchemist.SolventResendTicks);
            state.Alchemist.SpawnPending = Bool(node, "spawnPending", state.Alchemist.SpawnPending);
            ReadInts(node, "flask", state.Alchemist.Flask);
        }

        private static void ReadDowser(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Dowser.VeinAt = Int(node, "veinAt", state.Dowser.VeinAt);
            state.Dowser.SecondVeinAt = Int(node, "secondVeinAt", state.Dowser.SecondVeinAt);
            state.Dowser.Hits = Number(node, "hits", state.Dowser.Hits);
            state.Dowser.VeinsStruck = Int(node, "veinsStruck", state.Dowser.VeinsStruck);
            state.Dowser.Veins = Long(node, "veins", state.Dowser.Veins);
            state.Dowser.CritTicks = Int(node, "critTicks", state.Dowser.CritTicks);
            state.Dowser.Luck = Number(node, "luck", state.Dowser.Luck);
            state.Dowser.Finds = Long(node, "finds", state.Dowser.Finds);
            state.Dowser.LuckGold = Number(node, "luckGold", state.Dowser.LuckGold);
            state.Dowser.HourTicks = Int(node, "hourTicks", state.Dowser.HourTicks);
            state.Dowser.Nuggets = Int(node, "nuggets", state.Dowser.Nuggets);
            state.Dowser.Chests = Long(node, "chests", state.Dowser.Chests);
            state.Dowser.SpawnPending = Bool(node, "spawnPending", state.Dowser.SpawnPending);
        }

        private static void ReadSoulbinder(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Soulbinder.Souls = Int(node, "souls", state.Soulbinder.Souls);
            state.Soulbinder.SoulsTaken = Long(node, "soulsTaken", state.Soulbinder.SoulsTaken);
            state.Soulbinder.SpiritCarry = Number(node, "spiritCarry", state.Soulbinder.SpiritCarry);
            state.Soulbinder.ReleaseTicks = Int(node, "releaseTicks", state.Soulbinder.ReleaseTicks);
            state.Soulbinder.Breaths = Long(node, "breaths", state.Soulbinder.Breaths);
            state.Soulbinder.ColdTicks = Int(node, "coldTicks", state.Soulbinder.ColdTicks);
            state.Soulbinder.ColdHits = Number(node, "coldHits", state.Soulbinder.ColdHits);
            state.Soulbinder.ColdResendTicks = Int(node, "coldResendTicks", state.Soulbinder.ColdResendTicks);
            state.Soulbinder.LendHits = Int(node, "lendHits", state.Soulbinder.LendHits);
            state.Soulbinder.Lends = Long(node, "lends", state.Soulbinder.Lends);
            state.Soulbinder.SoulsBack = Long(node, "soulsBack", state.Soulbinder.SoulsBack);
            ReadNumbers(node, "strength", state.Soulbinder.Strength);
            ReadBigs(node, "sourceHp", state.Soulbinder.SourceHp);
        }

        private static void ReadGhostcaptain(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Ghostcaptain.Loaded = Int(node, "loaded", state.Ghostcaptain.Loaded);
            state.Ghostcaptain.LoadHits = Int(node, "loadHits", state.Ghostcaptain.LoadHits);
            state.Ghostcaptain.Mixed = Bool(node, "mixed", state.Ghostcaptain.Mixed);
            state.Ghostcaptain.Volleys = Long(node, "volleys", state.Ghostcaptain.Volleys);
            state.Ghostcaptain.BreachTicks = Int(node, "breachTicks", state.Ghostcaptain.BreachTicks);
            state.Ghostcaptain.BreachResendTicks = Int(node, "breachResendTicks", state.Ghostcaptain.BreachResendTicks);
            state.Ghostcaptain.Sailors = Int(node, "sailors", state.Ghostcaptain.Sailors);
            state.Ghostcaptain.BossSailors = Int(node, "bossSailors", state.Ghostcaptain.BossSailors);
            state.Ghostcaptain.DawnTicks = Int(node, "dawnTicks", state.Ghostcaptain.DawnTicks);
            state.Ghostcaptain.CrewCarry = Number(node, "crewCarry", state.Ghostcaptain.CrewCarry);
            state.Ghostcaptain.Dawns = Long(node, "dawns", state.Ghostcaptain.Dawns);
            state.Ghostcaptain.MateTicks = Int(node, "mateTicks", state.Ghostcaptain.MateTicks);
            state.Ghostcaptain.MatePower = Number(node, "matePower", state.Ghostcaptain.MatePower);
            state.Ghostcaptain.MateShots = Long(node, "mateShots", state.Ghostcaptain.MateShots);
            state.Ghostcaptain.ParrotCasts = Int(node, "parrotCasts", state.Ghostcaptain.ParrotCasts);
            state.Ghostcaptain.Repeats = Long(node, "repeats", state.Ghostcaptain.Repeats);
            state.Ghostcaptain.LastSkillPower = Number(node, "lastSkillPower", state.Ghostcaptain.LastSkillPower);
        }

        private static void ReadMothcaller(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Mothcaller.Moths = Int(node, "moths", state.Mothcaller.Moths);
            state.Mothcaller.SwarmCarry = Number(node, "swarmCarry", state.Mothcaller.SwarmCarry);
            state.Mothcaller.DecayTicks = Int(node, "decayTicks", state.Mothcaller.DecayTicks);
            state.Mothcaller.PollenRelayTicks = Int(node, "pollenRelayTicks", state.Mothcaller.PollenRelayTicks);
            state.Mothcaller.MothTicks = Int(node, "mothTicks", state.Mothcaller.MothTicks);
            state.Mothcaller.AwakeTicks = Int(node, "awakeTicks", state.Mothcaller.AwakeTicks);
            state.Mothcaller.MothCarry = Number(node, "mothCarry", state.Mothcaller.MothCarry);
            state.Mothcaller.Wakes = Long(node, "wakes", state.Mothcaller.Wakes);
            state.Mothcaller.BossSeen = Bool(node, "bossSeen", state.Mothcaller.BossSeen);
        }

        private static void ReadCartomancer(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Cartomancer.DrawHits = Int(node, "drawHits", state.Cartomancer.DrawHits);
            state.Cartomancer.Draws = Long(node, "draws", state.Cartomancer.Draws);
            state.Cartomancer.Arcana = Long(node, "arcana", state.Cartomancer.Arcana);
            state.Cartomancer.CoinsApplied = Bool(node, "coinsApplied", state.Cartomancer.CoinsApplied);
            state.Cartomancer.ArcanaSecondTicks = Int(node, "arcanaSecondTicks", state.Cartomancer.ArcanaSecondTicks);
            state.Cartomancer.TermTicks = Int(node, "termTicks", state.Cartomancer.TermTicks);
            state.Cartomancer.GoldAdded = Number(node, "goldAdded", state.Cartomancer.GoldAdded);
            state.Cartomancer.ExactPaid = Bool(node, "exactPaid", state.Cartomancer.ExactPaid);
            state.Cartomancer.Penalty = Bool(node, "penalty", state.Cartomancer.Penalty);
            state.Cartomancer.Streak = Int(node, "streak", state.Cartomancer.Streak);
            state.Cartomancer.Wins = Long(node, "wins", state.Cartomancer.Wins);
            state.Cartomancer.Losses = Long(node, "losses", state.Cartomancer.Losses);
            state.Cartomancer.BossSeen = Bool(node, "bossSeen", state.Cartomancer.BossSeen);
            state.Cartomancer.LayHits = Int(node, "layHits", state.Cartomancer.LayHits);
            state.Cartomancer.Lays = Long(node, "lays", state.Cartomancer.Lays);
            state.Cartomancer.RevealHits = Number(node, "revealHits", state.Cartomancer.RevealHits);
            state.Cartomancer.RevealSuit = Int(node, "revealSuit", state.Cartomancer.RevealSuit);
            state.Cartomancer.RevealTicks = Int(node, "revealTicks", state.Cartomancer.RevealTicks);
            state.Cartomancer.Reveals = Long(node, "reveals", state.Cartomancer.Reveals);
            state.Cartomancer.CupsCarry = Number(node, "cupsCarry", state.Cartomancer.CupsCarry);
            state.Cartomancer.EffectResendTicks = Int(node, "effectResendTicks", state.Cartomancer.EffectResendTicks);
            state.Cartomancer.Spawned = Bool(node, "spawned", state.Cartomancer.Spawned);
            ReadInts(node, "hand", state.Cartomancer.Hand);
            ReadInts(node, "arcanaTicks", state.Cartomancer.ArcanaTicks);
            ReadInts(node, "laid", state.Cartomancer.Laid);
        }

        private static void ReadHerbwitch(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Herbwitch.Vines = Int(node, "vines", state.Herbwitch.Vines);
            state.Herbwitch.SeedHits = Int(node, "seedHits", state.Herbwitch.SeedHits);
            state.Herbwitch.Seeds = Long(node, "seeds", state.Herbwitch.Seeds);
            state.Herbwitch.Flowers = Long(node, "flowers", state.Herbwitch.Flowers);
            state.Herbwitch.VineCarry = Number(node, "vineCarry", state.Herbwitch.VineCarry);
            state.Herbwitch.GardenResendTicks = Int(node, "gardenResendTicks", state.Herbwitch.GardenResendTicks);
            state.Herbwitch.Venom = Number(node, "venom", state.Herbwitch.Venom);
            state.Herbwitch.VenomCarryOver = Number(node, "venomCarryOver", state.Herbwitch.VenomCarryOver);
            state.Herbwitch.VenomTickCarry = Number(node, "venomTickCarry", state.Herbwitch.VenomTickCarry);
            state.Herbwitch.Brew = Number(node, "brew", state.Herbwitch.Brew);
            state.Herbwitch.DrinkHits = Int(node, "drinkHits", state.Herbwitch.DrinkHits);
            state.Herbwitch.Drinks = Long(node, "drinks", state.Herbwitch.Drinks);
            state.Herbwitch.PartyPoisonTicks = Int(node, "partyPoisonTicks", state.Herbwitch.PartyPoisonTicks);
            state.Herbwitch.PoisonCarry = Number(node, "poisonCarry", state.Herbwitch.PoisonCarry);
            state.Herbwitch.Spawned = Bool(node, "spawned", state.Herbwitch.Spawned);
            state.Herbwitch.FlowerCarry = Int(node, "flowerCarry", state.Herbwitch.FlowerCarry);
            ReadInts(node, "vineStage", state.Herbwitch.VineStage);
            ReadInts(node, "vineGrowTicks", state.Herbwitch.VineGrowTicks);
        }

        private static void ReadGolemancer(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Golemancer.SealHits = Int(node, "sealHits", state.Golemancer.SealHits);
            state.Golemancer.Seals = Long(node, "seals", state.Golemancer.Seals);
            state.Golemancer.GolemTicks = Int(node, "golemTicks", state.Golemancer.GolemTicks);
            state.Golemancer.SecondGolemTicks = Int(node, "secondGolemTicks", state.Golemancer.SecondGolemTicks);
            state.Golemancer.GolemPower = Number(node, "golemPower", state.Golemancer.GolemPower);
            state.Golemancer.GolemCarry = Number(node, "golemCarry", state.Golemancer.GolemCarry);
            state.Golemancer.Runes = Int(node, "runes", state.Golemancer.Runes);
            state.Golemancer.RuneHits = Int(node, "runeHits", state.Golemancer.RuneHits);
            state.Golemancer.EternalCarry = Number(node, "eternalCarry", state.Golemancer.EternalCarry);
            state.Golemancer.Brand = Int(node, "brand", state.Golemancer.Brand);
            state.Golemancer.BrandHits = Number(node, "brandHits", state.Golemancer.BrandHits);
            state.Golemancer.BrandPops = Long(node, "brandPops", state.Golemancer.BrandPops);
            state.Golemancer.BrandResendTicks = Int(node, "brandResendTicks", state.Golemancer.BrandResendTicks);
            state.Golemancer.MinorHits = Int(node, "minorHits", state.Golemancer.MinorHits);
            state.Golemancer.Minors = Int(node, "minors", state.Golemancer.Minors);
            state.Golemancer.MinorCarry = Number(node, "minorCarry", state.Golemancer.MinorCarry);
            state.Golemancer.Spawned = Bool(node, "spawned", state.Golemancer.Spawned);
            ReadInts(node, "minorTicks", state.Golemancer.MinorTicks);
        }

        private static void ReadBoons(ContentNode node, GameState state, EnemyCatalog enemies)
        {
            state.Boons.Fireflies = Int(node, "fireflies", state.Boons.Fireflies);
            state.Boons.FireflyTicks = Int(node, "fireflyTicks", state.Boons.FireflyTicks);
            state.Boons.SparkPower = Number(node, "sparkPower", state.Boons.SparkPower);
            state.Boons.SparkEvery = Int(node, "sparkEvery", state.Boons.SparkEvery);
            state.Boons.BurstPower = Number(node, "burstPower", state.Boons.BurstPower);
            state.Boons.StrikesSinceSpark = Int(node, "strikesSinceSpark", state.Boons.StrikesSinceSpark);
            state.Boons.Sparks = Long(node, "sparks", state.Boons.Sparks);
            state.Boons.BeaconCharges = Int(node, "beaconCharges", state.Boons.BeaconCharges);
            state.Boons.BeaconPower = Number(node, "beaconPower", state.Boons.BeaconPower);
            state.Boons.Hushes = Int(node, "hushes", state.Boons.Hushes);
            state.Boons.HushPower = Number(node, "hushPower", state.Boons.HushPower);
            state.Boons.HushHits = Long(node, "hushHits", state.Boons.HushHits);
            state.Boons.AcidTicks = Int(node, "acidTicks", state.Boons.AcidTicks);
            state.Boons.RedTicks = Int(node, "redTicks", state.Boons.RedTicks);
            state.Boons.BlueTicks = Int(node, "blueTicks", state.Boons.BlueTicks);
            state.Boons.GoldTicks = Int(node, "goldTicks", state.Boons.GoldTicks);
            state.Boons.ElixirPower = Number(node, "elixirPower", state.Boons.ElixirPower);
            state.Boons.ElixirGold = Number(node, "elixirGold", state.Boons.ElixirGold);
            state.Boons.RedEvery = Int(node, "redEvery", state.Boons.RedEvery);
            state.Boons.RedStrikes = Int(node, "redStrikes", state.Boons.RedStrikes);
            state.Boons.ElixirStreak = Int(node, "elixirStreak", state.Boons.ElixirStreak);
            state.Boons.Elixirs = Long(node, "elixirs", state.Boons.Elixirs);
            state.Boons.LendTicks = Int(node, "lendTicks", state.Boons.LendTicks);
            state.Boons.LendPower = Number(node, "lendPower", state.Boons.LendPower);
            state.Boons.LentHits = Long(node, "lentHits", state.Boons.LentHits);
            state.Boons.Pollen = Number(node, "pollen", state.Boons.Pollen);
            state.Boons.PollenRate = Number(node, "pollenRate", state.Boons.PollenRate);
            state.Boons.PollenTicks = Int(node, "pollenTicks", state.Boons.PollenTicks);
            state.Boons.PollenBurnSeconds = Number(node, "pollenBurnSeconds", state.Boons.PollenBurnSeconds);
            state.Boons.PollenEvery = Int(node, "pollenEvery", state.Boons.PollenEvery);
            state.Boons.PollenQueue = Bool(node, "pollenQueue", state.Boons.PollenQueue);
            state.Boons.PollenLight = Number(node, "pollenLight", state.Boons.PollenLight);
            state.Boons.PollenCrits = Int(node, "pollenCrits", state.Boons.PollenCrits);
            state.Boons.PollenBurn = Number(node, "pollenBurn", state.Boons.PollenBurn);
            state.Boons.PollenBurnTicks = Int(node, "pollenBurnTicks", state.Boons.PollenBurnTicks);
            state.Boons.PollenBurnCarry = Number(node, "pollenBurnCarry", state.Boons.PollenBurnCarry);
            state.Boons.PollenShakes = Long(node, "pollenShakes", state.Boons.PollenShakes);
            state.Boons.PetalEvery = Int(node, "petalEvery", state.Boons.PetalEvery);
            state.Boons.PetalPower = Number(node, "petalPower", state.Boons.PetalPower);
            state.Boons.PetalTicks = Int(node, "petalTicks", state.Boons.PetalTicks);
            state.Boons.PetalMax = Int(node, "petalMax", state.Boons.PetalMax);
            state.Boons.PetalHits = Int(node, "petalHits", state.Boons.PetalHits);
            state.Boons.PetalSeeds = Int(node, "petalSeeds", state.Boons.PetalSeeds);
            state.Boons.PetalCarry = Number(node, "petalCarry", state.Boons.PetalCarry);
            state.Boons.PetalStings = Long(node, "petalStings", state.Boons.PetalStings);
        }

        private static int Int(ContentNode node, string field, int fallback) =>
            node.HasField(field) ? node.Field(field).AsInt() : fallback;

        private static double Number(ContentNode node, string field, double fallback) =>
            node.HasField(field) ? node.Field(field).AsNumber(-1e300, 1e300) : fallback;

        private static bool Bool(ContentNode node, string field, bool fallback) =>
            node.HasField(field) ? node.Field(field).AsBool() : fallback;

        private static JsonValue Long(long value) => JsonValue.Of(value.ToString(CultureInfo.InvariantCulture));

        private static long Long(ContentNode node, string field, long fallback)
        {
            if (!node.HasField(field))
                return fallback;
            var item = node.Field(field);
            var text = item.AsString();
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw item.Error($"'{text}' is not a whole number");
            return value;
        }

        private static BigNumber Big(ContentNode node, string field, BigNumber fallback) =>
            node.HasField(field) ? SaveValues.ReadBigNumber(node.Field(field)) : fallback;

        private static EnemyDef? Enemy(ContentNode node, string field, EnemyCatalog enemies)
        {
            if (!node.HasField(field) || node.Field(field).IsNull)
                return null;
            return enemies.TryGet(node.Field(field).AsString(), out var enemy) ? enemy : null;
        }

        private static JsonValue Ints(int[] values)
        {
            var items = new JsonValue[values.Length];
            for (var i = 0; i < values.Length; i++)
                items[i] = JsonValue.Of(values[i]);
            return JsonValue.Array(items);
        }

        private static JsonValue Bools(bool[] values)
        {
            var items = new JsonValue[values.Length];
            for (var i = 0; i < values.Length; i++)
                items[i] = JsonValue.Of(values[i]);
            return JsonValue.Array(items);
        }

        private static JsonValue Numbers(double[] values)
        {
            var items = new JsonValue[values.Length];
            for (var i = 0; i < values.Length; i++)
                items[i] = JsonValue.Of(values[i]);
            return JsonValue.Array(items);
        }

        private static JsonValue Bigs(BigNumber[] values)
        {
            var items = new JsonValue[values.Length];
            for (var i = 0; i < values.Length; i++)
                items[i] = SaveValues.Of(values[i]);
            return JsonValue.Array(items);
        }

        private static IReadOnlyList<ContentNode> Items(ContentNode node, string field, int capacity)
        {
            if (!node.HasField(field))
                return System.Array.Empty<ContentNode>();
            var items = node.Field(field).Items();
            if (items.Count > capacity)
                throw node.Field(field).Error($"expected at most {capacity} values, got {items.Count}");
            return items;
        }

        private static void ReadInts(ContentNode node, string field, int[] values)
        {
            var items = Items(node, field, values.Length);
            for (var i = 0; i < items.Count; i++)
                values[i] = items[i].AsInt();
        }

        private static void ReadBools(ContentNode node, string field, bool[] values)
        {
            var items = Items(node, field, values.Length);
            for (var i = 0; i < items.Count; i++)
                values[i] = items[i].AsBool();
        }

        private static void ReadNumbers(ContentNode node, string field, double[] values)
        {
            var items = Items(node, field, values.Length);
            for (var i = 0; i < items.Count; i++)
                values[i] = items[i].AsNumber(-1e300, 1e300);
        }

        private static void ReadBigs(ContentNode node, string field, BigNumber[] values)
        {
            var items = Items(node, field, values.Length);
            for (var i = 0; i < items.Count; i++)
                values[i] = SaveValues.ReadBigNumber(items[i]);
        }

        private static JsonValue Object(params (string Name, JsonValue Value)[] fields)
        {
            var map = new Dictionary<string, JsonValue>(fields.Length);
            foreach (var (name, value) in fields)
                map.Add(name, value);
            return JsonValue.Object(map);
        }
    }
}
