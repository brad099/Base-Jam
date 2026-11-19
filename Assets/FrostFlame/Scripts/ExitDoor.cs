using UnityEngine;

namespace FrostFlame
{
    /// <summary>
    /// Goal chest. Opens while its matching hero stands in front of it.
    /// The level is won when both chests are occupied at the same time.
    /// </summary>
    public class ExitDoor : MonoBehaviour
    {
        public Element element = Element.Fire;
        public SpriteRenderer chest;
        public Sprite[] openFrames;           // closed -> open
        public Vector2 zoneSize = new Vector2(1.0f, 1.2f);
        public Vector2 zoneOffset = new Vector2(0f, 0.6f);

        public bool Occupied { get; private set; }
        float open;   // 0..1

        void FixedUpdate()
        {
            Occupied = false;
            var cols = Physics2D.OverlapBoxAll((Vector2)transform.position + zoneOffset, zoneSize, 0f);
            foreach (var c in cols)
            {
                var p = c.GetComponent<PlayerController>();
                if (p != null && !p.IsDead && p.element == element && p.Grounded) { Occupied = true; break; }
            }
        }

        void Update()
        {
            open = Mathf.MoveTowards(open, Occupied ? 1f : 0f, Time.deltaTime * 4f);
            if (chest && openFrames != null && openFrames.Length > 0)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(open * (openFrames.Length - 1)), 0, openFrames.Length - 1);
                chest.sprite = openFrames[i];
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = ElementColors.Of(element);
            Gizmos.DrawWireCube((Vector2)transform.position + zoneOffset, zoneSize);
        }
    }
}
