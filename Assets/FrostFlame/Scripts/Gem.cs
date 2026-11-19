using UnityEngine;

namespace FrostFlame
{
    /// <summary>Spinning gem that only its matching hero can collect.</summary>
    public class Gem : MonoBehaviour
    {
        public Element element = Element.Fire;
        public Transform art;

        Vector3 basePos;
        float seed;
        float collectT = -1f;
        SpriteRenderer sr;

        void Start()
        {
            if (art == null) art = transform;
            basePos = art.localPosition;
            seed = Random.value * 10f;
            sr = art.GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (collectT >= 0f)
            {
                collectT += Time.deltaTime;
                float k = collectT / 0.3f;
                art.localPosition = basePos + Vector3.up * k * 0.8f;
                art.localScale = Vector3.one * (1f + k * 0.6f);
                if (sr) { var c = sr.color; c.a = 1f - k; sr.color = c; }
                if (k >= 1f) Destroy(gameObject);
                return;
            }
            float t = Time.time + seed;
            art.localPosition = basePos + Vector3.up * Mathf.Sin(t * 2.5f) * 0.08f;
            art.localScale = new Vector3(Mathf.Lerp(0.25f, 1f, Mathf.Abs(Mathf.Cos(t * 2f))), 1f, 1f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collectT >= 0f) return;
            var p = other.GetComponent<PlayerController>();
            if (p == null || p.IsDead || p.element != element) return;
            collectT = 0f;
            GetComponent<Collider2D>().enabled = false;
            p.Collect();
            if (GameManager.Instance) GameManager.Instance.AddGem(element);
        }
    }
}
