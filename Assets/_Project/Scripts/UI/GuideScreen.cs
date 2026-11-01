using AS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AS.UI
{
    /// <summary>
    /// A short field guide: one page of enemies and one of pickups, each showing for
    /// <see cref="pageDuration"/> seconds or until any button is pressed. Shown before the very first
    /// run (Play on the title goes through <see cref="PlayFromTitle"/>), and any time from the title's
    /// Guide button. Runs on unscaled time, since the title screen freezes the game.
    /// </summary>
    public class GuideScreen : UIPanel
    {
        [SerializeField] GameManager game;
        [SerializeField] InputReader input;
        [Tooltip("Shown one at a time, in order.")]
        [SerializeField] GameObject[] pages;
        [Tooltip("Shows which page this is, e.g. 1 / 2.")]
        [SerializeField] TMP_Text pageLabel;
        [SerializeField, Min(0.5f)] float pageDuration = 4f;
        [Tooltip("Presses this soon after a page appears are ignored, so the press that opened the guide can't skip it.")]
        [SerializeField, Min(0f)] float inputGrace = 0.3f;

        int _page = -1;
        float _pageShownAt;
        bool _playAfter;
        GameObject _returnSelection;

        public bool IsOpen => _page >= 0;
        /// <summary>The page showing, from 0; -1 when closed.</summary>
        public int Page => _page;
        public int PageCount => pages.Length;

        /// <summary>For the title's Play button: the guide first if it has never been seen, then the run.</summary>
        public void PlayFromTitle()
        {
            if (GameSettings.GuideSeen) game.Play();
            else Open(playAfter: true);
        }

        /// <summary>For the title's Guide button: the guide, then back to the title.</summary>
        public void Show() => Open(playAfter: false);

        /// <summary>The next page, or close after the last one.</summary>
        public void Next()
        {
            if (!IsOpen) return;
            if (_page + 1 < pages.Length) ShowPage(_page + 1);
            else Close();
        }

        void Open(bool playAfter)
        {
            if (IsOpen) return;
            _playAfter = playAfter;
            var events = EventSystem.current;
            _returnSelection = events != null ? events.currentSelectedGameObject : null;
            SetVisible(true);
            // Nothing selected, so Submit can't press a title button hidden behind the guide.
            if (events != null) events.SetSelectedGameObject(null);
            input.AnyButtonPressed += OnAnyButton;
            ShowPage(0);
        }

        void ShowPage(int page)
        {
            _page = page;
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == page);
            if (pageLabel != null) pageLabel.text = $"{page + 1} / {pages.Length}";
            _pageShownAt = Time.unscaledTime;
        }

        void Close()
        {
            _page = -1;
            input.AnyButtonPressed -= OnAnyButton;
            SetVisible(false);
            GameSettings.GuideSeen = true;
            GameSettings.Save();

            if (_playAfter) game.Play();
            else if (_returnSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_returnSelection);
        }

        void OnAnyButton()
        {
            if (IsOpen && Time.unscaledTime >= _pageShownAt + inputGrace) Next();
        }

        void Update()
        {
            if (IsOpen && Time.unscaledTime >= _pageShownAt + pageDuration) Next();
        }

        void OnDestroy()
        {
            if (IsOpen) input.AnyButtonPressed -= OnAnyButton;
        }
    }
}
