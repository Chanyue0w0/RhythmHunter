using System.Collections.Generic;
using CartoonFX;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Adapt runtime combat instances only. Purchased prefabs and non-battle scenes stay untouched.
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    public sealed class Art2BattleCameraShake : MonoBehaviour
    {
        public FightCombatController combat;
        public Art2CameraController cameraController;
        public bool enableEffectShake = true;
        [Min(0)] public float strengthMultiplier = .65f;
        [Min(0)] public float maximumEffectShake = .08f;
        [Min(1)] public float frequency = 24;
        private sealed class Voice
        {
            public CFXR_Effect effect;
            public FightBattleEffect owner;
            public CFXR_Effect.CameraShake settings;
            public float elapsed, phase;
        }
        private readonly List<Voice> voices = new();
        private readonly List<CFXR_Effect> adapted = new();
        private void OnEnable() { if (combat) combat.BattleEffectSpawned += OnEffect; }
        private void OnEffect(FightBattleEffect owner)
        {
            foreach (var effect in owner.GetComponentsInChildren<CFXR_Effect>(true))
            {
                var settings = effect.cameraShake;
                if (settings == null || !settings.enabled) continue;
                settings.StopShake(); settings.enabled = false;
                adapted.Add(effect);
                // Bound simultaneous contributions; multiple particles cannot amplify indefinitely.
                if (voices.Count >= 8) voices.RemoveAt(0);
                voices.Add(new Voice { effect = effect, owner = owner, settings = settings, phase = Random.value * 6.28f });
            }
        }
        private void LateUpdate()
        {
            Vector2 sum = Vector2.zero;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var v = voices[i];
                if (!v.effect || !v.owner || !v.owner.isActiveAndEnabled) { voices.RemoveAt(i); continue; }
                if (!v.owner.Frozen) v.elapsed += Time.deltaTime;
                float duration = Mathf.Max(.01f, v.settings.duration), t = v.elapsed - v.settings.delay;
                if (t > duration) { voices.RemoveAt(i); continue; }
                if (t < 0 || !enableEffectShake) continue;
                float weight = v.settings.shakeCurve != null ? v.settings.shakeCurve.Evaluate(t / duration) : 1 - t / duration;
                var strength = v.settings.shakeStrength;
                float phase = t * frequency * Mathf.PI * 2 + v.phase;
                sum += new Vector2(Mathf.Sin(phase) * strength.x, Mathf.Sin(phase * 1.17f) * strength.y) * (weight * strengthMultiplier);
            }
            adapted.RemoveAll(x => x == null);
            if (cameraController) cameraController.SetEffectShake(Vector2.ClampMagnitude(sum, maximumEffectShake));
        }
        private void OnDisable()
        {
            if (combat) combat.BattleEffectSpawned -= OnEffect;
            if (cameraController) cameraController.SetEffectShake(Vector2.zero);
            foreach (var effect in adapted) if (effect && effect.cameraShake != null) effect.cameraShake.enabled = true;
            adapted.Clear(); voices.Clear();
        }
    }
}
