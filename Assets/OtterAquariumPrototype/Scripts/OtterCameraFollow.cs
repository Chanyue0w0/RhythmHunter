using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [RequireComponent(typeof(Camera))]
    public sealed class OtterCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 lookAhead = new(0f, 0.65f);
        [SerializeField] private float smoothTime = 0.22f;
        [SerializeField] private Vector2 minimum = new(-11f, -8.25f);
        [SerializeField] private Vector2 maximum = new(11f, 8.25f);
        [SerializeField] private bool keepViewportInsideBounds = true;

        private Vector3 velocity;
        private Camera followCamera;

        public void Configure(Transform followTarget, Vector2 minBounds, Vector2 maxBounds)
        {
            target = followTarget;
            minimum = minBounds;
            maximum = maxBounds;
        }

        private void Awake()
        {
            followCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + (Vector3)lookAhead;
            Vector2 centerMinimum = minimum;
            Vector2 centerMaximum = maximum;
            if (keepViewportInsideBounds && followCamera != null && followCamera.orthographic)
            {
                float halfHeight = followCamera.orthographicSize;
                float halfWidth = halfHeight * followCamera.aspect;
                centerMinimum += new Vector2(halfWidth, halfHeight);
                centerMaximum -= new Vector2(halfWidth, halfHeight);

                if (centerMinimum.x > centerMaximum.x)
                    centerMinimum.x = centerMaximum.x = (minimum.x + maximum.x) * 0.5f;
                if (centerMinimum.y > centerMaximum.y)
                    centerMinimum.y = centerMaximum.y = (minimum.y + maximum.y) * 0.5f;
            }

            desired.x = Mathf.Clamp(desired.x, centerMinimum.x, centerMaximum.x);
            desired.y = Mathf.Clamp(desired.y, centerMinimum.y, centerMaximum.y);
            desired.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
