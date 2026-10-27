#if UNITY_EDITOR
using System.Collections;
using AS.Core;
using AS.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AS.Tests
{
    /// <summary>The title screen, the pause menu's buttons and the settings menu.</summary>
    public class MenuTests : ArenaTestFixture
    {
        const string MixerPath = "Assets/_Project/Audio/ArenaMixer.mixer";

        static T Panel<T>() where T : UIPanel => Object.FindAnyObjectByType<T>();
        static Button FindButton(UIPanel panel, string name) => panel.transform.Find(name).GetComponent<Button>();
        static Slider FindSlider(string name) => GameObject.Find("UI/SettingsMenu/" + name).GetComponent<Slider>();

        static float MixerLevel(string parameter)
        {
            Assert.IsTrue(LoadAsset<AudioMixer>(MixerPath).GetFloat(parameter, out float level), $"Mixer doesn't expose {parameter}.");
            return level;
        }

        static void AssertWiredTo(Button button, Object target, string method)
        {
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), $"{button.name} isn't wired.");
            Assert.AreSame(target, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(method, button.onClick.GetPersistentMethodName(0));
        }

        // ------------------------------------------------------------------ title screen

        [UnityTest]
        public IEnumerator Game_OpensOnTheTitleScreen_WithTheArenaFrozen()
        {
            HighScoreStore.TrySubmit(250);
            yield return LoadArena(stayOnTitle: true);

            Assert.IsTrue(game.State.Is(GameState.Title));
            Assert.IsTrue(Panel<TitleScreen>().IsVisible);
            Assert.AreEqual("BEST 250", FindText("UI/TitleScreen/Best").text);
            Assert.AreEqual(0f, Time.timeScale);

            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return null;
            yield return null;
            Release(keyboard.dKey);
            Assert.AreEqual(startX, player.transform.position.x, "Player moved behind the title screen.");

            // Escape does nothing here.
            game.TogglePause();
            Assert.IsTrue(game.State.Is(GameState.Title));
        }

        [UnityTest]
        public IEnumerator TitleScreen_WithNoBestScore_LeavesTheBestLineEmpty()
        {
            yield return LoadArena(stayOnTitle: true);
            Assert.AreEqual("", FindText("UI/TitleScreen/Best").text);
        }

        [UnityTest]
        public IEnumerator TitleScreen_Play_StartsARun()
        {
            yield return LoadArena(stayOnTitle: true);
            var title = Panel<TitleScreen>();
            AssertWiredTo(FindButton(title, "QuitButton"), game, nameof(GameManager.Quit));

            FindButton(title, "PlayButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.IsFalse(title.IsVisible);
            Assert.AreEqual(1f, Time.timeScale);
            float startX = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.3f);
            Release(keyboard.dKey);
            Assert.Greater(player.transform.position.x, startX + 0.5f, "Player can't move after pressing Play.");
        }

        [UnityTest]
        public IEnumerator TitleScreen_Settings_OpensSettings_AndBackReturns()
        {
            yield return LoadArena(stayOnTitle: true);
            var title = Panel<TitleScreen>();
            var settings = Panel<SettingsMenu>();

            FindButton(title, "SettingsButton").onClick.Invoke();
            Assert.IsTrue(settings.IsVisible);
            Assert.IsFalse(title.IsVisible);

            FindButton(settings, "BackButton").onClick.Invoke();
            Assert.IsFalse(settings.IsVisible);
            Assert.IsTrue(title.IsVisible);
            Assert.IsTrue(game.State.Is(GameState.Title));
        }

        // ------------------------------------------------------------------ pause menu

        [UnityTest]
        public IEnumerator PauseMenu_HasAQuitButton_WiredToGameManagerQuit()
        {
            yield return LoadArena();
            game.SetPaused(true);
            yield return null;

            var quit = FindButton(Panel<PauseMenu>(), "QuitButton");
            Assert.IsTrue(quit.interactable);
            // Pressing it would stop Play mode and end the test run, so check the wiring instead.
            AssertWiredTo(quit, game, nameof(GameManager.Quit));
        }

        [UnityTest]
        public IEnumerator PauseMenu_Settings_OpensSettings_AndBackReturnsToThePauseMenu()
        {
            yield return LoadArena();
            game.SetPaused(true);
            yield return null;
            var pause = Panel<PauseMenu>();
            var settings = Panel<SettingsMenu>();

            FindButton(pause, "SettingsButton").onClick.Invoke();
            Assert.IsTrue(settings.IsVisible);
            Assert.IsFalse(pause.IsVisible);

            FindButton(settings, "BackButton").onClick.Invoke();
            Assert.IsFalse(settings.IsVisible);
            Assert.IsTrue(pause.IsVisible);
            Assert.IsTrue(game.State.Is(GameState.Paused));
        }

        [UnityTest]
        public IEnumerator Settings_OpenedFromPause_CloseWhenThePlayerUnpauses()
        {
            yield return LoadArena();
            game.SetPaused(true);
            yield return null;
            FindButton(Panel<PauseMenu>(), "SettingsButton").onClick.Invoke();

            game.TogglePause();
            yield return null;

            Assert.IsTrue(game.State.Is(GameState.Playing));
            Assert.IsFalse(Panel<SettingsMenu>().IsVisible);
            Assert.IsFalse(Panel<PauseMenu>().IsVisible);
        }

        // ------------------------------------------------------------------ settings

        [UnityTest]
        public IEnumerator Settings_VolumeSliders_SetTheMixer_AndAreSaved()
        {
            yield return LoadArena(stayOnTitle: true);
            Assert.AreEqual(0f, MixerLevel(SettingsMenu.MusicParameter), 0.01f);

            FindSlider("MusicSlider").value = 0.5f;
            FindSlider("EffectsSlider").value = 0f;

            Assert.AreEqual(-6.02f, MixerLevel(SettingsMenu.MusicParameter), 0.01f);
            Assert.AreEqual(GameSettings.SilentDecibels, MixerLevel(SettingsMenu.SfxParameter), 0.01f);
            Assert.AreEqual(0.5f, GameSettings.MusicVolume, 0.001f);
            Assert.AreEqual(0f, GameSettings.SfxVolume, 0.001f);
        }

        [UnityTest]
        public IEnumerator Settings_SavedBeforeLaunch_AreAppliedOnStart()
        {
            GameSettings.MusicVolume = 0.25f;
            GameSettings.SfxVolume = 0.5f;
            yield return LoadArena(stayOnTitle: true);

            Assert.AreEqual(0.25f, FindSlider("MusicSlider").value, 0.001f);
            Assert.AreEqual(0.5f, FindSlider("EffectsSlider").value, 0.001f);
            Assert.AreEqual(-12.04f, MixerLevel(SettingsMenu.MusicParameter), 0.01f);
            Assert.AreEqual(-6.02f, MixerLevel(SettingsMenu.SfxParameter), 0.01f);
        }

        [UnityTest]
        public IEnumerator Settings_FullscreenButton_FlipsTheSetting_AndItsLabel()
        {
            GameSettings.Fullscreen = false;
            yield return LoadArena(stayOnTitle: true);
            var button = FindButton(Panel<SettingsMenu>(), "FullscreenButton");
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            Assert.AreEqual("FULLSCREEN: OFF", label.text);

            button.onClick.Invoke();
            Assert.IsTrue(GameSettings.Fullscreen);
            Assert.AreEqual("FULLSCREEN: ON", label.text);

            button.onClick.Invoke();
            Assert.IsFalse(GameSettings.Fullscreen);
            Assert.AreEqual("FULLSCREEN: OFF", label.text);
        }
    }
}
#endif
