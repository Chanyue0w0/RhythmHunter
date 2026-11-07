using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [ExecuteAlways, RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class Art2ShadowTestLight : MonoBehaviour
    {
        [SerializeField] private Transform aimTarget;
        [Tooltip("Moving this directional light changes its aim, not distance attenuation. Disable to rotate manually.")]
        [SerializeField] private bool aimAtTarget = true;

        private void LateUpdate()
        {
            if (!aimAtTarget || aimTarget == null) return;
            Vector3 direction = aimTarget.position - transform.position;
            if (direction.sqrMagnitude < .001f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .99f ? Vector3.forward : Vector3.up;
            transform.rotation = Quaternion.LookRotation(direction, up);
        }

        private void OnDrawGizmosSelected()
        {
            if (aimTarget == null) return;
            Gizmos.color = new Color(1, .75f, .25f);
            Gizmos.DrawLine(transform.position, aimTarget.position);
            Gizmos.DrawWireSphere(aimTarget.position, .2f);
        }
    }
}
