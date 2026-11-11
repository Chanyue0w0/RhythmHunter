using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // A weapon remains a single combined hand + weapon sprite. No combat events live here.
    [DefaultExecutionOrder(-10)]
    public sealed partial class FightWeaponVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Grip in the weapon root's local units. Does not change the source sprite pivot.")]
        [SerializeField] private Vector2 gripPoint;
        [Tooltip("Angle of the blade's forward axis in its authored sprite; right = 0, up = 90.")]
        [SerializeField] private float forwardAngle = 90;
        [Tooltip("Small offset along the body sprite's local Z; negative is toward its front.")]
        [SerializeField] private float depthOffset = -.005f;
        public SpriteRenderer Renderer => spriteRenderer;
        public Vector2 GripPoint => gripPoint;
        public float ForwardAngle => forwardAngle;
        public float DepthOffset => depthOffset;
        public void Configure(SpriteRenderer renderer, Vector2 grip, float forward)
        { spriteRenderer = renderer; gripPoint = grip; forwardAngle = forward; }
    }
}
