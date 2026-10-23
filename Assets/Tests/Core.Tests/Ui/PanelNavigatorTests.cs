using System;
using System.Linq;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Ui
{
    public class PanelNavigatorTests
    {
        [Test]
        public void DefaultBarHasFiveTabsWithPartyAndPortalInMore()
        {
            var navigator = new PanelNavigator();

            Assert.That(navigator.Bar, Is.EqualTo(new[] { PanelSection.Hero, PanelSection.Book, PanelSection.Tree, PanelSection.Items, PanelSection.Party }));
            Assert.That(navigator.More, Is.EqualTo(new[] { PanelSection.Portal, PanelSection.Stats, PanelSection.Camp, PanelSection.Shop, PanelSection.Settings }));
        }

        [Test]
        public void MenuOpensTheLastTabAndClosesAgain()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Items);
            navigator.Close();

            navigator.PressMenu();

            Assert.That(navigator.IsOpen, Is.True);
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Items));
            navigator.PressMenu();
            Assert.That(navigator.IsOpen, Is.False);
        }

        [Test]
        public void ClickOnTheActiveTabCollapsesThePanel()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Book);

            navigator.SelectTab(PanelSection.Book);

            Assert.That(navigator.IsOpen, Is.False);
        }

        [Test]
        public void ClickOnAnotherTabSwitchesWithoutClosing()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Book);

            navigator.SelectTab(PanelSection.Tree);

            Assert.That(navigator.IsOpen, Is.True);
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Tree));
            Assert.That(navigator.Title, Is.EqualTo("Дерево"));
        }

        [Test]
        public void MoreShowsTheListAndASectionFromItOpensInTheSamePanel()
        {
            var navigator = new PanelNavigator();
            navigator.PressMore();

            Assert.That(navigator.ShowingMore, Is.True);
            Assert.That(navigator.Title, Is.EqualTo("Ещё"));
            Assert.That(navigator.SubTabs, Is.Empty);

            navigator.SelectTab(PanelSection.Portal);

            Assert.That(navigator.ShowingMore, Is.False);
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Portal));
            Assert.That(navigator.Height, Is.EqualTo(PanelHeight.Half));
        }

        [Test]
        public void PinnedPanelIgnoresClicksOutsideButNotTheCloseButton()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);
            navigator.TogglePin();

            navigator.ClickOutside();
            Assert.That(navigator.IsOpen, Is.True);

            navigator.Close();
            Assert.That(navigator.IsOpen, Is.False);
        }

        [Test]
        public void UnpinnedPanelClosesOnClickOutside()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);

            navigator.ClickOutside();

            Assert.That(navigator.IsOpen, Is.False);
        }

        [Test]
        public void HeightIsRememberedForEachTab()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);
            navigator.ToggleHeight();
            navigator.SelectTab(PanelSection.Book);

            Assert.That(navigator.Height, Is.EqualTo(PanelHeight.Full));

            navigator.SelectTab(PanelSection.Hero);
            Assert.That(navigator.Height, Is.EqualTo(PanelHeight.Half));
        }

        [Test]
        public void OnlyTheTreeCanBeWide()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Items);
            navigator.ToggleWide();
            Assert.That(navigator.IsWide, Is.False);

            navigator.SelectTab(PanelSection.Tree);
            navigator.ToggleWide();
            Assert.That(navigator.IsWide, Is.True);
            Assert.That(navigator.Frame.WidthPx, Is.EqualTo(960));
            Assert.That(navigator.Frame.HeightPx, Is.EqualTo(640));

            navigator.SelectTab(PanelSection.Items);
            Assert.That(navigator.IsWide, Is.False);
            Assert.That(navigator.Frame.WidthPx, Is.EqualTo(480));
        }

        [Test]
        public void SubTabIsRememberedPerSectionAndOutOfRangeIsIgnored()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Hero);
            navigator.SelectSubTab(2);
            navigator.SelectSubTab(7);
            navigator.SelectTab(PanelSection.Party);

            Assert.That(navigator.SubTab, Is.EqualTo(0));
            navigator.SelectTab(PanelSection.Hero);
            Assert.That(navigator.SubTab, Is.EqualTo(2));
            Assert.That(navigator.SubTabs[navigator.SubTab], Is.EqualTo("Ввод"));
        }

        [Test]
        public void WheelCyclesThroughTheBar()
        {
            var navigator = new PanelNavigator();
            navigator.SelectTab(PanelSection.Party);

            navigator.Scroll(1);
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Hero));

            navigator.Scroll(-2);
            Assert.That(navigator.Active, Is.EqualTo(PanelSection.Items));
        }

        [Test]
        public void PlayerCanPutAMoreSectionOnTheBar()
        {
            var navigator = new PanelNavigator();

            navigator.PlaceOnBar(4, PanelSection.Portal);

            Assert.That(navigator.Bar[4], Is.EqualTo(PanelSection.Portal));
            Assert.That(navigator.More, Does.Contain(PanelSection.Party));
            Assert.That(navigator.More, Does.Not.Contain(PanelSection.Portal));
        }

        [Test]
        public void MovingATabAlongTheBarSwapsIt()
        {
            var navigator = new PanelNavigator();

            navigator.PlaceOnBar(0, PanelSection.Tree);

            Assert.That(navigator.Bar, Is.EqualTo(new[] { PanelSection.Tree, PanelSection.Book, PanelSection.Hero, PanelSection.Items, PanelSection.Party }));
        }

        [Test]
        public void BarMustHoldFiveDifferentSections()
        {
            Assert.Throws<ArgumentException>(() => new PanelNavigator(new[] { PanelSection.Hero, PanelSection.Hero, PanelSection.Tree, PanelSection.Items, PanelSection.Party }));
            Assert.Throws<ArgumentException>(() => new PanelNavigator(new[] { PanelSection.Hero }));
        }

        [Test]
        public void ChangedFiresOnEveryVisibleChange()
        {
            var navigator = new PanelNavigator();
            var changes = 0;
            navigator.Changed += () => changes++;

            navigator.PressMenu();
            navigator.SelectTab(PanelSection.Tree);
            navigator.ToggleWide();
            navigator.Close();
            navigator.Close();

            Assert.That(changes, Is.EqualTo(4));
        }

        [Test]
        public void EverySectionHasATitle()
        {
            Assert.That(PanelSections.All.Select(PanelSections.Title), Has.All.Not.Empty);
        }
    }
}
