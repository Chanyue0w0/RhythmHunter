using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Scrolls a full-width sprite to the left and keeps a duplicate beside it for a seamless loop.
    /// </summary>
    public sealed class LoopingBackgroundScroller : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField, Min(0f)] private float unitsPerSecond = 0.17f;

        private Transform loopCopy;
        private Vector3 startPosition;
        private float loopWidth;
        private float travelledDistance;

        private void Awake()
        {
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();

            CreateLoopCopy();
            ResetPositions();
        }

        private void OnEnable()
        {
            travelledDistance = 0f;
            ResetPositions();
        }

        private void Update()
        {
            if (loopCopy == null || loopWidth <= 0f)
                return;

            float parentScaleX = transform.parent == null
                ? 1f
                : Mathf.Abs(transform.parent.lossyScale.x);
            float localUnitsPerSecond = unitsPerSecond / Mathf.Max(0.0001f, parentScaleX);
            travelledDistance = Mathf.Repeat(
                travelledDistance + localUnitsPerSecond * Time.unscaledDeltaTime,
                loopWidth);

            Vector3 offset = Vector3.left * travelledDistance;
            transform.localPosition = startPosition + offset;
            loopCopy.localPosition = startPosition + Vector3.right * loopWidth + offset;
        }

        private void OnDisable()
        {
            travelledDistance = 0f;
            ResetPositions();
        }

        private void CreateLoopCopy()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            startPosition = transform.localPosition;
            loopWidth = sourceRenderer.sprite.rect.width /
                        Mathf.Max(1f, sourceRenderer.sprite.pixelsPerUnit) *
                        Mathf.Abs(transform.localScale.x);

            GameObject copyObject = new($"{name} (Loop Copy)");
            loopCopy = copyObject.transform;
            loopCopy.SetParent(transform.parent, false);
            loopCopy.localRotation = transform.localRotation;
            loopCopy.localScale = transform.localScale;

            SpriteRenderer copyRenderer = copyObject.AddComponent<SpriteRenderer>();
            copyRenderer.sprite = sourceRenderer.sprite;
            copyRenderer.color = sourceRenderer.color;
            copyRenderer.flipX = sourceRenderer.flipX;
            copyRenderer.flipY = sourceRenderer.flipY;
            copyRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            copyRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            copyRenderer.sortingOrder = sourceRenderer.sortingOrder;
            copyRenderer.maskInteraction = sourceRenderer.maskInteraction;
        }

        private void ResetPositions()
        {
            if (loopCopy == null || loopWidth <= 0f)
                return;

            transform.localPosition = startPosition;
            loopCopy.localPosition = startPosition + Vector3.right * loopWidth;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            unitsPerSecond = Mathf.Max(0f, unitsPerSecond);
        }
#endif
    }
}
