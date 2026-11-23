using UnityEngine;

namespace NoggiDemo
{
    [RequireComponent(typeof(Camera))]
    public sealed class PerlicaComparisonFraming : MonoBehaviour
    {
        [SerializeField, Min(.01f)] float minimumHalfWidth = .406f;

        Camera view;
        float defaultHalfHeight;

        void Awake()
        {
            view = GetComponent<Camera>();
            defaultHalfHeight = view.orthographicSize;
        }

        void LateUpdate()
        {
            view.orthographicSize = Mathf.Max(defaultHalfHeight, minimumHalfWidth / view.aspect);
        }
    }
}
