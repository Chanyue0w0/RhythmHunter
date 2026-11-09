using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [DisallowMultipleComponent]
    public sealed class Art2CharacterShadowSettings : MonoBehaviour
    {
        [Tooltip("Disable this component to use the scene defaults instead.")]
        public bool showShadow = true;
        [Tooltip("Ground-plane width (X) and depth (Z), in world units.")]
        public Vector2 size = new(1.25f, .65f);
        [Tooltip("World X/Z offset from FootAnchor. Does not move the character.")]
        public Vector2 groundOffset;
        public bool followsLight = true;
        [Min(0)] public float projectionHeight = .25f;
        [Min(0)] public float maxLightOffset = .45f;
    }
}
