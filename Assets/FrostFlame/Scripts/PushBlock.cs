using UnityEngine;

namespace FrostFlame
{
    /// <summary>Marker for the heavy snow block heroes can shove around (and stand on).</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PushBlock : MonoBehaviour
    {
        Rigidbody2D rb;

        void Awake() { rb = GetComponent<Rigidbody2D>(); }

        void FixedUpdate()
        {
            // stop sliding quickly once nobody pushes it
            var v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, 12f * Time.fixedDeltaTime);
            rb.linearVelocity = v;
        }
    }
}
