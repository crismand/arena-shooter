using AS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace AS.UI
{
    /// <summary>
    /// Music and effects volume (through the mixer's exposed MusicVolume and SfxVolume) and a
    /// fullscreen toggle, stored in <see cref="GameSettings"/>. Opened from the title screen or
    /// the pause menu; Back returns there. Saved settings are applied on start, even if the menu
    /// is never opened.
    /// </summary>
    public class SettingsMenu : UIPanel
    {
        public const string MusicParameter = "MusicVolume";
        public const string SfxParameter = "SfxVolume";

        [SerializeField] AudioMixer mixer;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] TMP_Text fullscreenLabel;

        UIPanel _returnTo;

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        // Not Awake: the mixer ignores SetFloat until the audio system has started.
        void Start()
        {
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
            ApplyVolume(MusicParameter, GameSettings.MusicVolume);
            ApplyVolume(SfxParameter, GameSettings.SfxVolume);
            ApplyFullscreen(GameSettings.Fullscreen);
        }

        /// <summary>For UI buttons: hides <paramref name="from"/> and shows this menu until Back.</summary>
        public void Open(UIPanel from)
        {
            _returnTo = from;
            if (from != null) from.SetVisible(false);
            SetVisible(true);
        }

        /// <summary>For UI buttons. Saves and goes back to the menu that opened this one.</summary>
        public void Back()
        {
            GameSettings.Save();
            SetVisible(false);
            if (_returnTo != null) _returnTo.SetVisible(true);
            _returnTo = null;
        }

        /// <summary>For UI buttons.</summary>
        public void ToggleFullscreen()
        {
            GameSettings.Fullscreen = !GameSettings.Fullscreen;
            ApplyFullscreen(GameSettings.Fullscreen);
        }

        void SetMusicVolume(float volume)
        {
            GameSettings.MusicVolume = volume;
            ApplyVolume(MusicParameter, volume);
        }

        void SetSfxVolume(float volume)
        {
            GameSettings.SfxVolume = volume;
            ApplyVolume(SfxParameter, volume);
        }

        void ApplyVolume(string parameter, float volume)
        {
            if (mixer != null) mixer.SetFloat(parameter, GameSettings.ToDecibels(volume));
        }

        void ApplyFullscreen(bool fullscreen)
        {
            fullscreenLabel.text = fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF";
            // Does nothing in the editor's Game view.
            if (Screen.fullScreen != fullscreen)
                Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        // Unpausing with Escape leaves the menu that opened this one, so close without going back.
        void OnGameStateChanged(GameStateChanged _)
        {
            if (!IsVisible) return;
            GameSettings.Save();
            SetVisible(false);
            _returnTo = null;
        }
    }
}
