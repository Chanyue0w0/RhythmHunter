using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightDamageEdgeFeedback : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private bool showDamageFlash = true;
        [SerializeField] private bool showLowHealthPulse = true;
        [SerializeField] private Color armorHitColor = new(.45f, .8f, 1f, .42f);
        [SerializeField] private Color hpHitColor = new(1f, .06f, .04f, .65f);
        [SerializeField] private Color lowHealthColor = new(1f, .38f, .4f, .18f);
        [SerializeField, Min(.05f)] private float hitFlashSeconds = .45f;
        [SerializeField, Range(.01f, .45f)] private float edgeWidth = .16f;
        [SerializeField, Min(.5f)] private float lowHealthThreshold = 1;
        [Tooltip("Low-health pulse period uses the existing song beat clock.")]
        [SerializeField, Min(.25f)] private float lowHealthPulseBeats = 2;
        private FightScreenEdgeGraphic graphic;
        private GameObject canvasObject;
        private float hitRemaining;
        private bool hpHit;
        private void OnEnable()
        {
            if (fight != null) fight.PartyDamageTaken += OnDamage;
        }
        private void OnDisable()
        {
            if (fight != null) fight.PartyDamageTaken -= OnDamage;
            hitRemaining = 0;
            if (canvasObject != null) canvasObject.SetActive(false);
        }
        private void OnDestroy()
        {
            if (canvasObject == null) return;
            if (Application.isPlaying) Destroy(canvasObject); else DestroyImmediate(canvasObject);
        }
        private void OnDamage(float armorLoss, float hpLoss)
        {
            if (!showDamageFlash || (armorLoss <= 0 && hpLoss <= 0)) return;
            // A later armor-only hit must not hide a still-visible HP hit from the same burst.
            if (hpLoss <= 0 && hpHit && hitRemaining > 0) return;
            hpHit = hpLoss > 0;
            hitRemaining = Mathf.Max(.05f, hitFlashSeconds);
            Render(0);
        }
        private void Update()
        {
            if (fight != null && (fight.IsPaused || fight.TimingCalibrationActive)) return;
            Render(Time.unscaledDeltaTime);
        }
        private void Render(float delta)
        {
            if (fight == null || !fight.HealthSystemEnabled || fight.AwaitingBattleStart || fight.BattleEnded)
            {
                hitRemaining = 0;
                if (canvasObject != null) canvasObject.SetActive(false);
                return;
            }
            hitRemaining = Mathf.Max(0, hitRemaining - Mathf.Max(0, delta));
            Color tint = Color.clear;
            if (showLowHealthPulse && fight.PartyHp > 0 && fight.PartyHp <= lowHealthThreshold)
            {
                var clock = fight.BeatClock;
                double position = Time.unscaledTime * 2; // Preview fallback only; no combat timing uses this.
                if (clock != null && clock.HasTimingAnchor && clock.TryGetBeatPhase(out float phase))
                    position = clock.LatestBeat.GlobalBeat + phase;
                float period = Mathf.Max(.25f, lowHealthPulseBeats);
                float pulse = .5f + .5f * Mathf.Cos((float)(position % period) / period * Mathf.PI * 2);
                tint = lowHealthColor; tint.a *= Mathf.Lerp(.2f, 1f, pulse);
            }
            if (showDamageFlash && hitRemaining > 0)
            {
                var flash = hpHit ? hpHitColor : armorHitColor;
                flash.a *= Mathf.SmoothStep(0, 1, hitRemaining / Mathf.Max(.05f, hitFlashSeconds));
                tint = Color.Lerp(tint, flash, Mathf.Clamp01(hitRemaining / .08f));
            }
            if (tint.a <= .001f)
            {
                if (canvasObject != null) canvasObject.SetActive(false);
                return;
            }
            EnsureGraphic();
            canvasObject.SetActive(true);
            if (!Mathf.Approximately(graphic.width, edgeWidth)) { graphic.width = edgeWidth; graphic.SetVerticesDirty(); }
            graphic.color = tint;
        }
        private void EnsureGraphic()
        {
            if (graphic != null) return;
            canvasObject = new GameObject("PlayerDamageEdgeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Below results (200). No raycaster: never intercepts input.
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var rect = FightMenuUi.Rect("DamageEdges", canvas.transform, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = rect.gameObject.AddComponent<FightScreenEdgeGraphic>();
            graphic.raycastTarget = false;
        }
    }
}
