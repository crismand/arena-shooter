using UnityEngine;

namespace AS.Enemies
{
    /// <summary>Heads straight for the target.</summary>
    public class Chaser : EnemyBase
    {
        protected override Vector2 DesiredVelocity() =>
            ((Vector2)(Target.position - transform.position)).normalized * data.MoveSpeed;
    }
}
