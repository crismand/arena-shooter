using UnityEngine;
using UnityEngine.EventSystems;

namespace AS.UI
{
    /// <summary>
    /// A full-screen panel shown and hidden through its CanvasGroup, so its scripts keep running
    /// (and keep listening to events) while hidden. Selects a control on show for gamepad users.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] GameObject firstSelected;

        CanvasGroup _group;

        public bool IsVisible => _group != null && _group.alpha > 0.5f;

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
            if (visible && firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }
}
