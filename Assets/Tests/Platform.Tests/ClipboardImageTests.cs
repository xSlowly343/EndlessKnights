using Game.Platform.Windows.Clipboard;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class ClipboardImageTests
    {
        [Test]
        public void ThePictureBecomesABottomUpBgraBitmap()
        {
            var rgba = new byte[] { 10, 20, 30, 0, 40, 50, 60, 128 };

            var dib = ClipboardImage.DibOf(rgba, 2, 1);

            Assert.That(dib.Length, Is.EqualTo(ClipboardImage.HeaderBytes + 8));
            Assert.That(dib[0], Is.EqualTo(ClipboardImage.HeaderBytes));
            Assert.That(dib[4], Is.EqualTo(2), "width");
            Assert.That(dib[8], Is.EqualTo(1), "a positive height keeps Unity's bottom-up rows");
            Assert.That(dib[12], Is.EqualTo(1), "planes");
            Assert.That(dib[14], Is.EqualTo(32), "bits per pixel");
            Assert.That(dib[20], Is.EqualTo(8), "image bytes");
            Assert.That(new[] { dib[40], dib[41], dib[42], dib[43] }, Is.EqualTo(new byte[] { 30, 20, 10, 255 }), "blue first, opaque");
            Assert.That(new[] { dib[44], dib[45], dib[46], dib[47] }, Is.EqualTo(new byte[] { 60, 50, 40, 255 }));
        }

        [Test]
        public void ACardOfTheSharedSizeHasTheRightHeader()
        {
            var dib = ClipboardImage.DibOf(new byte[1200 * 630 * 4], 1200, 630);

            Assert.That(dib[4] | dib[5] << 8, Is.EqualTo(1200));
            Assert.That(dib[8] | dib[9] << 8, Is.EqualTo(630));
        }

        [Test]
        public void PixelsThatDoNotMatchTheSizeAreRefused()
        {
            Assert.That(() => ClipboardImage.DibOf(new byte[7], 2, 1), Throws.ArgumentException);
            Assert.That(() => ClipboardImage.DibOf(new byte[0], 0, 0), Throws.ArgumentException);
        }
    }
}
