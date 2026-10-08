using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Owns visual cleanup on wave/end; never resolves damage from a travel callback.
    [DefaultExecutionOrder(-100)]
    public sealed class FightBattleEffect : MonoBehaviour
    {
        private FightCombatController fight;
        private bool enemy;
        private float remaining;
        private readonly List<ParticleSystem> pausedParticles = new();
        private Animator[] animators;
        private float[] speeds;
        private bool paused;
        public bool Frozen => fight != null && (fight.IsPaused || fight.TimingCalibrationActive || (enemy && fight.EnemyActionsPaused));
        public void Configure(FightCombatController controller, bool isEnemy, float lifetime)
        {
            fight = controller; enemy = isEnemy; remaining = lifetime;
            animators = GetComponentsInChildren<Animator>(true);
            speeds = new float[animators.Length];
            fight?.RegisterBattleEffect(this);
        }
        private void Update()
        {
            bool freeze = Frozen;
            if (freeze != paused)
            {
                paused = freeze;
                if (freeze)
                {
                    foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
                        if (particles.isPlaying) { particles.Pause(false); pausedParticles.Add(particles); }
                    for (int i = 0; i < animators.Length; i++) { speeds[i] = animators[i].speed; animators[i].speed = 0; }
                }
                else
                {
                    foreach (var particles in pausedParticles) if (particles != null) particles.Play(false);
                    pausedParticles.Clear();
                    for (int i = 0; i < animators.Length; i++) if (animators[i] != null) animators[i].speed = speeds[i];
                }
            }
            if (freeze || remaining <= 0) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) Destroy(gameObject);
        }
        public void Cancel()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
        private void OnDestroy() { if (fight != null) fight.UnregisterBattleEffect(this); }
    }

    public sealed partial class FightCombatController
    {
        private readonly HashSet<FightBattleEffect> battleEffects = new();
        internal void RegisterBattleEffect(FightBattleEffect effect) => battleEffects.Add(effect);
        internal void UnregisterBattleEffect(FightBattleEffect effect) => battleEffects.Remove(effect);
        private void ClearBattleEffects()
        {
            foreach (var effect in new List<FightBattleEffect>(battleEffects)) if (effect != null) effect.Cancel();
            battleEffects.Clear();
        }
    }
}
