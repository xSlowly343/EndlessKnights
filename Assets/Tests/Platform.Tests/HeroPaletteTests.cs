using Game.Presentation.Battle;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class HeroPaletteTests
    {
        [Test]
        public void TheCloakAndTheGlowAreToldApartFromTheBodyAndTheSword()
        {
            Assert.That(HeroPalette.IsCloak(new Color32(81, 51, 115, 255)), Is.True);
            Assert.That(HeroPalette.IsCloak(new Color32(193, 129, 212, 255)), Is.True);
            Assert.That(HeroPalette.IsCloak(new Color32(31, 15, 53, 255)), Is.True);
            Assert.That(HeroPalette.IsGlow(new Color32(111, 253, 146, 255)), Is.True);
            Assert.That(HeroPalette.IsGlow(new Color32(0, 48, 13, 255)), Is.True);
            foreach (var other in new[] { new Color32(0, 0, 0, 255), new Color32(85, 85, 85, 255), new Color32(108, 108, 108, 255) })
            {
                Assert.That(HeroPalette.IsCloak(other), Is.False);
                Assert.That(HeroPalette.IsGlow(other), Is.False);
            }
        }

        [Test]
        public void ARecolourKeepsTheShadingAndLeavesTheRestAlone()
        {
            var pixels = new[]
            {
                new Color32(81, 51, 115, 255), new Color32(31, 15, 53, 255), new Color32(111, 253, 146, 255),
                new Color32(85, 85, 85, 255), new Color32(0, 0, 0, 0), new Color32(32, 202, 123, 43)
            };
            Color crimson = new Color32(140, 31, 46, 255);
            Color ice = new Color32(124, 216, 255, 255);

            HeroPalette.Recolor(pixels, crimson, ice);

            Assert.That((int)pixels[0].r, Is.EqualTo(140).Within(1), "the base tone becomes the chosen colour");
            Assert.That((int)pixels[0].g, Is.EqualTo(31).Within(1));
            Assert.That((int)pixels[0].b, Is.EqualTo(46).Within(1));
            Assert.That(pixels[1].r, Is.GreaterThan(pixels[1].b).And.LessThan(pixels[0].r), "a shadow stays a shadow");
            Assert.That((int)pixels[2].r, Is.EqualTo(124).Within(1));
            Assert.That((int)pixels[2].g, Is.EqualTo(216).Within(1));
            Assert.That((int)pixels[2].b, Is.EqualTo(255).Within(1));
            Assert.That(pixels[3], Is.EqualTo(new Color32(85, 85, 85, 255)));
            Assert.That(pixels[4].a, Is.Zero);
            Assert.That(pixels[5].a, Is.EqualTo(43), "a see-through slash pixel keeps its alpha");
            Assert.That(pixels[5].b, Is.GreaterThan(pixels[5].r));
        }

        [Test]
        public void ACloakPatternPaintsEachFrameByItsOwnCloak()
        {
            var cloak = new Color32(81, 51, 115, 255);
            var light = new Color32(142, 99, 149, 255);
            var steel = new Color32(85, 85, 85, 255);
            var none = new Color32(0, 0, 0, 0);
            Color32[] Sheet() => new[]
            {
                none, none, none, none, none, none,
                none, cloak, none, cloak, cloak, cloak,
                none, light, none, cloak, cloak, cloak,
                none, cloak, none, cloak, steel, cloak,
                none, none, none, none, none, none
            };
            Color navy = new Color32(42, 58, 106, 255);
            Color gold = new Color32(240, 192, 64, 255);

            var trim = Sheet();
            HeroPalette.Recolor(trim, 6, 3, new CloakPaint(navy, gold, CloakPattern.Trim), null);
            Assert.That(trim[7].r, Is.GreaterThan(trim[7].b), "a cloak one pixel wide is all edge");
            Assert.That(trim[9].r, Is.GreaterThan(trim[9].b), "the second frame starts its own edge");
            Assert.That(trim[16].b, Is.GreaterThan(trim[16].r), "a pixel walled in by cloak and steel keeps the first colour");
            Assert.That(trim[22], Is.EqualTo(steel));

            var gradient = Sheet();
            HeroPalette.Recolor(gradient, 6, 3, new CloakPaint(navy, gold, CloakPattern.Gradient), null);
            Assert.That(gradient[21].b, Is.GreaterThan(gradient[21].r), "the top row of the sheet is the first colour");
            Assert.That(gradient[9].r, Is.GreaterThan(gradient[9].b), "the bottom row is the second");

            var duotone = Sheet();
            HeroPalette.Recolor(duotone, 6, 3, new CloakPaint(navy, gold, CloakPattern.Duotone), null);
            Assert.That(duotone[13].r, Is.GreaterThan(duotone[13].b), "highlights take the second colour");
            Assert.That(duotone[7].b, Is.GreaterThan(duotone[7].r));

            var plain = Sheet();
            HeroPalette.Recolor(plain, 6, 3, new CloakPaint(navy, gold, CloakPattern.Plain), null);
            Assert.That((int)plain[7].r, Is.EqualTo(42).Within(1));
            Assert.That((int)plain[7].b, Is.EqualTo(106).Within(1));
            Assert.That(CloakPaint.PatternOf("stars"), Is.EqualTo(CloakPattern.Stars));
            Assert.That(CloakPaint.PatternOf(string.Empty), Is.EqualTo(CloakPattern.Plain));
        }

        [Test]
        public void TheDefaultLookKeepsTheOriginalSheetsAndOthersGetTheirOwn()
        {
            Assert.That(HeroModel.Skin(default), Is.SameAs(HeroModel.Skin(default)));
            Assert.That(HeroModel.Skin(default).Idle.Frame(0).texture.name, Is.EqualTo("Seeker_Void_Idle_30x30_24f"));
            Assert.That(HeroModel.Skin(default).Attack.FrameCount, Is.EqualTo(8));
        }
    }
}
