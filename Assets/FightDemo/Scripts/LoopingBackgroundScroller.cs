using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Scrolls a sprite to the right and keeps copies on both sides for a seamless loop.
    /// </summary>
    public sealed class LoopingBackgroundScroller : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField, Min(0f)] private float unitsPerSecond = 0.17f;
        [SerializeField, Min(0)] private int cropLeftPixels;
        [SerializeField, Min(0)] private int cropRightPixels;
        [SerializeField, Min(0)] private int loopGapPixels;
        [SerializeField, Min(0)] private int seamOverlapPixels;
        [SerializeField] private bool splitIntoBeatSlices;
        [SerializeField] private FightCombatController beatSource;
        [SerializeField] private Sprite[] beatSliceSpriteOverrides;
        [SerializeField] private Vector3[] beatSlicePositionOverrides;
        [SerializeField] private bool copyChildRenderers;

        private Transform leftLoopCopy;
        private Transform rightLoopCopy;
        private Sprite originalSprite;
        private Sprite croppedSprite;
        private Vector3 startPosition;
        private float unscaledSpriteWidth;
        private float travelledDistance;
        private float restScaleX;
        private float motionMultiplier = 1f;

        // Changing speed/direction must not reset the accumulated scroll position.
        public void SetMotionMultiplier(float multiplier) => motionMultiplier = multiplier;
        private readonly List<(Transform source, Transform copy)> copiedTransforms = new();
        private MaterialPropertyBlock spriteProperties;
        private bool usingBeatSlices;
        private readonly List<Sprite> beatSliceSprites = new();
        private readonly List<Vector3> beatSlicePositions = new();
        private readonly List<int> beatSliceParityIndices = new();

        private void Awake()
        {
            restScaleX = Mathf.Abs(transform.localScale.x);
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();

            ApplyHorizontalCrop();
            usingBeatSlices = copyChildRenderers ||
                              (splitIntoBeatSlices &&
                               (CreateConfiguredBeatSliceAssets() || CreateBeatSliceAssets()));
            if (usingBeatSlices)
            {
                if (!copyChildRenderers)
                    CreateBeatSliceRenderers(transform);
                sourceRenderer.enabled = false;
            }
            CreateLoopCopies();
            ResetPositions();
        }

        private void OnEnable()
        {
            travelledDistance = 0f;
            ResetPositions();
        }

        private void LateUpdate()
        {
            if (leftLoopCopy == null || rightLoopCopy == null || unscaledSpriteWidth <= 0f)
                return;

            float parentScaleX = transform.parent == null
                ? 1f
                : Mathf.Abs(transform.parent.lossyScale.x);
            float localUnitsPerSecond = unitsPerSecond / Mathf.Max(0.0001f, parentScaleX);
            float loopWidth = GetLoopWidth();
            travelledDistance = Mathf.Repeat(
                travelledDistance + localUnitsPerSecond * motionMultiplier * Time.deltaTime,
                loopWidth);

            float centeredOffset = Mathf.Repeat(
                travelledDistance + loopWidth * 0.5f,
                loopWidth) - loopWidth * 0.5f;
            Vector3 centerPosition = startPosition + Vector3.right * centeredOffset;

            SyncCopyTransform(leftLoopCopy);
            SyncCopyTransform(rightLoopCopy);
            transform.localPosition = centerPosition;
            leftLoopCopy.localPosition = centerPosition + Vector3.left * loopWidth;
            rightLoopCopy.localPosition = centerPosition + Vector3.right * loopWidth;
            // Only the authored grass owns animation. Copies display that same pose.
            foreach (var pair in copiedTransforms)
            {
                if (pair.source == null || pair.copy == null) continue;
                pair.copy.localPosition = pair.source.localPosition;
                pair.copy.localRotation = pair.source.localRotation;
                pair.copy.localScale = pair.source.localScale;
            }
        }

        private void OnDisable()
        {
            travelledDistance = 0f;
            ResetPositions();
        }

        private void CreateLoopCopies()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            startPosition = transform.localPosition;
            unscaledSpriteWidth = sourceRenderer.sprite.rect.width /
                                  Mathf.Max(1f, sourceRenderer.sprite.pixelsPerUnit);

            leftLoopCopy = CreateLoopCopy("Left");
            rightLoopCopy = CreateLoopCopy("Right");
        }

        private Transform CreateLoopCopy(string side)
        {
            GameObject copyObject = new($"{name} (Loop Copy {side})");
            Transform copyTransform = copyObject.transform;
            copyTransform.SetParent(transform.parent, false);
            copyTransform.localRotation = transform.localRotation;
            copyTransform.localScale = transform.localScale;

            if (usingBeatSlices)
            {
                if (copyChildRenderers)
                    CopyChildRenderers(copyTransform);
                else
                    CreateBeatSliceRenderers(copyTransform);
            }
            else
            {
                SpriteRenderer copyRenderer = copyObject.AddComponent<SpriteRenderer>();
                CopyRendererSettings(copyRenderer, sourceRenderer.sprite);
            }
            return copyTransform;
        }

        private void CopyChildRenderers(Transform copyParent)
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform sourceChild = transform.GetChild(index);
                CopyVisualHierarchy(sourceChild, copyParent);
            }
        }

        private void CopyVisualHierarchy(Transform source, Transform parent)
        {
            // Do not clone BeatBounce or other behaviours: their Awake/order and
            // beat anchors can differ from the original's combat-driven animation.
            GameObject copy = new($"{source.name} (Loop Visual)");
            copy.layer = source.gameObject.layer;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            SpriteRenderer renderer = source.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                SpriteRenderer visual = copy.AddComponent<SpriteRenderer>();
                visual.sprite = renderer.sprite;
                visual.color = renderer.color;
                visual.flipX = renderer.flipX;
                visual.flipY = renderer.flipY;
                visual.sharedMaterials = renderer.sharedMaterials;
                visual.sortingLayerID = renderer.sortingLayerID;
                visual.sortingOrder = renderer.sortingOrder;
                visual.maskInteraction = renderer.maskInteraction;
                visual.spriteSortPoint = renderer.spriteSortPoint;
                visual.enabled = renderer.enabled;
                BindSpriteTexture(visual, renderer, renderer.sprite);
            }
            copy.SetActive(source.gameObject.activeSelf);
            copiedTransforms.Add((source, copy.transform));
            foreach (Transform child in source)
                CopyVisualHierarchy(child, copy.transform);
        }

        private bool CreateConfiguredBeatSliceAssets()
        {
            if (beatSliceSpriteOverrides == null || beatSlicePositionOverrides == null)
                return false;

            int sliceCount = Mathf.Min(
                beatSliceSpriteOverrides.Length,
                beatSlicePositionOverrides.Length);
            if (sliceCount == 0)
                return false;

            for (int index = 0; index < sliceCount; index++)
            {
                Sprite sliceSprite = beatSliceSpriteOverrides[index];
                if (sliceSprite == null)
                    continue;

                beatSliceSprites.Add(sliceSprite);
                beatSlicePositions.Add(beatSlicePositionOverrides[index]);
                beatSliceParityIndices.Add(index);
            }

            return beatSliceSprites.Count > 0;
        }

        private bool CreateBeatSliceAssets()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return false;

            Sprite sourceSprite = sourceRenderer.sprite;
            Texture2D texture = sourceSprite.texture;
            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException exception)
            {
                Debug.LogWarning(
                    $"Cannot slice {name}; enable Read/Write on {texture.name}. {exception.Message}",
                    this);
                return false;
            }

            Rect spriteRect = sourceSprite.rect;
            int xMin = Mathf.RoundToInt(spriteRect.xMin);
            int xMax = Mathf.RoundToInt(spriteRect.xMax) - 1;
            int yMin = Mathf.RoundToInt(spriteRect.yMin);
            int yMax = Mathf.RoundToInt(spriteRect.yMax) - 1;
            bool[] activeRows = new bool[yMax - yMin + 1];

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[y * texture.width + x].a == 0)
                        continue;

                    activeRows[y - yMin] = true;
                    break;
                }
            }

            List<Vector2Int> rowBands = FindRuns(activeRows, 8);
            if (rowBands.Count == 0)
                return false;

            float pivotTextureY = spriteRect.yMin + sourceSprite.pivot.y;
            Vector2Int selectedRowBand = rowBands[0];
            float nearestPivotDistance = float.MaxValue;
            foreach (Vector2Int candidate in rowBands)
            {
                float candidateCenter = yMin + (candidate.x + candidate.y) * 0.5f;
                float pivotDistance = Mathf.Abs(candidateCenter - pivotTextureY);
                if (pivotDistance >= nearestPivotDistance)
                    continue;

                nearestPivotDistance = pivotDistance;
                selectedRowBand = candidate;
            }

            foreach (Vector2Int rowBand in rowBands)
            {
                if (rowBand != selectedRowBand)
                    continue;

                int bandYMin = yMin + rowBand.x;
                int bandYMax = yMin + rowBand.y;
                bool[] activeColumns = new bool[xMax - xMin + 1];
                for (int x = xMin; x <= xMax; x++)
                {
                    for (int y = bandYMin; y <= bandYMax; y++)
                    {
                        if (pixels[y * texture.width + x].a == 0)
                            continue;

                        activeColumns[x - xMin] = true;
                        break;
                    }
                }

                List<Vector2Int> columnRuns = FindRuns(activeColumns, 4);
                for (int sliceIndex = 0; sliceIndex < columnRuns.Count; sliceIndex++)
                {
                    int runXMin = xMin + columnRuns[sliceIndex].x;
                    int runXMax = xMin + columnRuns[sliceIndex].y;
                    int contentXMin = runXMax;
                    int contentXMax = runXMin;
                    int contentYMin = bandYMax;
                    int contentYMax = bandYMin;

                    for (int y = bandYMin; y <= bandYMax; y++)
                    {
                        for (int x = runXMin; x <= runXMax; x++)
                        {
                            if (pixels[y * texture.width + x].a == 0)
                                continue;

                            contentXMin = Mathf.Min(contentXMin, x);
                            contentXMax = Mathf.Max(contentXMax, x);
                            contentYMin = Mathf.Min(contentYMin, y);
                            contentYMax = Mathf.Max(contentYMax, y);
                        }
                    }

                    Rect sliceRect = Rect.MinMaxRect(
                        Mathf.Max(xMin, contentXMin - 1),
                        Mathf.Max(yMin, contentYMin - 1),
                        Mathf.Min(xMax + 1, contentXMax + 2),
                        Mathf.Min(yMax + 1, contentYMax + 2));
                    Sprite sliceSprite = Sprite.Create(
                        texture,
                        sliceRect,
                        new Vector2(0.5f, 0.5f),
                        sourceSprite.pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                    sliceSprite.name = $"{sourceSprite.name}_GrassSlice_{sliceIndex}";

                    float localX = (sliceRect.center.x - spriteRect.xMin - sourceSprite.pivot.x) /
                                   sourceSprite.pixelsPerUnit;
                    float localY = (sliceRect.center.y - spriteRect.yMin - sourceSprite.pivot.y) /
                                   sourceSprite.pixelsPerUnit;
                    beatSliceSprites.Add(sliceSprite);
                    beatSlicePositions.Add(new Vector3(localX, localY, 0f));
                    beatSliceParityIndices.Add(sliceIndex);
                }
            }

            return beatSliceSprites.Count > 0;
        }

        private static List<Vector2Int> FindRuns(bool[] active, int mergeGap)
        {
            List<Vector2Int> runs = new();
            int runStart = -1;
            int lastActive = -1;
            for (int index = 0; index < active.Length; index++)
            {
                if (!active[index])
                    continue;

                if (runStart < 0)
                {
                    runStart = index;
                }
                else if (index - lastActive - 1 > mergeGap)
                {
                    runs.Add(new Vector2Int(runStart, lastActive));
                    runStart = index;
                }

                lastActive = index;
            }

            if (runStart >= 0)
                runs.Add(new Vector2Int(runStart, lastActive));
            return runs;
        }

        private void CreateBeatSliceRenderers(Transform parent)
        {
            for (int index = 0; index < beatSliceSprites.Count; index++)
            {
                GameObject sliceObject = new($"Grass_{beatSliceParityIndices[index]}");
                Transform sliceTransform = sliceObject.transform;
                sliceTransform.SetParent(parent, false);
                sliceTransform.localPosition = beatSlicePositions[index];

                SpriteRenderer sliceRenderer = sliceObject.AddComponent<SpriteRenderer>();
                CopyRendererSettings(sliceRenderer, beatSliceSprites[index]);

                BeatBounce pulse = sliceObject.AddComponent<BeatBounce>();
                pulse.ConfigureGrassSlice(beatSource, beatSliceParityIndices[index]);
            }
        }

        private void CopyRendererSettings(SpriteRenderer target, Sprite sprite)
        {
            target.sprite = sprite;
            target.color = sourceRenderer.color;
            target.flipX = sourceRenderer.flipX;
            target.flipY = sourceRenderer.flipY;
            target.sharedMaterial = sourceRenderer.sharedMaterial;
            target.sortingLayerID = sourceRenderer.sortingLayerID;
            target.sortingOrder = sourceRenderer.sortingOrder;
            target.maskInteraction = sourceRenderer.maskInteraction;
            target.drawMode = sourceRenderer.drawMode;
            target.size = sourceRenderer.size;
            target.tileMode = sourceRenderer.tileMode;
            target.spriteSortPoint = sourceRenderer.spriteSortPoint;
            BindSpriteTexture(target, sourceRenderer, sprite);
        }

        private void BindSpriteTexture(SpriteRenderer target, SpriteRenderer source, Sprite sprite)
        {
            // Legacy Sprites/Default under URP does not reliably bind _MainTex on
            // newly created renderers. A valid Sprite/bounds alone can still draw
            // an invisible copy. Preserve overrides and explicitly bind its texture.
            spriteProperties ??= new MaterialPropertyBlock();
            source.GetPropertyBlock(spriteProperties);
            if (sprite != null) spriteProperties.SetTexture("_MainTex", sprite.texture);
            target.SetPropertyBlock(spriteProperties);
        }

        private void ApplyHorizontalCrop()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            originalSprite = sourceRenderer.sprite;
            Rect sourceRect = originalSprite.rect;
            int left = Mathf.Clamp(cropLeftPixels, 0, Mathf.FloorToInt(sourceRect.width) - 1);
            int right = Mathf.Clamp(
                cropRightPixels,
                0,
                Mathf.FloorToInt(sourceRect.width) - left - 1);
            if (left == 0 && right == 0)
                return;

            Rect croppedRect = new(
                sourceRect.x + left,
                sourceRect.y,
                sourceRect.width - left - right,
                sourceRect.height);
            float pivotTextureX = sourceRect.x + originalSprite.pivot.x;
            Vector2 croppedPivot = new(
                (pivotTextureX - croppedRect.x) / croppedRect.width,
                originalSprite.pivot.y / sourceRect.height);

            croppedSprite = Sprite.Create(
                originalSprite.texture,
                croppedRect,
                croppedPivot,
                originalSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            croppedSprite.name = $"{originalSprite.name}_LoopCrop";
            sourceRenderer.sprite = croppedSprite;
        }

        private void ResetPositions()
        {
            if (leftLoopCopy == null || rightLoopCopy == null || unscaledSpriteWidth <= 0f)
                return;

            float loopWidth = GetLoopWidth();
            transform.localPosition = startPosition;
            SyncCopyTransform(leftLoopCopy);
            SyncCopyTransform(rightLoopCopy);
            leftLoopCopy.localPosition = startPosition + Vector3.left * loopWidth;
            rightLoopCopy.localPosition = startPosition + Vector3.right * loopWidth;
        }

        private void SyncCopyTransform(Transform copyTransform)
        {
            copyTransform.localRotation = transform.localRotation;
            copyTransform.localScale = transform.localScale;
        }

        private float GetLoopWidth()
        {
            float pixelsPerUnit = sourceRenderer?.sprite == null
                ? 100f
                : Mathf.Max(1f, sourceRenderer.sprite.pixelsPerUnit);
            float gapUnits = loopGapPixels / pixelsPerUnit;
            float overlapUnits = seamOverlapPixels / pixelsPerUnit;
            return Mathf.Max(
                       1f / pixelsPerUnit,
                       unscaledSpriteWidth + gapUnits - overlapUnits) *
                   restScaleX;
        }

        private void OnDestroy()
        {
            if (leftLoopCopy != null)
                Destroy(leftLoopCopy.gameObject);
            if (rightLoopCopy != null)
                Destroy(rightLoopCopy.gameObject);

            if (sourceRenderer != null && originalSprite != null)
            {
                sourceRenderer.sprite = originalSprite;
                sourceRenderer.enabled = true;
            }
            if (croppedSprite != null)
                Destroy(croppedSprite);
            foreach (Sprite sliceSprite in beatSliceSprites)
            {
                if (sliceSprite != null &&
                    (beatSliceSpriteOverrides == null ||
                     System.Array.IndexOf(beatSliceSpriteOverrides, sliceSprite) < 0))
                    Destroy(sliceSprite);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            unitsPerSecond = Mathf.Max(0f, unitsPerSecond);
            cropLeftPixels = Mathf.Max(0, cropLeftPixels);
            cropRightPixels = Mathf.Max(0, cropRightPixels);
            loopGapPixels = Mathf.Max(0, loopGapPixels);
            seamOverlapPixels = Mathf.Max(0, seamOverlapPixels);
        }
#endif
    }
}
