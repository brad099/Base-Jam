using UnityEngine;

namespace FrostFlame
{
    /// <summary>
    /// Kinematic platform driven by activators.
    /// Hold: travels to B while any activator is on, returns to A otherwise (elevator).
    /// PingPong: shuttles between A and B while any activator is on, stops otherwise.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        public enum Mode { Hold, PingPong }

        public Mode mode = Mode.Hold;
        public Activator[] activators;
        public Vector2 offsetToB = new Vector2(0f, 3f);
        public float speed = 3f;
        public float pauseAtEnds = 0.4f;
        public SpriteRenderer lamp;
        public Color offColor = new Color(0.35f, 0.35f, 0.4f);
        public Color onColor = Color.yellow;

        public Vector2 Velocity { get; private set; }

        Rigidbody2D rb;
        Vector2 a, b;
        bool towardB = true;
        float pause;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            a = rb.position;
            b = a + offsetToB;
        }

        bool Active()
        {
            if (activators == null) return false;
            foreach (var act in activators) if (act != null && act.IsOn) return true;
            return false;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool active = Active();
            Vector2 pos = rb.position;
            Vector2 target = pos;

            if (mode == Mode.Hold)
            {
                target = active ? b : a;
            }
            else if (active)
            {
                if (pause > 0f) pause -= dt;
                else
                {
                    target = towardB ? b : a;
                    if (Vector2.Distance(pos, target) < 0.001f)
                    {
                        towardB = !towardB;
                        pause = pauseAtEnds;
                        target = pos;
                    }
                }
            }

            Vector2 next = Vector2.MoveTowards(pos, target, speed * dt);
            Velocity = (next - pos) / dt;
            rb.MovePosition(next);

            if (lamp) lamp.color = active ? onColor : offColor;
        }

        void OnDrawGizmos()
        {
            Vector2 start = Application.isPlaying ? a : (Vector2)transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, start + offsetToB);
        }
    }
}
