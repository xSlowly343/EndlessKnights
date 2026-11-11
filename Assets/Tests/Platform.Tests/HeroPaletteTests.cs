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
        public void TheDefaultLookKeepsTheOriginalSheetsAndOthersGetTheirOwn()
        {
            Assert.That(HeroModel.Skin(default), Is.SameAs(HeroModel.Skin(default)));
            Assert.That(HeroModel.Skin(default).Idle.Frame(0).texture.name, Is.EqualTo("Seeker_Void_Idle_30x30_24f"));
            Assert.That(HeroModel.Skin(default).Attack.FrameCount, Is.EqualTo(8));
        }
    }
}
