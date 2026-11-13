using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightUnitSlot
    {
        [Header("Rest GetHurt Hop")]
        [SerializeField] private bool enableRestHitHop = true;
        [Tooltip("World X distance to the left. The hop returns within half a beat while GetHurt continues.")]
        [SerializeField, Min(0)] private float restHitHopDistance = .12f;
        [SerializeField, Min(0)] private float restHitHopHeight = .16f;
        [SerializeField, Range(.01f, .5f)] private float restHitHopBeats = .5f;
        [SerializeField, Min(0)] private float restHitShakeDistance = .015f;
        private bool restHitHopActive;
        private bool deathPresentationStarted;
        public bool DeathPresentationPending => team == UnitTeam.Enemy && currentHp <= 0 && actorInstance != null && !RetainedForBurst &&
            (!deathPresentationStarted || combatAnimator != null && combatAnimator.IsPlaying &&
            combatAnimator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.Hit);

        private void BeginDeathPresentation()
        {
            if (deathPresentationStarted || actorInstance == null) return;
            deathPresentationStarted = true;
            ResetInputFeedback();
            combatAnimator?.InterruptPlayback();
            if (combatAnimator == null) return;
            // Enemy-action pauses must not prevent a defeated enemy's final reaction.
            combatAnimator.SetPlaybackPaused(false);
            if (combatAnimator.Play(FightCharacterCombatAnimator.CombatAnimation.Hit)) BeginRestHitHop();
        }

        private void BeginRestHitHop()
        {
            ResetInputFeedback();
            if (!enableRestHitHop || actorInstance == null) return;
            inputFeedbackTarget = actorInstance.transform;
            inputFeedbackOrigin = inputFeedbackTarget.localPosition;
            restHitHopActive = true;
        }

        private void UpdateRestHitHop()
        {
            if (!restHitHopActive) return;
            if (!enableRestHitHop || (currentHp <= 0 && !deathPresentationStarted) || inputFeedbackTarget == null || combatAnimator == null ||
                !combatAnimator.IsPlaying || combatAnimator.ActiveAnimation != FightCharacterCombatAnimator.CombatAnimation.Hit)
            {
                ResetInputFeedback();
                return;
            }
            // Read actual animation progress: tempo changes and pauses cannot desynchronize the hop.
            float progress = combatAnimator.ActiveSequenceElapsedBeats / Mathf.Clamp(restHitHopBeats, .01f, .5f);
            if (progress >= 1f) { ResetInputFeedback(); return; }
            // First 20%: fast ascent. Next 30%: shake at the apex. Last 50%: return.
            float arc = progress < .2f ? 1f - Mathf.Pow(1f - progress / .2f, 3f)
                : progress < .5f ? 1f : 1f - Mathf.SmoothStep(0, 1, (progress - .5f) / .5f);
            float shakePhase = Mathf.Clamp01((progress - .2f) / .3f);
            float shake = Mathf.Sin(shakePhase * Mathf.PI * 4f) * Mathf.Sin(shakePhase * Mathf.PI) * restHitShakeDistance;
            Vector3 offset = new Vector3(-restHitHopDistance * arc + shake, restHitHopHeight * arc, 0);
            var parent = inputFeedbackTarget.parent;
            inputFeedbackTarget.position = (parent != null ? parent.TransformPoint(inputFeedbackOrigin) : inputFeedbackOrigin) + offset;
        }
    }
}
