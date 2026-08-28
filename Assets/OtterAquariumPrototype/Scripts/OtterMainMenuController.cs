using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RhythmHunter.OtterAquariumPrototype
{
    public sealed class OtterMainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string gameplayScenePath =
            "Assets/OtterAquariumPrototype/Scenes/OtterZooGoblinDemo1.unity";
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Floating Otter")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform otterRect;
        [SerializeField] private Image otterImage;
        [SerializeField] private Sprite[] floatingFrames;
        [SerializeField, Min(1f)] private float animationFramesPerSecond = 7f;
        [SerializeField, Min(0f)] private float driftPixelsPerSecond = 42f;
        [SerializeField, Min(0f)] private float wrapPadding = 80f;

        [Header("Corner Videos")]
        [SerializeField] private RawImage[] videoPreviews;
        [SerializeField] private VideoPlayer[] videoPlayers;

        private RenderTexture[] runtimeVideoTextures;
        private TMP_FontAsset runtimeMenuFont;
        private Font runtimeSourceFont;
        private float animationTime;
        private int currentAnimationFrame = -1;
        private bool isLoading;

        public void Configure(
            string configuredGameplayScenePath,
            Button configuredStartButton,
            Button configuredExitButton,
            RectTransform configuredCanvasRect,
            RectTransform configuredOtterRect,
            Image configuredOtterImage,
            Sprite[] configuredFloatingFrames,
            RawImage[] configuredVideoPreviews,
            VideoPlayer[] configuredVideoPlayers)
        {
            gameplayScenePath = configuredGameplayScenePath;
            startButton = configuredStartButton;
            exitButton = configuredExitButton;
            canvasRect = configuredCanvasRect;
            otterRect = configuredOtterRect;
            otterImage = configuredOtterImage;
            floatingFrames = configuredFloatingFrames;
            videoPreviews = configuredVideoPreviews;
            videoPlayers = configuredVideoPlayers;
        }

        private void Awake()
        {
            ApplyTraditionalChineseFont();

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartGame);
                startButton.onClick.AddListener(StartGame);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitDoesNothing);
                exitButton.onClick.AddListener(ExitDoesNothing);
            }

            SetOtterFrame(0);
        }

        private void Start()
        {
            StartCornerVideos();
        }

        private void Update()
        {
            AnimateOtter();
            DriftOtter();
        }

        private void OnDestroy()
        {
            if (runtimeVideoTextures != null)
            {
                foreach (RenderTexture texture in runtimeVideoTextures)
                {
                    if (texture == null)
                        continue;

                    texture.Release();
                    Destroy(texture);
                }
            }

            if (runtimeMenuFont != null)
                Destroy(runtimeMenuFont);
            if (runtimeSourceFont != null)
                Destroy(runtimeSourceFont);
        }

        public void StartGame()
        {
            if (isLoading || string.IsNullOrWhiteSpace(gameplayScenePath))
                return;

            isLoading = true;
            if (startButton != null)
                startButton.interactable = false;

            SceneManager.LoadScene(gameplayScenePath, LoadSceneMode.Single);
        }

        public void ExitDoesNothing()
        {
            // Intentionally left blank for the current prototype request.
        }

        private void AnimateOtter()
        {
            if (otterImage == null || floatingFrames == null || floatingFrames.Length == 0)
                return;

            animationTime += Time.unscaledDeltaTime;
            int frame = Mathf.FloorToInt(animationTime * animationFramesPerSecond) % floatingFrames.Length;
            SetOtterFrame(frame);
        }

        private void SetOtterFrame(int frameIndex)
        {
            if (otterImage == null || floatingFrames == null || floatingFrames.Length == 0)
                return;

            frameIndex = Mathf.Clamp(frameIndex, 0, floatingFrames.Length - 1);
            if (frameIndex == currentAnimationFrame)
                return;

            Sprite frame = floatingFrames[frameIndex];
            if (frame == null || frame.texture == null)
                return;

            currentAnimationFrame = frameIndex;
            otterImage.sprite = frame;
            otterImage.preserveAspect = false;

            // Every floating PNG uses one shared transparent canvas, but Unity's tight
            // sprite rectangles have different sizes. Mapping each crop back into that
            // source canvas keeps the otter at one constant scale across the animation.
            RectTransform imageRect = otterImage.rectTransform;
            Vector2 sourceCanvas = new(frame.texture.width, frame.texture.height);
            float canvasScale = Mathf.Min(
                otterRect.rect.width / Mathf.Max(1f, sourceCanvas.x),
                otterRect.rect.height / Mathf.Max(1f, sourceCanvas.y));
            Rect spriteRect = frame.rect;

            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = spriteRect.size * canvasScale;
            imageRect.anchoredPosition = (spriteRect.center - sourceCanvas * 0.5f) * canvasScale;
            imageRect.localScale = Vector3.one;
        }

        private void DriftOtter()
        {
            if (canvasRect == null || otterRect == null || driftPixelsPerSecond <= 0f)
                return;

            Vector2 position = otterRect.anchoredPosition;
            position.x -= driftPixelsPerSecond * Time.unscaledDeltaTime;

            float canvasHalfWidth = canvasRect.rect.width * 0.5f;
            float otterHalfWidth = otterRect.rect.width * Mathf.Abs(otterRect.localScale.x) * 0.5f;
            if (position.x + otterHalfWidth < -canvasHalfWidth - wrapPadding)
                position.x = canvasHalfWidth + otterHalfWidth + wrapPadding;

            otterRect.anchoredPosition = position;
        }

        private void StartCornerVideos()
        {
            int previewCount = videoPreviews?.Length ?? 0;
            int playerCount = videoPlayers?.Length ?? 0;
            int count = Mathf.Min(previewCount, playerCount);
            runtimeVideoTextures = new RenderTexture[count];

            for (int index = 0; index < count; index++)
            {
                RawImage preview = videoPreviews[index];
                VideoPlayer player = videoPlayers[index];
                if (preview == null || player == null || player.clip == null)
                    continue;

                int sourceWidth = player.clip.width > 0 ? (int)player.clip.width : 640;
                int sourceHeight = player.clip.height > 0 ? (int)player.clip.height : 360;
                float scale = Mathf.Min(1f, 640f / Mathf.Max(sourceWidth, sourceHeight));
                int width = Mathf.Max(16, Mathf.RoundToInt(sourceWidth * scale));
                int height = Mathf.Max(16, Mathf.RoundToInt(sourceHeight * scale));

                RenderTexture texture = new(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = $"MainMenuVideo_{index + 1}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();

                runtimeVideoTextures[index] = texture;
                preview.texture = texture;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = texture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.isLooping = true;
                player.Play();
            }
        }

        private void ApplyTraditionalChineseFont()
        {
            string[] preferredFonts =
            {
                "Microsoft JhengHei UI",
                "Microsoft JhengHei",
                "Noto Sans CJK TC",
                "PingFang TC",
                "Arial Unicode MS"
            };

            runtimeSourceFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 64);
            if (runtimeSourceFont == null)
                return;

            runtimeMenuFont = TMP_FontAsset.CreateFontAsset(runtimeSourceFont);
            if (runtimeMenuFont == null)
                return;

            runtimeMenuFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            foreach (TextMeshProUGUI label in GetComponentsInChildren<TextMeshProUGUI>(true))
                label.font = runtimeMenuFont;
        }
    }
}
