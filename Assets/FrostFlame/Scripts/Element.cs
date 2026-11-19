using UnityEngine;

namespace FrostFlame
{
    /// <summary>Which hero an object belongs to / harms.</summary>
    public enum Element
    {
        Fire,
        Ice,
        Both
    }

    public static class ElementColors
    {
        public static readonly Color Fire = new Color(1f, 0.45f, 0.18f);
        public static readonly Color Ice = new Color(0.35f, 0.8f, 1f);
        public static readonly Color Poison = new Color(0.45f, 0.95f, 0.3f);

        public static Color Of(Element e)
        {
            switch (e)
            {
                case Element.Fire: return Fire;
                case Element.Ice: return Ice;
                default: return Poison;
            }
        }

        public static string Name(Element e) => e == Element.Fire ? "Ember" : e == Element.Ice ? "Frost" : "Both";
    }
}
