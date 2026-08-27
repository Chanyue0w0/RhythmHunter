using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.OtterAquariumPrototype
{
    /// <summary>
    /// Beat-duration screen interference spawned by special rhythm projectiles.
    /// It is created at runtime so charts can use the effect without scene wiring.
    /// </summary>
    public sealed class RhythmScreenInterference : MonoBehaviour
    {
        public enum InterferenceKind
        {
            None,
            OrangeInk,
            SaveLoading
        }

        private static RhythmScreenInterference activeInk;
        private static RhythmScreenInterference activeSave;
        private static Sprite softCircleSprite;

        private InterferenceKind kind;
        private CanvasGroup canvasGroup;
        private TMP_Text loadingText;
        private RectTransform progressFill;
        private float elapsed;
        private float durationSeconds;

        public static void Trigger(
            InterferenceKind requestedKind,
            float requestedDurationSeconds,
            Sprite sourceIcon)
        {
            if (requestedKind == InterferenceKind.None)
                return;

            RhythmScreenInterference active = requestedKind == InterferenceKind.OrangeInk
                ? activeInk
                : activeSave;
            if (active != null)
            {
                active.Refresh(requestedDurationSeconds, sourceIcon);
                return;
            }

            GameObject root = new($"RhythmScreenInterference_{requestedKind}");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>().enabled = false;

            RhythmScreenInterference interference = root.AddComponent<RhythmScreenInterference>();
            interference.kind = requestedKind;
            interference.durationSeconds = Mathf.Max(0.1f, requestedDurationSeconds);
            interference.canvasGroup = root.AddComponent<CanvasGroup>();
            interference.canvasGroup.alpha = 0f;
            interference.canvasGroup.blocksRaycasts = false;
            interference.canvasGroup.interactable = false;
            if (requestedKind == InterferenceKind.OrangeInk)
            {
                activeInk = interference;
                interference.BuildOrangeInk();
            }
            else
            {
                activeSave = interference;
                interference.BuildSaveLoading(sourceIcon);
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, durationSeconds));
            float fadeIn = Mathf.Clamp01(normalized / 0.09f);
            float fadeOut = Mathf.Clamp01((1f - normalized) / 0.22f);
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));

            if (kind == InterferenceKind.OrangeInk)
            {
                float pulse = 1f + Mathf.Sin(elapsed * 7f) * 0.012f;
                transform.localScale = new Vector3(pulse, pulse, 1f);
            }
            else
            {
                UpdateSaveLoading(normalized);
            }

            if (normalized >= 1f)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (activeInk == this)
                activeInk = null;
            if (activeSave == this)
                activeSave = null;
        }

        private void Refresh(float requestedDurationSeconds, Sprite sourceIcon)
        {
            elapsed = 0f;
            durationSeconds = Mathf.Max(0.1f, requestedDurationSeconds);
            if (kind != InterferenceKind.SaveLoading || sourceIcon == null)
                return;

            Transform icon = transform.Find("SavePanel/SaveIcon");
            if (icon != null && icon.TryGetComponent(out Image image))
                image.sprite = sourceIcon;
        }

        private void BuildOrangeInk()
        {
            RectTransform inkRoot = CreateRect("OrangeInk", transform);
            inkRoot.anchorMin = new Vector2(0.47f, -0.04f);
            inkRoot.anchorMax = new Vector2(1.04f, 1.04f);
            inkRoot.offsetMin = Vector2.zero;
            inkRoot.offsetMax = Vector2.zero;

            Image wash = inkRoot.gameObject.AddComponent<Image>();
            wash.color = new Color(0.83f, 0.25f, 0.025f, 0.24f);
            wash.raycastTarget = false;

            Sprite circle = GetSoftCircleSprite();
            Color[] palette =
            {
                new(0.93f, 0.34f, 0.035f, 0.72f),
                new(0.72f, 0.18f, 0.018f, 0.62f),
                new(0.98f, 0.47f, 0.055f, 0.48f),
                new(0.42f, 0.09f, 0.01f, 0.42f)
            };

            // Fixed values keep the splatter art stable and make screenshots reproducible.
            Vector2[] positions =
            {
                new(0.70f, 0.58f), new(0.88f, 0.72f), new(0.53f, 0.35f),
                new(0.79f, 0.28f), new(0.98f, 0.45f), new(0.63f, 0.88f),
                new(0.46f, 0.78f), new(0.91f, 0.11f), new(0.58f, 0.08f),
                new(0.83f, 0.94f), new(0.37f, 0.53f), new(1.01f, 0.82f)
            };
            Vector2[] sizes =
            {
                new(530f, 480f), new(420f, 360f), new(390f, 510f),
                new(490f, 330f), new(370f, 540f), new(310f, 420f),
                new(250f, 310f), new(280f, 230f), new(220f, 300f),
                new(260f, 310f), new(190f, 240f), new(220f, 280f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                RectTransform blot = CreateRect($"InkBlot_{i:00}", inkRoot);
                blot.anchorMin = positions[i];
                blot.anchorMax = positions[i];
                blot.anchoredPosition = Vector2.zero;
                blot.sizeDelta = sizes[i];
                blot.localRotation = Quaternion.Euler(0f, 0f, i * 29f % 71f - 35f);
                Image image = blot.gameObject.AddComponent<Image>();
                image.sprite = circle;
                image.color = palette[i % palette.Length];
                image.raycastTarget = false;
            }

            for (int i = 0; i < 7; i++)
            {
                RectTransform drip = CreateRect($"InkDrip_{i:00}", inkRoot);
                float x = 0.42f + i * 0.095f;
                drip.anchorMin = new Vector2(x, 0.42f + (i % 3) * 0.16f);
                drip.anchorMax = drip.anchorMin;
                drip.pivot = new Vector2(0.5f, 1f);
                drip.sizeDelta = new Vector2(18f + (i % 2) * 13f, 150f + (i % 4) * 62f);
                Image image = drip.gameObject.AddComponent<Image>();
                image.color = palette[(i + 1) % palette.Length];
                image.raycastTarget = false;
            }

            for (int i = 0; i < 14; i++)
            {
                RectTransform fleck = CreateRect($"InkFleck_{i:00}", inkRoot);
                float x = 0.27f + ((i * 37) % 73) / 100f;
                float y = 0.04f + ((i * 53) % 91) / 100f;
                float size = 18f + (i * 17 % 52);
                fleck.anchorMin = new Vector2(x, y);
                fleck.anchorMax = fleck.anchorMin;
                fleck.sizeDelta = new Vector2(size, size * (0.65f + (i % 3) * 0.22f));
                Image image = fleck.gameObject.AddComponent<Image>();
                image.sprite = circle;
                image.color = palette[(i + 2) % palette.Length];
                image.raycastTarget = false;
            }
        }

        private void BuildSaveLoading(Sprite sourceIcon)
        {
            RectTransform dimmer = CreateRect("SaveDimmer", transform);
            Stretch(dimmer);
            Image dimmerImage = dimmer.gameObject.AddComponent<Image>();
            dimmerImage.color = new Color(0.015f, 0.025f, 0.04f, 0.42f);
            dimmerImage.raycastTarget = false;

            RectTransform panel = CreateRect("SavePanel", transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 260f);
            panel.anchoredPosition = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.045f, 0.075f, 0.96f);
            panelImage.raycastTarget = false;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.26f, 0.9f, 1f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);

            RectTransform icon = CreateRect("SaveIcon", panel);
            icon.anchorMin = icon.anchorMax = new Vector2(0.16f, 0.57f);
            icon.sizeDelta = new Vector2(145f, 145f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = sourceIcon;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            RectTransform label = CreateRect("LoadingLabel", panel);
            label.anchorMin = new Vector2(0.31f, 0.30f);
            label.anchorMax = new Vector2(0.96f, 0.88f);
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            loadingText = label.gameObject.AddComponent<TextMeshProUGUI>();
            loadingText.text = "SAVE...\nLOADING";
            loadingText.fontSize = 48f;
            loadingText.fontStyle = FontStyles.Bold;
            loadingText.alignment = TextAlignmentOptions.MidlineLeft;
            loadingText.color = new Color(0.84f, 0.98f, 1f, 1f);
            loadingText.raycastTarget = false;

            RectTransform bar = CreateRect("ProgressBar", panel);
            bar.anchorMin = new Vector2(0.31f, 0.17f);
            bar.anchorMax = new Vector2(0.94f, 0.25f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
            Image barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0.08f, 0.15f, 0.20f, 1f);
            barImage.raycastTarget = false;

            progressFill = CreateRect("ProgressFill", bar);
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
            Image fillImage = progressFill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.18f, 0.86f, 1f, 1f);
            fillImage.raycastTarget = false;
        }

        private void UpdateSaveLoading(float normalized)
        {
            int dots = 1 + Mathf.FloorToInt(elapsed * 4f) % 3;
            if (loadingText != null)
                loadingText.text = $"SAVE{new string('.', dots)}\nLOADING";
            if (progressFill != null)
                progressFill.anchorMax = new Vector2(Mathf.Lerp(0.05f, 0.94f, normalized), 1f);
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite GetSoftCircleSprite()
        {
            if (softCircleSprite != null)
                return softCircleSprite;

            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeInkSoftCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.49f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float angleNoise = Mathf.Sin(Mathf.Atan2(delta.y, delta.x) * 9f + 0.8f) * 0.055f;
                    float edge = 1f - delta.magnitude / (radius * (1f + angleNoise));
                    byte alpha = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 6f)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            softCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            softCircleSprite.name = "RuntimeInkSoftCircleSprite";
            softCircleSprite.hideFlags = HideFlags.HideAndDontSave;
            return softCircleSprite;
        }
    }
}
