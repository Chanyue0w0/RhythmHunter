using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    [RequireComponent(typeof(Camera))]
    public sealed class OtterCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 lookAhead = new(0f, 0.65f);
        [SerializeField] private float smoothTime = 0.22f;
        [SerializeField] private Vector2 minimum = new(-6.5f, -3.5f);
        [SerializeField] private Vector2 maximum = new(6.5f, 3.5f);

        private Vector3 velocity;

        public void Configure(Transform followTarget, Vector2 minBounds, Vector2 maxBounds)
        {
            target = followTarget;
            minimum = minBounds;
            maximum = maxBounds;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + (Vector3)lookAhead;
            desired.x = Mathf.Clamp(desired.x, minimum.x, maximum.x);
            desired.y = Mathf.Clamp(desired.y, minimum.y, maximum.y);
            desired.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
