using AS.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace AS.Feedback
{
    /// <summary>
    /// Cuts the camera straight to its target when the arena resets, instead of letting the follow
    /// damping glide it back from where the player died. Also drops any shake still playing.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraSnap : MonoBehaviour
    {
        CinemachineCamera _camera;

        void Awake() => _camera = GetComponent<CinemachineCamera>();

        void OnEnable() => EventBus.Subscribe<ArenaReset>(OnArenaReset);
        void OnDisable() => EventBus.Unsubscribe<ArenaReset>(OnArenaReset);

        void OnArenaReset(ArenaReset _)
        {
            // Without a valid previous state, the next camera update skips damping.
            _camera.PreviousStateIsValid = false;
            CinemachineImpulseManager.Instance.Clear();
        }
    }
}
