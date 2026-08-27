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
        [Header("Master Switch")]
        [Tooltip("Disable before Play to bypass every cinematic and test combat immediately.")]
        [SerializeField] private bool enableCinematics = true;

        [Header("Sequence Slots (may be empty)")]
        [SerializeField] private bool playBuiltInOpening = true;
        [SerializeField] private bool skipRhythmTimelineIntroAfterCinematic = true;
        [SerializeField] private PlayableDirector additionalOpeningTimeline;
        [SerializeField] private PlayableDirector endingTimeline;

        [Header("Dependencies")]
        [SerializeField] private OtterGoblinDemo1Runner runner;
        [SerializeField] private FmodBeatClock beatClock;
        [SerializeField] private OtterGoblinDemo1Input combatInput;
        [SerializeField] private OtterGoblinDemo1Presenter presenter;
        [SerializeField] private Camera sceneCamera;

        [Header("Opening Timing")]
        [SerializeField, Min(0.1f)] private float otterSwimSeconds = 2.2f;
        [SerializeField, Min(0.1f)] private float cameraPanSeconds = 0.85f;
        [SerializeField, Min(0.1f)] private float dialogueAutoAdvanceSeconds = 2f;
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
        private Canvas dialogueCanvas;

        public bool EnableCinematics => enableCinematics;
        public bool HasOpeningContent => playBuiltInOpening || additionalOpeningTimeline != null;
        public bool HasEndingContent => endingTimeline != null;

        public void Configure(
            OtterGoblinDemo1Runner configuredRunner,
            FmodBeatClock configuredBeatClock,
            OtterGoblinDemo1Input configuredInput,
            OtterGoblinDemo1Presenter configuredPresenter,
            Camera configuredCamera)
        {
            runner = configuredRunner;
            beatClock = configuredBeatClock;
            combatInput = configuredInput;
            presenter = configuredPresenter;
            sceneCamera = configuredCamera;
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
        }

        private void OnDisable()
        {
            if (runner != null)
                runner.BattleWon -= OnBattleWon;
            RestoreTimeScale();
            DestroyDialogue();
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
            yield return ShowDialogue(
                "斧頭哥布林",
                "既然被你發現了，那就只好解決你了");

            presenter.ShowCinematicEnemyRaisedAxe();
            yield return new WaitForSecondsRealtime(0.38f);
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

        private IEnumerator ShowDialogue(string speaker, string line)
        {
            CreateDialogue(speaker, line);
            float elapsed = 0f;
            while (elapsed < dialogueAutoAdvanceSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 0.15f && WasAdvancePressed())
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
            speakerText.color = new Color(1f, 0.58f, 0.23f, 1f);

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
            if (!enableCinematics || endingRunning || endingTimeline == null)
                return;
            StartCoroutine(RunEnding());
        }

        private IEnumerator RunEnding()
        {
            endingRunning = true;
            combatInput?.SetDefendEnabled(false);
            yield return PlayAndWait(endingTimeline);
            endingRunning = false;
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

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
