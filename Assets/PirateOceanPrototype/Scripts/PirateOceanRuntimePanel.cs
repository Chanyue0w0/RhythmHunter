using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.PirateOceanPrototype
{
    /// <summary>
    /// Immediate-mode prototype panel for tuning ocean, ship, and camera motion
    /// together during Play Mode. It intentionally has no Canvas dependencies.
    /// </summary>
    public sealed class PirateOceanRuntimePanel : MonoBehaviour
    {
        [Header("Prototype Systems")]
        [SerializeField] private PirateOceanWaveController oceanWaves;
        [SerializeField] private PirateShipMotionController shipMotion;
        [SerializeField] private PirateBossCameraController bossCamera;

        [Header("Panel")]
        [SerializeField] private bool panelVisible = true;
        [SerializeField] private Rect windowRect = new(18f, 18f, 390f, 650f);

        public bool PanelVisible => panelVisible;

        public void Configure(
            PirateOceanWaveController waveController,
            PirateShipMotionController motionController,
            PirateBossCameraController cameraController)
        {
            oceanWaves = waveController;
            shipMotion = motionController;
            bossCamera = cameraController;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                TogglePanel();
        }

        private void OnGUI()
        {
            if (!panelVisible)
            {
                if (GUI.Button(new Rect(18f, 18f, 190f, 30f), "F1  OPEN OCEAN LAB"))
                    panelVisible = true;
                return;
            }

            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "PIRATE OCEAN LAB");
        }

        public void SetPanelVisible(bool visible)
        {
            panelVisible = visible;
        }

        public void TogglePanel()
        {
            panelVisible = !panelVisible;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(4f);
            GUILayout.Label("F1: hide/show  |  B: toggle camera");

            GUILayout.Space(5f);
            GUILayout.Label("SEA STATE PRESETS");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CALM"))
                ApplyCalmPreset();
            if (GUILayout.Button("COMBAT"))
                ApplyCombatPreset();
            if (GUILayout.Button("STORM"))
                ApplyStormPreset();
            GUILayout.EndHorizontal();

            if (oceanWaves != null)
            {
                GUILayout.Space(7f);
                GUILayout.Label("OCEAN WAVES");
                DrawSlider("Intensity", oceanWaves.Intensity, 0f, 2f, oceanWaves.SetIntensity);
                DrawSlider("Height", oceanWaves.WaveHeight, 0f, 1.2f, oceanWaves.SetWaveHeight);
                DrawSlider("Speed", oceanWaves.WaveSpeed, 0f, 5f, oceanWaves.SetWaveSpeed);
                DrawSlider("Frequency", oceanWaves.Frequency, 0.1f, 3f, oceanWaves.SetFrequency);
                DrawSlider("Foam", oceanWaves.FoamAmount, 0f, 1f, oceanWaves.SetFoamAmount);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Direction: {oceanWaves.Direction}", GUILayout.Width(170f));
                if (GUILayout.Button("LEFT"))
                    oceanWaves.SetDirection(PirateOceanWaveController.TravelDirection.Left);
                if (GUILayout.Button("RIGHT"))
                    oceanWaves.SetDirection(PirateOceanWaveController.TravelDirection.Right);
                GUILayout.EndHorizontal();
            }

            if (shipMotion != null)
            {
                GUILayout.Space(7f);
                GUILayout.Label("SHIP MOTION");
                DrawSlider("Intensity", shipMotion.MotionIntensity, 0f, 2f, shipMotion.SetMotionIntensity);
                DrawSlider("Heave", shipMotion.HeaveAmplitude, 0f, 0.75f, shipMotion.SetHeaveAmplitude);
                DrawSlider("Sway", shipMotion.SwayAmplitude, 0f, 0.6f, shipMotion.SetSwayAmplitude);
                DrawSlider("Roll", shipMotion.RollDegrees, 0f, 12f, shipMotion.SetRollDegrees);
                DrawSlider("Pitch", shipMotion.PitchScaleAmount, 0f, 0.12f, shipMotion.SetPitchScaleAmount);
                DrawSlider("Speed", shipMotion.MotionSpeed, 0.05f, 3f, shipMotion.SetMotionSpeed);
            }

            if (bossCamera != null)
            {
                GUILayout.Space(7f);
                GUILayout.Label("CINEMACHINE");
                DrawSlider("Blend Seconds", bossCamera.BlendDuration, 0f, 6f, bossCamera.SetBlendDuration);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("SHIP VIEW"))
                    bossCamera.ShowShipCombatView();
                if (GUILayout.Button("BOSS VIEW"))
                    bossCamera.ShowBossWideView();
                GUILayout.EndHorizontal();
                GUILayout.Label(bossCamera.IsBlending
                    ? "Status: BLENDING"
                    : bossCamera.BossViewActive ? "Status: BOSS WIDE" : "Status: SHIP COMBAT");
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("HIDE PANEL (F1)"))
                panelVisible = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private static void DrawSlider(
            string label,
            float currentValue,
            float minimum,
            float maximum,
            Action<float> setter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {currentValue:0.00}", GUILayout.Width(170f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, minimum, maximum);
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(nextValue, currentValue))
                setter(nextValue);
        }

        private void ApplyCalmPreset()
        {
            ApplyPreset(
                0.55f, 0.18f, 0.75f, 0.65f, 0.4f,
                0.55f, 0.08f, 0.035f, 1.3f, 0.012f, 0.55f);
        }

        private void ApplyCombatPreset()
        {
            ApplyPreset(
                1f, 0.34f, 1.25f, 0.82f, 0.72f,
                1f, 0.14f, 0.08f, 2.8f, 0.025f, 0.78f);
        }

        private void ApplyStormPreset()
        {
            ApplyPreset(
                1.5f, 0.65f, 2.4f, 1.15f, 1f,
                1.45f, 0.25f, 0.15f, 6f, 0.04f, 1.25f);
        }

        private void ApplyPreset(
            float oceanIntensity,
            float waveHeight,
            float waveSpeed,
            float waveFrequency,
            float foamAmount,
            float shipIntensity,
            float heave,
            float sway,
            float roll,
            float pitch,
            float shipSpeed)
        {
            if (oceanWaves != null)
            {
                oceanWaves.SetIntensity(oceanIntensity);
                oceanWaves.SetWaveHeight(waveHeight);
                oceanWaves.SetWaveSpeed(waveSpeed);
                oceanWaves.SetFrequency(waveFrequency);
                oceanWaves.SetFoamAmount(foamAmount);
            }

            if (shipMotion != null)
            {
                shipMotion.SetMotionIntensity(shipIntensity);
                shipMotion.SetHeaveAmplitude(heave);
                shipMotion.SetSwayAmplitude(sway);
                shipMotion.SetRollDegrees(roll);
                shipMotion.SetPitchScaleAmount(pitch);
                shipMotion.SetMotionSpeed(shipSpeed);
            }
        }
    }
}
