using Game.Platform.Windows.Input;
using NUnit.Framework;

namespace Game.Platform.Tests
{
    public class KeyboardEventFilterTests
    {
        private const ushort Make = 0x0000;
        private const ushort Break = 0x0001;
        private const ushort KeyA = 0x41;
        private const ushort KeyC = 0x43;
        private const ushort Control = 0x11;
        private const ushort LeftShift = 0xA0;
        private const ushort LeftWindows = 0x5B;

        private KeyboardEventFilter _filter = null!;

        [SetUp]
        public void SetUp() => _filter = new KeyboardEventFilter();

        [TestCase((ushort)0x0000, true)]
        [TestCase((ushort)0x0002, true)]
        [TestCase((ushort)0x0001, false)]
        [TestCase((ushort)0x0003, false)]
        public void OnlyMakeEventsAreKeyDowns(ushort flags, bool expected)
        {
            Assert.That(KeyboardEventFilter.IsKeyDown(flags), Is.EqualTo(expected));
        }

        [Test]
        public void HeldKeyCountsOnceDespiteAutoRepeat()
        {
            var counted = 0;
            for (var i = 0; i < 50; i++)
                if (_filter.Process(KeyA, Make) != KeyClass.None)
                    counted++;
            _filter.Process(KeyA, Break);

            Assert.That(counted, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseAndPressAgainCountsAgain()
        {
            Assert.That(_filter.Process(KeyA, Make), Is.EqualTo(KeyClass.Regular));
            Assert.That(_filter.Process(KeyA, Break), Is.EqualTo(KeyClass.None));
            Assert.That(_filter.Process(KeyA, Make), Is.EqualTo(KeyClass.Regular));
        }

        [Test]
        public void ControlPlusCCountsAsOnePress()
        {
            var counted = 0;
            foreach (var (key, flags) in new[] { (Control, Make), (KeyC, Make), (KeyC, Break), (Control, Break) })
                if (_filter.Process(key, flags) != KeyClass.None)
                    counted++;

            Assert.That(counted, Is.EqualTo(1));
        }

        [Test]
        public void KeyWithControlOrAltHeldIsACombo()
        {
            _filter.Process(Control, Make);
            Assert.That(_filter.Process(KeyC, Make), Is.EqualTo(KeyClass.Combo));
            _filter.Process(KeyC, Break);
            _filter.Process(Control, Break);

            _filter.Process(0xA4, Make);
            Assert.That(_filter.Process(KeyA, Make), Is.EqualTo(KeyClass.Combo));
            _filter.Process(KeyA, Break);
            _filter.Process(0xA4, Break);

            Assert.That(_filter.Process(KeyA, Make), Is.EqualTo(KeyClass.Regular));
        }

        [TestCase((ushort)0x10)]
        [TestCase((ushort)0x11)]
        [TestCase((ushort)0x12)]
        [TestCase(LeftShift)]
        [TestCase((ushort)0xA5)]
        [TestCase(LeftWindows)]
        [TestCase((ushort)0x5C)]
        public void ModifiersAreNeverCounted(ushort modifier)
        {
            Assert.That(_filter.Process(modifier, Make), Is.EqualTo(KeyClass.None));
        }

        [TestCase((ushort)0x0D, KeyClass.Enter)]
        [TestCase((ushort)0x08, KeyClass.Backspace)]
        [TestCase((ushort)0x20, KeyClass.Space)]
        [TestCase((ushort)0x41, KeyClass.Regular)]
        [TestCase((ushort)0x70, KeyClass.Regular)]
        public void KeysAreClassified(ushort key, KeyClass expected)
        {
            Assert.That(_filter.Process(key, Make), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidKeyIsIgnored()
        {
            Assert.That(_filter.Process(0xFF, Make), Is.EqualTo(KeyClass.None));
        }

        [Test]
        public void CountersSplitCategoriesAndIncludeThemInTotal()
        {
            var counters = new InputCounters();
            counters.AddKey(KeyClass.Regular);
            counters.AddKey(KeyClass.Enter);
            counters.AddKey(KeyClass.Backspace);
            counters.AddKey(KeyClass.Space);
            counters.AddKey(KeyClass.Space);
            counters.AddKey(KeyClass.Combo);
            counters.AddKey(KeyClass.None);

            var snapshot = counters.Drain();

            Assert.That(snapshot.KeyDowns, Is.EqualTo(6));
            Assert.That(snapshot.ComboDowns, Is.EqualTo(1));
            Assert.That(snapshot.EnterDowns, Is.EqualTo(1));
            Assert.That(snapshot.BackspaceDowns, Is.EqualTo(1));
            Assert.That(snapshot.SpaceDowns, Is.EqualTo(2));
            Assert.That(counters.Drain().KeyDowns, Is.EqualTo(0));
        }
    }
}
