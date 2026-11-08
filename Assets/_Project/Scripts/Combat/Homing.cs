using System.Collections.Generic;
using UnityEngine;

namespace AS.Combat
{
    /// <summary>
    /// Steers a moving projectile toward the nearest target within <see cref="range"/>, turning at
    /// most <see cref="turnRate"/> degrees a second and keeping its speed. Sits next to
    /// <see cref="Projectile"/>, which still handles hits and lifetime.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Homing : MonoBehaviour
    {
        [SerializeField] LayerMask targets;
        [SerializeField, Min(0f)] float range = 8f;
        [Tooltip("Degrees per second. Lower makes wide, lazy curves.")]
        [SerializeField, Min(0f)] float turnRate = 300f;

        // Shared by every homing bullet: only used inside one FixedUpdate at a time.
        static readonly List<Collider2D> Found = new();

        Rigidbody2D _body;
        ContactFilter2D _filter;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _filter = new ContactFilter2D { useLayerMask = true, layerMask = targets, useTriggers = false };
        }

        void FixedUpdate()
        {
            Vector2 velocity = _body.linearVelocity;
            // Pooled and waiting: nothing to steer.
            if (velocity.sqrMagnitude < 0.0001f || !TryFindNearest(out Vector2 target)) return;

            float turn = Vector2.SignedAngle(velocity, target - _body.position);
            float maxTurn = turnRate * Time.fixedDeltaTime;
            velocity = Quaternion.Euler(0f, 0f, Mathf.Clamp(turn, -maxTurn, maxTurn)) * velocity;

            _body.linearVelocity = velocity;
            _body.rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        }

        bool TryFindNearest(out Vector2 nearest)
        {
            nearest = default;
            Physics2D.OverlapCircle(_body.position, range, _filter, Found);
            float best = float.MaxValue;
            foreach (var hit in Found)
            {
                Vector2 position = hit.attachedRigidbody != null ? hit.attachedRigidbody.position : (Vector2)hit.transform.position;
                float distance = (position - _body.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = position;
                }
            }
            return best < float.MaxValue;
        }
    }
}
