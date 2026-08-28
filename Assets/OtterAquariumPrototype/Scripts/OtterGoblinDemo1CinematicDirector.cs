using System.Collections;
using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace RhythmHunter.OtterAquariumPrototype
{
    /// <summary>
    /// Optional wrapper around Demo1: opening cinematic -> rhythm battle -> optional ending Timeline.
    /// When disabled, the existing music/chart flow starts immediately and remains untouched.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class OtterGoblinDemo1CinematicDirector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class DialogueLine
        {
            [SerializeField] private string speaker = "海獺俠";
            [SerializeField, TextArea(2, 5)] private string content = string.Empty;
            [SerializeField, Min(0f)] private float autoAdvanceSeconds = 3f;
            [SerializeField] private bool allowInputAdvance = true;

            public DialogueLine()
            {
            }

            public DialogueLine(
                string configuredSpeaker,
                string configuredContent,
                float configuredAutoAdvanceSeconds,
                bool configuredAllowInputAdvance = true)
            {
                speaker = configuredSpeaker;
                content = configuredContent;
                autoAdvanceSeconds = configuredAutoAdvanceSeconds;
                allowInputAdvance = configuredAllowInputAdvance;
            }

            public string Speaker => speaker;
            public string Content => content;
            public float AutoAdvanceSeconds => autoAdvanceSeconds;
            public bool AllowInputAdvance => allowInputAdvance;

            public void Sanitize()
            {
                autoAdvanceSeconds = Mathf.Max(0f, autoAdvanceSeconds);
                if (autoAdvanceSeconds <= 0f && !allowInputAdvance)
                    allowInputAdvance = true;
            }
        }

        [System.Serializable]
        public sealed class FutureLevelCard
        {
            [SerializeField] private Sprite image;
            [SerializeField] private string title = "未來關卡";

            public FutureLevelCard()
            {
            }

            public FutureLevelCard(string configuredTitle)
            {
                title = configuredTitle;
            }

            public Sprite Image => image;
            public string Title => title;

            public void SetImage(Sprite configuredImage)
            {
                image = configuredImage;
            }
        }

        [Header("Master Switch")]
        [Tooltip("Disable before Play to bypass every cinematic and test combat immediately.")]
        [SerializeField] private bool enableCinematics = true;

        [Header("Sequence Slots (may be empty)")]
        [SerializeField] private bool playBuiltInOpening = true;
        [SerializeField] private bool playBuiltInEnding = true;
        [SerializeField] private bool skipRhythmTimelineIntroAfterCinematic = true;
        [SerializeField] private PlayableDirector additionalOpeningTimeline;
        [SerializeField] private PlayableDirector endingTimeline;

        [Header("Built-in Ending")]
        [SerializeField] private Sprite heroicSpiritSprite;
        [SerializeField] private Vector3 heroicSpiritWorldPosition = new(-2.45f, 0.25f, -0.5f);
        [SerializeField, Min(0.01f)] private float heroicSpiritScale = 1.05f;
        [SerializeField, Min(0f)] private float heroicSpiritBobAmplitude = 0.12f;
        [SerializeField, Min(0.01f)] private float heroicSpiritBobCyclesPerSecond = 0.55f;
        [SerializeField, Min(0.01f)] private float flashInSeconds = 0.08f;
        [SerializeField, Min(0f)] private float flashHoldSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float flashFadeSeconds = 0.75f;

        [Header("Opening Dialogue (editable and reorderable)")]
        [SerializeField] private DialogueLine[] openingDialogue =
        {
            new("斧頭哥布林", "既然被你發現了，那就只好解決你了", 2f)
        };

        [Header("Ending Dialogue - Before Hero Appears")]
        [SerializeField] private DialogueLine[] preHeroEndingDialogue =
        {
            new("？？？", "少年……", 2.2f),
            new("？？？", "你知道完美的身材是怎麼來的嗎？", 3.2f),
            new("海獺", "……", 2f),
            new("？？？", "是責任感……", 2.8f)
        };

        [Header("Ending Dialogue - Hero Appears")]
        [SerializeField] private DialogueLine[] heroRevealDialogue =
        {
            new("海獺俠", "世界的和平……就在你的掌掌之中……", 3.5f),
            new("海獺俠", "我知道你一定很好奇我是誰……", 3.5f)
        };

        [Header("Ending Dialogue - After Otter Leaves")]
        [SerializeField] private DialogueLine[] postOtterExitDialogue =
        {
            new("海獺俠", "唉，現在的年輕人……", 3f)
        };

        [Header("Ending Otter Exit")]
        [SerializeField, Min(0.1f)] private float endingOtterSwimSeconds = 2.1f;
        [SerializeField, Min(0f)] private float endingOtterSwimDistance = 7.5f;
        [SerializeField, Min(0f)] private float endingOtterSwimArcHeight = 0.22f;

        [Header("Demo End - Future Level Panel")]
        [SerializeField] private bool showFutureLevelPanel = true;
        [SerializeField] private string futureLevelPanelTitle = "DEMO COMPLETE  •  未來關卡預覽";
        [SerializeField] private FutureLevelCard[] futureLevelCards =
        {
            new("核能危機"),
            new("健壯海獺"),
            new("撲克臉"),
            new("發大財")
        };

        [Header("Dependencies")]
        [SerializeField] private OtterGoblinDemo1Runner runner;
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private OtterGoblinDemo1Input combatInput;
        [SerializeField] private OtterGoblinDemo1Presenter presenter;
        [SerializeField] private Camera sceneCamera;

        [Header("Opening Timing")]
        [SerializeField, Min(0.1f)] private float otterSwimSeconds = 2.2f;
        [SerializeField, Min(0.1f)] private float cameraPanSeconds = 0.85f;
        [SerializeField, Min(0f)] private float dialogueInputLockSeconds = 0.15f;
        [SerializeField, Min(0f)] private float goblinRaisedAxeHoldSeconds = 0.38f;
        [SerializeField, Range(0.05f, 1f)] private float axeSlowMotionScale = 0.28f;
        [SerializeField, Min(0.1f)] private float axeTravelGameSeconds = 0.8f;
        [SerializeField, Min(1f)] private float cinematicCrackingTimingScale = 2.5f;
        [SerializeField, Min(0f)] private float cinematicCatchHoldBeats = 0.5f;

        [Header("Camera Framing")]
        [SerializeField, Min(0.5f)] private float otterFocusSize = 3.1f;
        [SerializeField, Min(0.5f)] private float goblinFocusSize = 2.8f;
        [SerializeField, Min(0.5f)] private float axeFocusSize = 2.15f;

        private Vector3 defaultCameraPosition;
        private float defaultCameraSize;
        private float originalTimeScale = 1f;
        private bool openingRunning;
        private bool endingRunning;
        private bool endingSequenceCompleted;
        private Canvas dialogueCanvas;
        private Canvas endingFlashCanvas;
        private Canvas demoEndCanvas;
        private Image endingFlashImage;
        private Transform heroicSpiritRoot;
        private Vector3 heroicSpiritBasePosition;
        private GameObject spiritualWorldBackdrop;
        private Texture2D spiritualWorldTexture;
        private Sprite spiritualWorldSprite;
        private OtterCombatAnimator subscribedOtterAnimator;

        public bool EnableCinematics => enableCinematics;
        public bool HasOpeningContent => playBuiltInOpening || additionalOpeningTimeline != null;
        public bool HasEndingContent => (playBuiltInEnding && heroicSpiritSprite != null) || endingTimeline != null;
        public int OpeningDialogueCount => openingDialogue?.Length ?? 0;
        public int EndingDialogueCount => (preHeroEndingDialogue?.Length ?? 0)
            + (heroRevealDialogue?.Length ?? 0)
            + (postOtterExitDialogue?.Length ?? 0);
        public int FutureLevelCardCount => futureLevelCards?.Length ?? 0;

        public void Configure(
            OtterGoblinDemo1Runner configuredRunner,
            FmodBeatClock configuredBeatClock,
            OtterGoblinDemo1Input configuredInput,
            OtterGoblinDemo1Presenter configuredPresenter,
            Camera configuredCamera,
            Sprite configuredHeroicSpiritSprite = null,
            Sprite[] configuredFutureLevelImages = null)
        {
            runner = configuredRunner;
            beatClock = configuredBeatClock;
            combatInput = configuredInput;
            presenter = configuredPresenter;
            sceneCamera = configuredCamera;
            if (configuredHeroicSpiritSprite != null)
                heroicSpiritSprite = configuredHeroicSpiritSprite;
            ConfigureFutureLevelImages(configuredFutureLevelImages);
        }

        private void Awake()
        {
            ResolveReferences();
            if (!enableCinematics)
                return;

            runner?.SetPlaybackGated(true);
            combatInput?.SetDefendEnabled(false);
            presenter?.SetCinematicMode(true);
            presenter?.OtterAnimator?.BeginCinematicControl();
        }

        private void OnEnable()
        {
            if (runner != null)
                runner.BattleWon += OnBattleWon;
            SubscribeToCrackImpact();
        }

        private void OnDisable()
        {
            if (runner != null)
                runner.BattleWon -= OnBattleWon;
            UnsubscribeFromCrackImpact();
            RestoreTimeScale();
            DestroyDialogue();
            DestroyEndingFlash();
            DestroyHeroicSpirit();
            DestroySpiritualWorldBackdrop();
            DestroyDemoEndPanel();
        }

        private IEnumerator Start()
        {
            ResolveReferences();
            CaptureDefaultCamera();
            if (!enableCinematics)
                yield break;

            openingRunning = true;
            if (playBuiltInOpening)
                yield return RunBuiltInOpening();
            if (additionalOpeningTimeline != null)
                yield return PlayAndWait(additionalOpeningTimeline);

            yield return RestoreBattleFraming();
            presenter?.ShowCinematicEnemyIdle();
            presenter?.OtterAnimator?.EndCinematicControl(skipRhythmTimelineIntroAfterCinematic);
            presenter?.SetCinematicMode(false);

            float startDelay = runner != null && runner.LevelData != null
                ? runner.LevelData.MusicStartDelaySeconds
                : 0f;
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            runner?.SetPlaybackGated(false);
            beatClock?.StartMusic();
            combatInput?.SetDefendEnabled(true);
            openingRunning = false;
        }

        private IEnumerator RunBuiltInOpening()
        {
            OtterCombatAnimator otterAnimator = presenter != null ? presenter.OtterAnimator : null;
            Transform otter = presenter != null ? presenter.OtterRoot : null;
            Transform enemy = presenter != null ? presenter.EnemyRoot : null;
            if (sceneCamera == null || otterAnimator == null || otter == null || enemy == null)
                yield break;

            Vector3 otterFocus = CameraPositionFor(otter.position + new Vector3(0.35f, 0.65f, 0f));
            sceneCamera.transform.position = otterFocus + Vector3.right * 0.2f;
            sceneCamera.orthographicSize = otterFocusSize;
            float elapsed = 0f;
            while (elapsed < otterSwimSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / otterSwimSeconds);
                otterAnimator.SetCinematicSwimming(progress, elapsed);
                sceneCamera.transform.position = Vector3.Lerp(
                    otterFocus + Vector3.right * 0.2f,
                    otterFocus,
                    Smooth(progress));
                yield return null;
            }
            otterAnimator.SetCinematicIdle();

            Vector3 goblinFocus = CameraPositionFor(enemy.position + new Vector3(0f, 0.7f, 0f));
            yield return MoveCamera(goblinFocus, goblinFocusSize, cameraPanSeconds);
            yield return PlayDialogueSequence(openingDialogue);

            presenter.ShowCinematicEnemyRaisedAxe();
            if (goblinRaisedAxeHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(goblinRaisedAxeHoldSeconds);
            presenter.ShowCinematicEnemyThrow();
            presenter.PlayCinematicAttackSound();
            yield return PlaySlowMotionAxe(enemy, otterAnimator);

            float waitLimit = 2f;
            while (otterAnimator.IsCracking && waitLimit > 0f)
            {
                waitLimit -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator PlaySlowMotionAxe(Transform enemy, OtterCombatAnimator otterAnimator)
        {
            GameObject prefab = presenter != null ? presenter.DefaultProjectilePrefab : null;
            if (prefab == null)
                yield break;

            float enemyScale = Mathf.Max(0.01f, Mathf.Abs(enemy.lossyScale.x));
            Vector3 start = enemy.position + new Vector3(1.15f * enemyScale, 0.62f * enemyScale, -0.45f);
            Vector3 end = otterAnimator.CatchTargetTransform.TransformPoint(otterAnimator.CatchAnchorLocal);
            GameObject instance = Instantiate(prefab, start, Quaternion.identity);
            instance.name = "CinematicFlyingAxe";
            RhythmTimelineProjectile projectile = instance.GetComponent<RhythmTimelineProjectile>();
            SpriteRenderer projectileRenderer = instance.GetComponent<SpriteRenderer>();
            if (projectile == null || projectileRenderer == null)
            {
                Destroy(instance);
                yield break;
            }

            projectileRenderer.enabled = true;
            originalTimeScale = Time.timeScale;
            Time.timeScale = axeSlowMotionScale;
            float elapsed = 0f;
            while (elapsed < axeTravelGameSeconds)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / axeTravelGameSeconds);
                float eased = Smooth(progress);
                Vector3 position = Vector3.LerpUnclamped(start, end, eased);
                position.y += Mathf.Sin(progress * Mathf.PI) * 0.7f;
                instance.transform.position = position;
                if (projectile.RotateDuringFlight)
                    instance.transform.rotation = Quaternion.Euler(0f, 0f, -720f * progress);

                Vector3 focus = CameraPositionFor(position + Vector3.up * 0.2f);
                sceneCamera.transform.position = focus;
                sceneCamera.orthographicSize = Mathf.Lerp(goblinFocusSize, axeFocusSize, Smooth(progress * 2f));
                yield return null;
            }

            RestoreTimeScale();
            instance.transform.position = end;
            sceneCamera.transform.position = CameraPositionFor(end + Vector3.up * 0.15f);
            presenter?.PlayCinematicCatchSound();
            otterAnimator.PlayCinematicCracking(
                projectile,
                cinematicCrackingTimingScale,
                cinematicCatchHoldBeats);
        }

        private IEnumerator PlayDialogueSequence(DialogueLine[] sequence)
        {
            if (sequence == null)
                yield break;

            foreach (DialogueLine line in sequence)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.Content))
                    continue;
                yield return ShowDialogue(line);
            }
        }

        private IEnumerator ShowDialogue(DialogueLine dialogueLine)
        {
            dialogueLine.Sanitize();
            CreateDialogue(dialogueLine.Speaker, dialogueLine.Content);
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                bool reachedAutoAdvance = dialogueLine.AutoAdvanceSeconds > 0f
                    && elapsed >= dialogueLine.AutoAdvanceSeconds;
                bool receivedInput = dialogueLine.AllowInputAdvance
                    && elapsed >= dialogueInputLockSeconds
                    && WasAdvancePressed();
                if (reachedAutoAdvance || receivedInput)
                    break;
                yield return null;
            }
            DestroyDialogue();
        }

        private void CreateDialogue(string speaker, string line)
        {
            DestroyDialogue();
            GameObject root = new("Demo1OpeningDialogue", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            dialogueCanvas = root.GetComponent<Canvas>();
            dialogueCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dialogueCanvas.sortingOrder = 25000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.GetComponent<GraphicRaycaster>().enabled = false;

            RectTransform panel = CreateRect("DialoguePanel", root.transform);
            panel.anchorMin = new Vector2(0.12f, 0.08f);
            panel.anchorMax = new Vector2(0.88f, 0.32f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.035f, 0.045f, 0.94f);
            panelImage.raycastTarget = false;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.46f, 0.12f, 0.95f);
            outline.effectDistance = new Vector2(4f, -4f);

            Font font = CreateDialogueFont();
            Text speakerText = CreateText("Speaker", panel, font, 36, FontStyle.Bold);
            RectTransform speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0.05f, 0.62f);
            speakerRect.anchorMax = new Vector2(0.95f, 0.92f);
            speakerRect.offsetMin = Vector2.zero;
            speakerRect.offsetMax = Vector2.zero;
            speakerText.text = speaker;
            speakerText.color = speaker == "海獺俠"
                ? new Color(0.35f, 0.88f, 1f, 1f)
                : new Color(1f, 0.58f, 0.23f, 1f);

            Text lineText = CreateText("Line", panel, font, 43, FontStyle.Normal);
            RectTransform lineRect = lineText.rectTransform;
            lineRect.anchorMin = new Vector2(0.05f, 0.17f);
            lineRect.anchorMax = new Vector2(0.95f, 0.66f);
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = Vector2.zero;
            lineText.text = line;
            lineText.color = Color.white;

            Text hint = CreateText("AdvanceHint", panel, font, 22, FontStyle.Normal);
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.55f, 0.02f);
            hintRect.anchorMax = new Vector2(0.95f, 0.2f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            hint.alignment = TextAnchor.MiddleRight;
            hint.text = "任意鍵繼續";
            hint.color = new Color(0.72f, 0.8f, 0.82f, 1f);
        }

        private IEnumerator RestoreBattleFraming()
        {
            if (sceneCamera == null)
                yield break;
            yield return MoveCamera(defaultCameraPosition, defaultCameraSize, cameraPanSeconds);
        }

        private IEnumerator MoveCamera(Vector3 targetPosition, float targetSize, float duration)
        {
            Vector3 startPosition = sceneCamera.transform.position;
            float startSize = sceneCamera.orthographicSize;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth(Mathf.Clamp01(elapsed / duration));
                sceneCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                sceneCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, progress);
                yield return null;
            }
            sceneCamera.transform.position = targetPosition;
            sceneCamera.orthographicSize = targetSize;
        }

        private IEnumerator PlayAndWait(PlayableDirector timeline)
        {
            if (timeline == null)
                yield break;
            timeline.time = 0.0;
            timeline.Play();
            while (timeline != null && timeline.state == PlayState.Playing)
                yield return null;
        }

        private void OnBattleWon(OtterGoblinDemo1Runner.CombatSummary summary)
        {
            if (!enableCinematics || endingRunning || endingSequenceCompleted || endingTimeline == null)
                return;
            StartCoroutine(RunEndingTimelineOnly());
        }

        private void OnCrackImpact(RhythmTimelineProjectile projectile, Vector3 impactWorldPosition)
        {
            if (!enableCinematics || !playBuiltInEnding || endingRunning || endingSequenceCompleted
                || projectile == null || !projectile.TriggersHeroEnding || heroicSpiritSprite == null)
            {
                return;
            }

            bool isFinalPhrase = runner != null
                && runner.LevelData != null
                && runner.CurrentPhraseNumber >= runner.LevelData.Phrases.Count;
            if (!isFinalPhrase)
                return;

            StartCoroutine(RunBuiltInEnding());
        }

        private IEnumerator RunBuiltInEnding()
        {
            endingRunning = true;
            combatInput?.SetDefendEnabled(false);
            presenter?.SetCinematicMode(true);

            CreateEndingFlash();
            yield return FadeEndingFlash(0f, 1f, flashInSeconds);
            if (flashHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(flashHoldSeconds);

            if (presenter != null && presenter.EnemyRoot != null)
                presenter.EnemyRoot.gameObject.SetActive(false);
            CreateSpiritualWorldBackdrop();
            yield return FadeEndingFlash(1f, 0f, flashFadeSeconds);
            DestroyEndingFlash();

            // The mysterious voice speaks while the otter is alone in the white world.
            yield return PlayDialogueSequence(preHeroEndingDialogue);

            CreateHeroicSpirit();
            yield return PlayDialogueSequence(heroRevealDialogue);

            yield return SwimOtterOutToRight();
            yield return PlayDialogueSequence(postOtterExitDialogue);

            if (endingTimeline != null)
                yield return PlayAndWait(endingTimeline);

            if (showFutureLevelPanel)
                CreateDemoEndPanel();

            endingSequenceCompleted = true;
            endingRunning = false;
        }

        private IEnumerator SwimOtterOutToRight()
        {
            Transform otter = presenter != null ? presenter.OtterRoot : null;
            OtterCombatAnimator animator = presenter != null ? presenter.OtterAnimator : null;
            if (otter == null)
                yield break;

            animator?.BeginCinematicControl();
            Vector3 start = otter.position;
            Vector3 target = start + Vector3.right * endingOtterSwimDistance;
            float elapsed = 0f;
            while (elapsed < endingOtterSwimSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float rawProgress = Mathf.Clamp01(elapsed / endingOtterSwimSeconds);
                float progress = Smooth(rawProgress);
                Vector3 position = Vector3.Lerp(start, target, progress);
                position.y += Mathf.Sin(rawProgress * Mathf.PI) * endingOtterSwimArcHeight;
                otter.position = position;
                animator?.SetCinematicSwimmingPose(elapsed, true);
                yield return null;
            }

            otter.position = target;
            otter.gameObject.SetActive(false);
        }

        private IEnumerator RunEndingTimelineOnly()
        {
            endingRunning = true;
            combatInput?.SetDefendEnabled(false);
            yield return PlayAndWait(endingTimeline);
            endingSequenceCompleted = true;
            endingRunning = false;
        }

        private void CreateEndingFlash()
        {
            DestroyEndingFlash();
            GameObject root = new("Demo1EndingWhiteFlash", typeof(Canvas), typeof(CanvasScaler));
            endingFlashCanvas = root.GetComponent<Canvas>();
            endingFlashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            endingFlashCanvas.sortingOrder = 32000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            RectTransform flashRect = CreateRect("Full Screen White", root.transform);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            endingFlashImage = flashRect.gameObject.AddComponent<Image>();
            endingFlashImage.color = new Color(1f, 1f, 1f, 0f);
            endingFlashImage.raycastTarget = false;
        }

        private IEnumerator FadeEndingFlash(float fromAlpha, float toAlpha, float duration)
        {
            if (endingFlashImage == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth(Mathf.Clamp01(elapsed / duration));
                endingFlashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }
            endingFlashImage.color = new Color(1f, 1f, 1f, toAlpha);
        }

        private void CreateHeroicSpirit()
        {
            DestroyHeroicSpirit();
            if (heroicSpiritSprite == null)
                return;

            GameObject root = new("Ending OtterHero");
            heroicSpiritRoot = root.transform;
            heroicSpiritBasePosition = heroicSpiritWorldPosition;
            heroicSpiritRoot.position = heroicSpiritBasePosition;
            heroicSpiritRoot.localScale = Vector3.one * heroicSpiritScale;

            GameObject visual = new("OtterHero Sprite", typeof(SpriteRenderer));
            visual.transform.SetParent(heroicSpiritRoot, false);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = heroicSpiritSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 120;
            visual.transform.localPosition = -heroicSpiritSprite.bounds.center;
        }

        private void CreateSpiritualWorldBackdrop()
        {
            DestroySpiritualWorldBackdrop();

            spiritualWorldTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Demo1 Spiritual World White"
            };
            spiritualWorldTexture.SetPixel(0, 0, Color.white);
            spiritualWorldTexture.Apply(false, true);
            spiritualWorldSprite = Sprite.Create(
                spiritualWorldTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            spiritualWorldSprite.name = "Demo1 Spiritual World White";

            spiritualWorldBackdrop = new GameObject("Pure White Spiritual World", typeof(SpriteRenderer));
            SpriteRenderer renderer = spiritualWorldBackdrop.GetComponent<SpriteRenderer>();
            renderer.sprite = spiritualWorldSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 10;
            UpdateSpiritualWorldBackdrop();
        }

        private void UpdateSpiritualWorldBackdrop()
        {
            if (spiritualWorldBackdrop == null || sceneCamera == null)
                return;

            float height = sceneCamera.orthographic
                ? sceneCamera.orthographicSize * 2f + 2f
                : 30f;
            float width = height * Mathf.Max(1f, sceneCamera.aspect) + 2f;
            Vector3 cameraPosition = sceneCamera.transform.position;
            spiritualWorldBackdrop.transform.position = new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                0f);
            spiritualWorldBackdrop.transform.localScale = new Vector3(width, height, 1f);
        }

        private void DestroySpiritualWorldBackdrop()
        {
            if (spiritualWorldBackdrop != null)
                Destroy(spiritualWorldBackdrop);
            if (spiritualWorldSprite != null)
                Destroy(spiritualWorldSprite);
            if (spiritualWorldTexture != null)
                Destroy(spiritualWorldTexture);
            spiritualWorldBackdrop = null;
            spiritualWorldSprite = null;
            spiritualWorldTexture = null;
        }

        private void DestroyEndingFlash()
        {
            if (endingFlashCanvas != null)
                Destroy(endingFlashCanvas.gameObject);
            endingFlashCanvas = null;
            endingFlashImage = null;
        }

        private void DestroyHeroicSpirit()
        {
            if (heroicSpiritRoot != null)
                Destroy(heroicSpiritRoot.gameObject);
            heroicSpiritRoot = null;
        }

        private void CreateDemoEndPanel()
        {
            DestroyDemoEndPanel();

            GameObject root = new("Demo End Future Levels", typeof(Canvas), typeof(CanvasScaler));
            demoEndCanvas = root.GetComponent<Canvas>();
            demoEndCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            demoEndCanvas.sortingOrder = 31500;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            RectTransform dimmer = CreateRect("Backdrop", root.transform);
            Stretch(dimmer, Vector2.zero, Vector2.one);
            Image dimmerImage = dimmer.gameObject.AddComponent<Image>();
            dimmerImage.color = new Color(0.018f, 0.035f, 0.055f, 0.96f);
            dimmerImage.raycastTarget = false;

            RectTransform panel = CreateRect("Future Level Panel", root.transform);
            panel.anchorMin = new Vector2(0.08f, 0.06f);
            panel.anchorMax = new Vector2(0.92f, 0.94f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.075f, 0.11f, 1f);
            panelImage.raycastTarget = false;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.78f, 0.92f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);

            Font font = CreateDialogueFont();
            Text heading = CreateText("Panel Title", panel, font, 46, FontStyle.Bold);
            heading.text = futureLevelPanelTitle;
            heading.alignment = TextAnchor.MiddleCenter;
            heading.color = Color.white;
            RectTransform headingRect = heading.rectTransform;
            Stretch(headingRect, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f));

            for (int index = 0; index < 4; index++)
            {
                FutureLevelCard data = futureLevelCards != null && index < futureLevelCards.Length
                    ? futureLevelCards[index]
                    : null;
                CreateFutureLevelCard(panel, font, data, index);
            }
        }

        private static void CreateFutureLevelCard(
            RectTransform parent,
            Font font,
            FutureLevelCard data,
            int index)
        {
            int column = index % 2;
            int row = index / 2;
            const float gap = 0.025f;
            float minX = column == 0 ? 0.045f : 0.5f + gap * 0.5f;
            float maxX = column == 0 ? 0.5f - gap * 0.5f : 0.955f;
            float maxY = row == 0 ? 0.84f : 0.455f;
            float minY = row == 0 ? 0.475f : 0.09f;

            RectTransform card = CreateRect($"Future Level #{index + 1}", parent);
            Stretch(card, new Vector2(minX, minY), new Vector2(maxX, maxY));
            Image cardBackground = card.gameObject.AddComponent<Image>();
            cardBackground.color = new Color(0.085f, 0.13f, 0.17f, 1f);
            cardBackground.raycastTarget = false;

            RectTransform pictureRect = CreateRect("Image", card);
            Stretch(pictureRect, new Vector2(0.025f, 0.21f), new Vector2(0.975f, 0.975f));
            Image picture = pictureRect.gameObject.AddComponent<Image>();
            picture.sprite = data?.Image;
            picture.preserveAspect = true;
            picture.color = data?.Image != null ? Color.white : new Color(0.18f, 0.28f, 0.34f, 1f);
            picture.raycastTarget = false;

            Text title = CreateText("Title", card, font, 30, FontStyle.Bold);
            title.text = string.IsNullOrWhiteSpace(data?.Title) ? $"未來關卡 {index + 1}" : data.Title;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.94f, 0.98f, 1f, 1f);
            Stretch(title.rectTransform, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.205f));
        }

        private void DestroyDemoEndPanel()
        {
            if (demoEndCanvas != null)
                Destroy(demoEndCanvas.gameObject);
            demoEndCanvas = null;
        }

        private void ConfigureFutureLevelImages(Sprite[] configuredImages)
        {
            if (configuredImages == null || configuredImages.Length == 0)
                return;
            if (futureLevelCards == null || futureLevelCards.Length < 4)
                futureLevelCards = new[] { new FutureLevelCard(), new FutureLevelCard(), new FutureLevelCard(), new FutureLevelCard() };
            for (int index = 0; index < Mathf.Min(4, configuredImages.Length); index++)
                futureLevelCards[index]?.SetImage(configuredImages[index]);
        }

        private void SubscribeToCrackImpact()
        {
            OtterCombatAnimator animator = presenter != null ? presenter.OtterAnimator : null;
            if (animator == subscribedOtterAnimator)
                return;
            UnsubscribeFromCrackImpact();
            subscribedOtterAnimator = animator;
            if (subscribedOtterAnimator != null)
                subscribedOtterAnimator.CrackImpact += OnCrackImpact;
        }

        private void UnsubscribeFromCrackImpact()
        {
            if (subscribedOtterAnimator != null)
                subscribedOtterAnimator.CrackImpact -= OnCrackImpact;
            subscribedOtterAnimator = null;
        }

        private void ResolveReferences()
        {
            if (runner == null)
                runner = GetComponent<OtterGoblinDemo1Runner>();
            if (beatClock == null && runner != null)
                beatClock = runner.BeatClock;
            if (combatInput == null)
                combatInput = GetComponent<OtterGoblinDemo1Input>();
            if (presenter == null)
                presenter = GetComponent<OtterGoblinDemo1Presenter>();
            if (sceneCamera == null)
                sceneCamera = Camera.main;
            if (isActiveAndEnabled)
                SubscribeToCrackImpact();
        }

        private void Update()
        {
            UpdateSpiritualWorldBackdrop();
            if (heroicSpiritRoot == null)
                return;

            float phase = Time.unscaledTime * heroicSpiritBobCyclesPerSecond * Mathf.PI * 2f;
            heroicSpiritRoot.position = heroicSpiritBasePosition
                + Vector3.up * (Mathf.Sin(phase) * heroicSpiritBobAmplitude);
            heroicSpiritRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * 0.8f);
        }

        private void OnValidate()
        {
            dialogueInputLockSeconds = Mathf.Max(0f, dialogueInputLockSeconds);
            goblinRaisedAxeHoldSeconds = Mathf.Max(0f, goblinRaisedAxeHoldSeconds);
            endingOtterSwimSeconds = Mathf.Max(0.1f, endingOtterSwimSeconds);
            endingOtterSwimDistance = Mathf.Max(0f, endingOtterSwimDistance);
            endingOtterSwimArcHeight = Mathf.Max(0f, endingOtterSwimArcHeight);
            SanitizeDialogue(openingDialogue);
            SanitizeDialogue(preHeroEndingDialogue);
            SanitizeDialogue(heroRevealDialogue);
            SanitizeDialogue(postOtterExitDialogue);
        }

        private static void SanitizeDialogue(DialogueLine[] sequence)
        {
            if (sequence == null)
                return;
            foreach (DialogueLine line in sequence)
                line?.Sanitize();
        }

        private void CaptureDefaultCamera()
        {
            if (sceneCamera == null)
                return;
            defaultCameraPosition = sceneCamera.transform.position;
            defaultCameraSize = sceneCamera.orthographicSize;
        }

        private Vector3 CameraPositionFor(Vector3 worldTarget)
        {
            float z = sceneCamera != null ? defaultCameraPosition.z : -10f;
            return new Vector3(worldTarget.x, worldTarget.y, z);
        }

        private void RestoreTimeScale()
        {
            if (openingRunning && !Mathf.Approximately(Time.timeScale, originalTimeScale))
                Time.timeScale = originalTimeScale;
        }

        private void DestroyDialogue()
        {
            if (dialogueCanvas != null)
                Destroy(dialogueCanvas.gameObject);
            dialogueCanvas = null;
        }

        private static bool WasAdvancePressed()
        {
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private static Font CreateDialogueFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft JhengHei", "Microsoft YaHei", "Noto Sans CJK TC", "Arial Unicode MS" },
                48);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Font font,
            int fontSize,
            FontStyle style)
        {
            RectTransform rect = CreateRect(objectName, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
