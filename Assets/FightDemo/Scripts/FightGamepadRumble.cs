using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightGamepadRumble : MonoBehaviour
    {
        [SerializeField] private bool feedbackEnabled = true;
        private Gamepad device;
        private float until;
        public void Pulse(float strength, float seconds = .065f)
        {
            if (!feedbackEnabled || !Application.isFocused) return;
            var current = Gamepad.current;
            if (current == null) return;
            if (device != current) Stop();
            device = current;
            device.SetMotorSpeeds(Mathf.Clamp01(strength * .45f), Mathf.Clamp01(strength));
            until = Time.unscaledTime + seconds;
        }
        public void Stop()
        {
            if (device != null && device.added) device.ResetHaptics();
            device = null; until = 0;
        }
        private void Update()
        {
            if (device != null && (!device.added || Time.unscaledTime >= until || GetComponent<FightCombatController>()?.IsPaused == true)) Stop();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
        private void OnApplicationPause(bool paused) { if (paused) Stop(); }
        private void OnDisable() => Stop();
    }
}
