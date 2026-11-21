using UnityEngine;

namespace StormLab
{
    [CreateAssetMenu(menuName = "Storm Lab/Charge sequence timings")]
    public sealed class ChargeSequenceProfile : ScriptableObject
    {
        [Min(0.05f)] public float charge = 1.45f;
        [Min(0.05f)] public float travel = 0.28f;
        [Min(0.05f)] public float impact = 0.72f;
    }
}

