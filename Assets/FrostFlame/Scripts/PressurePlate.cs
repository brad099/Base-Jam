using UnityEngine;

namespace FrostFlame
{
    /// <summary>On while any hero or pushable block rests on it.</summary>
    public class PressurePlate : Activator
    {
        public Transform button;
        public SpriteRenderer lamp;
        public Color offColor = new Color(0.35f, 0.35f, 0.4f);
        public Color onColor = Color.yellow;
        public Vector2 zoneSize = new Vector2(1.2f, 0.4f);
        public Vector2 zoneOffset = new Vector2(0f, 0.25f);

        bool on;
        Vector3 buttonUp;
        public override bool IsOn => on;

        void Start() { if (button) buttonUp = button.localPosition; }

        void FixedUpdate()
        {
            on = false;
            var cols = Physics2D.OverlapBoxAll((Vector2)transform.position + zoneOffset, zoneSize, 0f);
            foreach (var c in cols)
            {
                if (c.isTrigger) continue;
                var p = c.GetComponent<PlayerController>();
                if ((p != null && !p.IsDead) || c.GetComponent<PushBlock>() != null) { on = true; break; }
            }
        }

        void Update()
        {
            if (button)
            {
                Vector3 target = buttonUp + (on ? Vector3.down * 0.12f : Vector3.zero);
                button.localPosition = Vector3.Lerp(button.localPosition, target, Time.deltaTime * 20f);
            }
            if (lamp) lamp.color = Color.Lerp(lamp.color, on ? onColor : offColor, Time.deltaTime * 12f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube((Vector2)transform.position + zoneOffset, zoneSize);
        }
    }
}
