using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightWeaponAttachment : MonoBehaviour
    {
        [SerializeField] private FightCharacterCombatAnimator characterAnimator;
        [SerializeField] private WeaponAttachmentData attachmentData;
        [SerializeField] private FightWeaponVisual weaponPrefab;
        private FightWeaponVisual weapon;
        private Transform anchor;
        private bool flipX, flipY;
        public FightWeaponVisual Weapon => weapon;
        public WeaponAttachmentData Data => attachmentData;
        public FightWeaponVisual Prefab => weaponPrefab;
        public void Configure(FightCharacterCombatAnimator animator, WeaponAttachmentData data, FightWeaponVisual prefab)
        {
            Unsubscribe();
            characterAnimator = animator; attachmentData = data; weaponPrefab = prefab;
            if (Application.isPlaying && isActiveAndEnabled) { Subscribe(); ReplaceWeapon(prefab); }
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Subscribe(); ReplaceWeapon(weaponPrefab);
        }
        private void Subscribe()
        {
            if (characterAnimator == null) return;
            characterAnimator.DisplayedFrameChanged -= ApplyFrame;
            characterAnimator.DisplayedFrameChanged += ApplyFrame;
        }
        private void Unsubscribe()
        { if (characterAnimator != null) characterAnimator.DisplayedFrameChanged -= ApplyFrame; }
        public void ReplaceWeapon(FightWeaponVisual prefab)
        {
            weaponPrefab = prefab;
            if (weapon != null) { weapon.gameObject.SetActive(false); Release(weapon.gameObject); }
            weapon = null;
            if (characterAnimator == null || characterAnimator.TargetRenderer == null || prefab == null) return;
            if (anchor == null)
            {
                anchor = new GameObject("WeaponAnchor").transform;
                anchor.SetParent(characterAnimator.TargetRenderer.transform, false);
            }
            weapon = Instantiate(prefab, anchor, false);
            if (Application.isPlaying)
                foreach (var presentation in FindObjectsByType<Art2.Art2ActorPresentation>(FindObjectsSortMode.None))
                    if (presentation.isActiveAndEnabled && presentation.gameObject.scene == gameObject.scene)
                        presentation.RegisterWeaponRenderer(weapon.Renderer);
            if (weapon.Renderer != null && weapon.Renderer.sprite != null)
            {
                var properties = new MaterialPropertyBlock();
                weapon.Renderer.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex", weapon.Renderer.sprite.texture);
                weapon.Renderer.SetPropertyBlock(properties);
            }
            ApplyFrame(characterAnimator.DisplayedFrame);
        }
        private void LateUpdate()
        {
            var renderer = characterAnimator != null ? characterAnimator.TargetRenderer : null;
            if (renderer != null && (flipX != renderer.flipX || flipY != renderer.flipY)) ApplyFrame(characterAnimator.DisplayedFrame);
        }
        private void ApplyFrame(FightCharacterCombatAnimator.DisplayedSpriteFrame frame)
        {
            if (weapon == null || anchor == null || characterAnimator.TargetRenderer == null) return;
            var body = characterAnimator.TargetRenderer;
            flipX = body.flipX; flipY = body.flipY;
            bool found = attachmentData != null && attachmentData.TryGet(frame.Id, out _);
            if (!found) { weapon.gameObject.SetActive(false); return; }
            attachmentData.TryGet(frame.Id, out var pose);
            weapon.gameObject.SetActive(pose.visible && frame.Sprite != null);
            // Reflect the complete pose once. Parent Transform scale is already inherited.
            anchor.localScale = new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 1);
            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;
            var rotation = Quaternion.Euler(0, 0, pose.rotation - weapon.ForwardAngle);
            weapon.transform.localRotation = rotation;
            var grip = Vector3.Scale(weapon.GripPoint, weapon.transform.localScale);
            weapon.transform.localPosition = new Vector3(pose.position.x, pose.position.y, weapon.DepthOffset) - rotation * grip;
        }
        private void OnDisable()
        {
            Unsubscribe();
            if (weapon != null) weapon.gameObject.SetActive(false);
        }
        private static void Release(GameObject target)
        { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
        private void OnDestroy() { if (anchor != null) Release(anchor.gameObject); }
    }
}
