using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class ProjectileSpriteShatter : MonoBehaviour
    {
        private const int Columns = 3;
        private const int Rows = 3;
        private const float Gravity = 4.2f;

        private SpriteRenderer pieceRenderer;
        private Sprite runtimeSprite;
        private Vector3 velocity;
        private float angularVelocity;
        private float lifetime;
        private float age;
        private Vector3 initialScale;

        public static bool Spawn(
            SpriteRenderer sourceRenderer,
            Transform sourceTransform,
            Vector3 impactWorldPosition,
            float lifetimeSeconds)
        {
            if (sourceRenderer == null || sourceTransform == null || sourceRenderer.sprite == null)
                return false;

            Sprite sourceSprite = sourceRenderer.sprite;
            Texture2D texture = sourceSprite.texture;
            Rect textureRect = sourceSprite.textureRect;
            if (texture == null || textureRect.width < Columns || textureRect.height < Rows)
                return false;

            GameObject root = new($"{sourceTransform.name}_Shatter");
            root.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            root.transform.localScale = sourceTransform.lossyScale;

            Bounds spriteBounds = sourceSprite.bounds;
            int createdPieces = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Rect fragmentRect = GetFragmentRect(textureRect, column, row);
                    if (fragmentRect.width < 1f || fragmentRect.height < 1f)
                        continue;

                    Sprite fragmentSprite = Sprite.Create(
                        texture,
                        fragmentRect,
                        new Vector2(0.5f, 0.5f),
                        sourceSprite.pixelsPerUnit);
                    fragmentSprite.name = $"{sourceSprite.name}_fragment_{column}_{row}";

                    GameObject pieceObject = new(fragmentSprite.name, typeof(SpriteRenderer));
                    pieceObject.transform.SetParent(root.transform, false);
                    float normalizedX = (column + 0.5f) / Columns;
                    float normalizedY = (row + 0.5f) / Rows;
                    pieceObject.transform.localPosition = new Vector3(
                        Mathf.Lerp(spriteBounds.min.x, spriteBounds.max.x, normalizedX),
                        Mathf.Lerp(spriteBounds.min.y, spriteBounds.max.y, normalizedY),
                        0f);

                    SpriteRenderer renderer = pieceObject.GetComponent<SpriteRenderer>();
                    renderer.sprite = fragmentSprite;
                    renderer.color = sourceRenderer.color;
                    renderer.flipX = sourceRenderer.flipX;
                    renderer.flipY = sourceRenderer.flipY;
                    renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                    renderer.sortingOrder = sourceRenderer.sortingOrder + 1 + (row + column) % 2;
                    renderer.sharedMaterial = sourceRenderer.sharedMaterial;

                    Vector3 worldCenter = pieceObject.transform.position;
                    Vector2 radial = worldCenter - impactWorldPosition;
                    if (radial.sqrMagnitude < 0.001f)
                        radial = new Vector2(column - 1f, row - 0.5f);
                    radial.Normalize();

                    float noise = Hash01(column, row);
                    Vector2 launchDirection = (radial + new Vector2(
                        Mathf.Lerp(-0.32f, 0.32f, noise),
                        Mathf.Lerp(0.35f, 0.9f, 1f - noise))).normalized;
                    float speed = Mathf.Lerp(1.45f, 2.65f, noise);

                    ProjectileSpriteShatter piece = pieceObject.AddComponent<ProjectileSpriteShatter>();
                    piece.Initialize(
                        renderer,
                        fragmentSprite,
                        launchDirection * speed,
                        Mathf.Lerp(-320f, 320f, Hash01(row + 7, column + 11)),
                        lifetimeSeconds * Mathf.Lerp(0.85f, 1.15f, noise));
                    createdPieces++;
                }
            }

            if (createdPieces == 0)
            {
                Destroy(root);
                return false;
            }

            Destroy(root, lifetimeSeconds * 1.25f + 0.1f);
            return true;
        }

        private void Initialize(
            SpriteRenderer configuredRenderer,
            Sprite configuredRuntimeSprite,
            Vector2 configuredVelocity,
            float configuredAngularVelocity,
            float configuredLifetime)
        {
            pieceRenderer = configuredRenderer;
            runtimeSprite = configuredRuntimeSprite;
            velocity = configuredVelocity;
            angularVelocity = configuredAngularVelocity;
            lifetime = Mathf.Max(0.1f, configuredLifetime);
            initialScale = transform.localScale;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            age += deltaTime;
            velocity += Vector3.down * (Gravity * deltaTime);
            transform.position += velocity * deltaTime;
            transform.Rotate(0f, 0f, angularVelocity * deltaTime, Space.Self);

            float progress = Mathf.Clamp01(age / lifetime);
            float fade = 1f - Mathf.InverseLerp(0.42f, 1f, progress);
            if (pieceRenderer != null)
            {
                Color color = pieceRenderer.color;
                color.a = fade;
                pieceRenderer.color = color;
            }
            transform.localScale = initialScale * Mathf.Lerp(1f, 0.68f, progress);

            if (progress >= 1f)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (runtimeSprite != null)
                Destroy(runtimeSprite);
        }

        private static Rect GetFragmentRect(Rect source, int column, int row)
        {
            float x0 = Mathf.Lerp(source.xMin, source.xMax, column / (float)Columns);
            float x1 = Mathf.Lerp(source.xMin, source.xMax, (column + 1f) / Columns);
            float y0 = Mathf.Lerp(source.yMin, source.yMax, row / (float)Rows);
            float y1 = Mathf.Lerp(source.yMin, source.yMax, (row + 1f) / Rows);
            return Rect.MinMaxRect(
                Mathf.Round(x0),
                Mathf.Round(y0),
                Mathf.Round(x1),
                Mathf.Round(y1));
        }

        private static float Hash01(int first, int second)
        {
            float value = Mathf.Sin(first * 12.9898f + second * 78.233f) * 43758.5453f;
            return Mathf.Repeat(value, 1f);
        }
    }
}
