using UnityEngine;

namespace RhythmHunter.TopDownBeatCombat
{
    public sealed class TopDownBeatCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private TopDownBeatPlayer player;
        [SerializeField, Min(0f)] private float smoothTime = 0.14f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 0.85f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 16f;
        [SerializeField] private Vector2 bounds = new(8.5f, 5.4f);

        private Vector3 velocity;
        private float shakeStrength;
        private float shakeUntil;

        public Transform Target => target;

        public void Configure(Transform followTarget, TopDownBeatPlayer controlledPlayer)
        {
            target = followTarget;
            player = controlledPlayer;
        }

        public void Kick(float strength, float duration = 0.1f)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeUntil = Mathf.Max(shakeUntil, Time.unscaledTime + duration);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector2 look = player != null ? player.Facing * lookAheadDistance : Vector2.zero;
            Vector3 desired = new(
                Mathf.Clamp(target.position.x + look.x, -bounds.x, bounds.x),
                Mathf.Clamp(target.position.y + look.y, -bounds.y, bounds.y),
                transform.position.z);
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

            if (Time.unscaledTime < shakeUntil)
            {
                Vector2 jitter = Random.insideUnitCircle * shakeStrength;
                smoothed += new Vector3(jitter.x, jitter.y, 0f);
            }
            else
            {
                shakeStrength = 0f;
            }

            smoothed.x = Mathf.Round(smoothed.x * pixelsPerUnit) / pixelsPerUnit;
            smoothed.y = Mathf.Round(smoothed.y * pixelsPerUnit) / pixelsPerUnit;
            transform.position = smoothed;
        }
    }
}
