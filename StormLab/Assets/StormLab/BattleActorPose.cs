using UnityEngine;

namespace StormLab
{
    public abstract class BattleActorPose : MonoBehaviour
    {
        public virtual void PrepareShot(Vector3 point) { }
        public abstract void React(RaycastHit hit);
        public abstract void ResetPose();
    }
}
