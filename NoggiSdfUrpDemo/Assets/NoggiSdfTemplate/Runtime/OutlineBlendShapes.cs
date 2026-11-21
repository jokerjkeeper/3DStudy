using UnityEngine;

namespace NoggiSdfToolkit
{
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("")]
    public sealed class OutlineBlendShapes : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer source;
        [SerializeField] SkinnedMeshRenderer outline;

        public void Bind(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            source = from;
            outline = to;
        }

        void LateUpdate()
        {
            if (!source || !outline || !source.sharedMesh || source.sharedMesh != outline.sharedMesh)
                return;

            for (int index = 0; index < source.sharedMesh.blendShapeCount; index++)
                outline.SetBlendShapeWeight(index, source.GetBlendShapeWeight(index));
        }
    }
}
