using UnityEngine;

namespace RhythmHunter.TopDownBeatCombat
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelFourDirectionPresenter : MonoBehaviour
    {
        [SerializeField] private bool trainingDummy;
        [SerializeField] private Color primaryColor = new(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField] private Color shadowColor = new(0.03f, 0.09f, 0.13f, 1f);

        private readonly Sprite[] directionalSprites = new Sprite[4];
        private readonly Texture2D[] directionalTextures = new Texture2D[4];
        private SpriteRenderer spriteRenderer;
        private int currentDirection = -1;

        public Vector2 Facing { get; private set; } = Vector2.right;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            BuildDirectionalSprites();
            SetFacing(Facing);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < directionalSprites.Length; i++)
            {
                if (directionalSprites[i] != null)
                    Destroy(directionalSprites[i]);
                if (directionalTextures[i] != null)
                    Destroy(directionalTextures[i]);
            }
        }

        public void Configure(bool isTrainingDummy, Color primary, Color secondary, Color shadow)
        {
            trainingDummy = isTrainingDummy;
            primaryColor = primary;
            secondaryColor = secondary;
            shadowColor = shadow;
        }

        public void SetFacing(Vector2 facing)
        {
            if (facing.sqrMagnitude < 0.01f)
                return;

            Facing = Cardinalize(facing);
            int direction = DirectionIndex(Facing);
            if (direction == currentDirection || spriteRenderer == null)
                return;

            currentDirection = direction;
            spriteRenderer.sprite = directionalSprites[direction];
        }

        public static Vector2 Cardinalize(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x >= 0f ? Vector2.right : Vector2.left;
            return direction.y >= 0f ? Vector2.up : Vector2.down;
        }

        private void BuildDirectionalSprites()
        {
            for (int direction = 0; direction < 4; direction++)
            {
                Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
                {
                    name = $"{name}_Direction_{direction}",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                Color clear = new(0f, 0f, 0f, 0f);
                Color[] pixels = new Color[16 * 16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = clear;

                DrawCharacter(pixels, direction);
                texture.SetPixels(pixels);
                texture.Apply(false, true);

                directionalTextures[direction] = texture;
                directionalSprites[direction] = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 16f, 16f),
                    new Vector2(0.5f, 0.32f),
                    16f,
                    0,
                    SpriteMeshType.FullRect);
                directionalSprites[direction].name = texture.name;
                directionalSprites[direction].hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void DrawCharacter(Color[] pixels, int direction)
        {
            Color primary = trainingDummy ? new Color(0.85f, 0.52f, 0.16f, 1f) : primaryColor;
            Color secondary = trainingDummy ? new Color(1f, 0.82f, 0.35f, 1f) : secondaryColor;

            Fill(pixels, 5, 2, 10, 3, shadowColor);
            Fill(pixels, 4, 4, 11, 9, primary);
            Fill(pixels, 5, 10, 10, 14, secondary);
            Fill(pixels, 4, 11, 11, 13, secondary);

            if (trainingDummy)
            {
                Fill(pixels, 7, 0, 8, 15, shadowColor);
                Fill(pixels, 2, 7, 13, 8, secondary);
                return;
            }

            switch (direction)
            {
                case 0: // down
                    Set(pixels, 6, 11, shadowColor);
                    Set(pixels, 9, 11, shadowColor);
                    Fill(pixels, 6, 8, 9, 8, secondary);
                    break;
                case 1: // right
                    Set(pixels, 10, 11, shadowColor);
                    Fill(pixels, 11, 7, 14, 8, secondary);
                    break;
                case 2: // up
                    Fill(pixels, 5, 13, 10, 14, primary);
                    Fill(pixels, 6, 5, 9, 5, secondary);
                    break;
                case 3: // left
                    Set(pixels, 5, 11, shadowColor);
                    Fill(pixels, 1, 7, 4, 8, secondary);
                    break;
            }
        }

        private static int DirectionIndex(Vector2 facing)
        {
            if (facing == Vector2.down) return 0;
            if (facing == Vector2.right) return 1;
            if (facing == Vector2.up) return 2;
            return 3;
        }

        private static void Fill(Color[] pixels, int minX, int minY, int maxX, int maxY, Color color)
        {
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                Set(pixels, x, y, color);
        }

        private static void Set(Color[] pixels, int x, int y, Color color)
        {
            if (x >= 0 && x < 16 && y >= 0 && y < 16)
                pixels[y * 16 + x] = color;
        }
    }
}
