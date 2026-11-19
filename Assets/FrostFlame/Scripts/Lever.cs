using UnityEngine;

namespace FrostFlame
{
    /// <summary>Toggle switch. A hero standing next to it presses their "use" key (S / Down).</summary>
    public class Lever : Activator
    {
        public Transform handle;
        public SpriteRenderer lamp;
        public Color offColor = new Color(0.35f, 0.35f, 0.4f);
        public Color onColor = new Color(0.8f, 0.45f, 1f);
        public bool startOn;

        bool on;
        public override bool IsOn => on;

        void Awake() { on = startOn; }

        public void Toggle() { on = !on; }

        void Update()
        {
            if (handle)
            {
                Quaternion target = Quaternion.Euler(0f, 0f, on ? -35f : 35f);
                handle.localRotation = Quaternion.Lerp(handle.localRotation, target, Time.deltaTime * 14f);
            }
            if (lamp) lamp.color = Color.Lerp(lamp.color, on ? onColor : offColor, Time.deltaTime * 12f);
        }
    }
}
