#if UNITY_EDITOR
using System.Collections;
using AS.Core;
using AS.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>The field guide: before the first run, from the title's Guide button, and paging.</summary>
    public class GuideTests : ArenaTestFixture
    {
        GuideScreen _guide;
        TitleScreen _title;

        IEnumerator LoadTitle(bool guideSeen)
        {
            GameSettings.GuideSeen = guideSeen;
            yield return LoadArena(stayOnTitle: true);
            _guide = Object.FindAnyObjectByType<GuideScreen>();
            _title = Object.FindAnyObjectByType<TitleScreen>();
        }

        void Click(string button) => _title.transform.Find(button).GetComponent<Button>().onClick.Invoke();

        /// <summary>Waits out the grace period, then presses a key.</summary>
        IEnumerator PressAnyButton()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstPlay_ShowsBothPages_ThenStartsTheRun()
        {
            yield return LoadTitle(guideSeen: false);
            Click("PlayButton");
            yield return null;

            Assert.IsTrue(_guide.IsVisible);
            Assert.AreEqual(0, _guide.Page);
            Assert.IsTrue(game.State.Is(GameState.Title), "The run waits for the guide.");
            Assert.AreEqual("1 / 2", FindText("UI/GuideScreen/PageLabel").text);

            yield return PressAnyButton();
            Assert.AreEqual(1, _guide.Page);
            yield return PressAnyButton();

            Assert.IsFalse(_guide.IsVisible);
            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.IsTrue(GameSettings.GuideSeen);
        }

        [UnityTest]
        public IEnumerator Play_OnceTheGuideHasBeenSeen_StartsStraightAway()
        {
            yield return LoadTitle(guideSeen: true);
            Click("PlayButton");
            yield return null;

            Assert.IsFalse(_guide.IsVisible);
            Assert.IsTrue(game.State.Is(GameState.Playing));
        }

        [UnityTest]
        public IEnumerator GuideButton_ShowsTheGuide_ThenReturnsToTheTitle()
        {
            yield return LoadTitle(guideSeen: true);
            Click("GuideButton");
            yield return null;
            Assert.IsTrue(_guide.IsVisible);

            yield return PressAnyButton();
            yield return PressAnyButton();

            Assert.IsFalse(_guide.IsVisible);
            Assert.IsTrue(game.State.Is(GameState.Title));
            Assert.IsTrue(_title.IsVisible);
        }

        [UnityTest]
        public IEnumerator Pages_TurnByThemselves_AfterAFewSeconds()
        {
            yield return LoadTitle(guideSeen: true);
            Click("GuideButton");
            yield return new WaitForSecondsRealtime(4.3f);

            Assert.AreEqual(1, _guide.Page, "The first page should have moved on after 4 s.");
        }

        [UnityTest]
        public IEnumerator EnemyPage_NamesEveryEnemy_AndPickupPage_EveryPickup()
        {
            yield return LoadTitle(guideSeen: true);
            foreach (string enemy in new[] { "CHASER", "SHOOTER", "TANK", "DASHER", "SPLITTER" })
                Assert.IsNotNull(GameObject.Find($"UI/GuideScreen/Enemies/{enemy}"), enemy);
            foreach (string pickup in new[] { "SPREAD", "RAPID", "SHIELD", "HEALTH", "BOMB", "HOMING" })
                Assert.IsNotNull(GameObject.Find($"UI/GuideScreen/Pickups/{pickup}"), pickup);
        }
    }
}
#endif
