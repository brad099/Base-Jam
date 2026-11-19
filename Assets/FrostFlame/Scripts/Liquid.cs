using UnityEngine;

namespace FrostFlame
{
    /// <summary>
    /// A pool surface. Lava harms Frost, water harms Ember, poison harms both.
    /// <see cref="safeFor"/> is the hero that may wade through; Both = nobody is safe.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Liquid : MonoBehaviour
    {
        public Element safeFor = Element.Fire;

        void Reset() { GetComponent<BoxCollider2D>().isTrigger = true; }

        void OnTriggerEnter2D(Collider2D other) { Check(other); }
        void OnTriggerStay2D(Collider2D other) { Check(other); }

        void Check(Collider2D other)
        {
            var p = other.GetComponent<PlayerController>();
            if (p == null || p.IsDead) return;
            if (safeFor == Element.Both || p.element != safeFor) p.Die();
        }
    }
}
