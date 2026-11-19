using UnityEngine;

namespace FrostFlame
{
    /// <summary>Base class for anything that can switch a mechanism on (plates, levers).</summary>
    public abstract class Activator : MonoBehaviour
    {
        public abstract bool IsOn { get; }
    }
}
