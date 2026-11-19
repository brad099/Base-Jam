using UnityEngine;

namespace FrostFlame
{
    /// <summary>Loops a list of sprites on a SpriteRenderer (used for the animated pools).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool randomStart = true;

        SpriteRenderer sr;
        float offset;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (randomStart) offset = Random.value * 10f;
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int i = Mathf.FloorToInt((Time.time + offset) * fps) % frames.Length;
            sr.sprite = frames[i];
        }
    }
}
