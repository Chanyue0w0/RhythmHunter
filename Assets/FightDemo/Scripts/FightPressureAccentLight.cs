using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>Shares the existing Pressure lighting fade; authored intensity is restored outside playback.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class FightPressureAccentLight : MonoBehaviour
    {
        [SerializeField] private FightCombatController combat;
        [Tooltip("啟用 Pressure 額外紅光。關閉後，預覽及戰鬥期間都不亮。")]
        public bool enablePressureLight = true;
        [Tooltip("只在非 Play 模式預覽此燈。Play 時跟隨原有 Pressure 燈光預告同步淡入、淡出，不會因預覽而常亮。")]
        public bool previewInEditor = true;
        private Light accent;
        private FightPressureVisuals pressureVisuals;
        private bool ownsIntensity;
        private float authoredIntensity, lastIntensity;

        public void Configure(FightCombatController controller)
        {
            combat = controller;
            FindPressureVisuals();
            RefreshLight();
        }

        internal static bool ShouldIlluminate(bool enabled, bool playing, bool preview, bool pressure) =>
            enabled && (playing ? pressure : preview);

        public void RefreshLight()
        {
            if (accent == null) accent = GetComponent<Light>();
            bool playing = Application.IsPlaying(gameObject);
            float blend = pressureVisuals != null && pressureVisuals.isActiveAndEnabled && combat != null && combat.isActiveAndEnabled
                ? pressureVisuals.PressureLightBlend : 0;
            ApplyLightState(playing, blend);
        }

        private void ApplyLightState(bool playing, float blend)
        {
            blend = Mathf.Clamp01(blend);
            bool show = isActiveAndEnabled && ShouldIlluminate(enablePressureLight, playing, previewInEditor, blend > 0);
            if (accent != null && playing && show)
            {
                if (!ownsIntensity || !Mathf.Approximately(accent.intensity, lastIntensity))
                    authoredIntensity = accent.intensity;
                ownsIntensity = true;
                accent.intensity = lastIntensity = authoredIntensity * blend;
            }
            else RestoreIntensity();
            if (accent != null && accent.enabled != show) accent.enabled = show;
        }

        private void FindPressureVisuals()
        {
            pressureVisuals = null;
            foreach (var visual in FindObjectsByType<FightPressureVisuals>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (visual.gameObject.scene == gameObject.scene && visual.CombatController == combat)
                { pressureVisuals = visual; break; }
        }

        private void RestoreIntensity()
        {
            if (ownsIntensity && accent != null) accent.intensity = authoredIntensity;
            ownsIntensity = false;
        }

        private void OnEnable() { FindPressureVisuals(); RefreshLight(); }
        private void LateUpdate() => RefreshLight();
        private void OnDisable()
        {
            if (accent == null) accent = GetComponent<Light>();
            RestoreIntensity();
            if (accent != null) accent.enabled = false;
        }
    }
}
