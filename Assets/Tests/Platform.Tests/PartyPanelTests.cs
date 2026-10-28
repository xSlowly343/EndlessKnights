using System.IO;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Ui;
using Game.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace Game.Platform.Tests
{
    public class PartyPanelTests
    {
        private PanelCanvas _panel = null!;
        private GameLoop _loop = null!;

        [SetUp]
        public void SetUp()
        {
            var data = GameData.Load(name => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data", name)));
            _loop = new GameLoop(data, new FakeInputSource(), new GameState { Class = HeroClass.Viking, RecordStage = 500 }, new StatSheet());
            _panel = PanelCanvas.Create();
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Party);
            _panel.Show(navigator);
            _panel.Place(0f, true);
            _panel.Party.Show(true, _loop);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_panel.gameObject);

        [Test]
        public void SoloPartyShowsOnlyYouAndNoSharedEnemy()
        {
            Assert.That(_panel.Party.IsShown, Is.True);
            Assert.That(_panel.Party.ShownMembers, Is.EqualTo(1));
            Assert.That(_panel.Party.ShowsEnemy, Is.False);
        }

        [Test]
        public void YourOwnRowOpensNoMemberMenu()
        {
            _panel.Party.UseMemberOptions(PartyMemberMenu.Options(_loop.Data.Party.Search));

            _panel.TryClick(_panel.Party.WorldPointOfRow(0));

            Assert.That(_panel.Party.ShowsMemberMenu, Is.False);
        }

        [Test]
        public void PartyButtonsSayOnlinePartiesComeLater()
        {
            Assert.That(_panel.TryClick(_panel.Party.WorldPointOfHelp()), Is.True);

            Assert.That(_panel.IsNoticeShown, Is.True);
        }
    }
}
