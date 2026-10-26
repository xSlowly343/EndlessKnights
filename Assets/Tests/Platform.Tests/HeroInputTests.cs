using System.IO;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class HeroInputTests
    {
        private PanelCanvas _panel = null!;
        private GameLoop _loop = null!;
        private FakeInputSource _input = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            Texts.UseLanguage("ru");
            _input = new FakeInputSource();
            _loop = new GameLoop(data, _input, new GameState { Class = HeroClass.Viking }, new StatSheet());
            _panel = PanelCanvas.Create();
            _panel.Bind(_loop, data.Tree);
        }

        [TearDown]
        public void TearDown()
        {
            NumberDisplay.Use(NumberFormatSettings.Default);
            Object.DestroyImmediate(_panel.gameObject);
        }

        [Test]
        public void InputTabShowsSessionAndAllTimeCounters()
        {
            _input.PressKeys(1234);
            _input.ClickLeft(3);
            _loop.Tick();

            Open();

            Assert.That(_panel.Hero.InputCell(0, 0), Is.EqualTo(Texts.Get("stats.keys")));
            Assert.That(_panel.Hero.InputCell(0, 1), Is.EqualTo("1 234"));
            Assert.That(_panel.Hero.InputCell(0, 2), Is.EqualTo("1 234"));
            Assert.That(_panel.Hero.InputCell(1, 1), Does.StartWith("3"));
        }

        [Test]
        public void ThousandsSeparatorFollowsTheNumberFormat()
        {
            NumberDisplay.Use(new NumberFormatSettings(NumberFormat.Letters, 2, ",", "."));
            _input.PressKeys(1234);
            _loop.Tick();

            Open();

            Assert.That(_panel.Hero.InputCell(0, 1), Is.EqualTo("1,234"));
        }

        private void Open()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);
            navigator.SelectSubTab(2);
            _panel.Show(navigator);
            _panel.Hero.Refresh(_loop);
        }
    }
}
