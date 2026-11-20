using System;
using System.Collections.Generic;
using UnityEngine;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        private sealed class MageGuard
        { public FightUnitSlot actor; public long start, end, fireBeat; public int version; public bool blocked; }
        private sealed class SlimeReduction
        { public FightUnitSlot target; public long until; public float amount, minimum; public GameObject visual; public Vector3 offset; public Transform foot; }
        private readonly List<MageGuard> mageGuards = new();
        private readonly List<SlimeReduction> slimeReductions = new();
        public event Action<FightUnitSlot> MageSlimeAttached;
        private void TrackMageGuard(FightUnitSlot actor, long beat)
        {
            if (!UsesEqualBeats || actor.CharacterDefinition == null || !actor.CharacterDefinition.EnableUnusedGuardSlime) return;
            if (GetComponent<FightTutorialController>()?.SuppressMageRetaliation == true) return;
            if (mageGuards.Exists(g => g.actor == actor && g.start == beat)) return;
            mageGuards.Add(new MageGuard { actor = actor, start = beat, end = beat + actor.CharacterDefinition.BasicGuardBeats, version = rosterVersion });
        }
        private void RecordMageGuardBlock(long beat)
        {
            foreach (var guard in mageGuards)
                if (!guard.blocked && beat >= guard.start && beat < guard.end)
                { guard.blocked = true; guard.fireBeat = beat + 1; }
        }
        private void ShiftMageGuards(long beats)
        { foreach (var guard in mageGuards) { guard.start += beats; guard.end += beats; if (guard.blocked) guard.fireBeat += beats; } }
        private void UpdateMageAbility()
        {
            for (int i = slimeReductions.Count - 1; i >= 0; i--)
            {
                var state = slimeReductions[i];
                if (state.target == null || !state.target.HasCharacter || state.target.CurrentHp <= 0 || NormalBattleBeatCount >= state.until)
                { ReturnSkillVisual(state.visual); slimeReductions.RemoveAt(i); }
                else if (state.visual != null) state.visual.transform.position = MageSlimeFloorPosition(state);
            }
            if (TeamSkillRunning || EnemyActionsPaused || WaveTransitionActive || !TryGetQteBeat(out double now)) return;
            for (int i = mageGuards.Count - 1; i >= 0; i--)
            {
                var guard = mageGuards[i];
                if (guard.version != rosterVersion || guard.actor == null || !guard.actor.HasCharacter)
                { mageGuards.RemoveAt(i); continue; }
                if (now < (guard.blocked ? guard.fireBeat : guard.end)) continue;
                // Resolve late-window hits first; one successful shield cast launches at most once.
                if (!guard.blocked && enemyHits.Exists(h => h.hitBeat >= guard.start && h.hitBeat < guard.end)) continue;
                if (!guard.blocked && pendingEnemyAttack && pendingAttackGlobalBeat >= guard.start && pendingAttackGlobalBeat < guard.end) continue;
                mageGuards.RemoveAt(i);
                if (guard.blocked) CastMageRetaliation(guard.actor);
            }
        }
        private void CastMageRetaliation(FightUnitSlot actor)
        {
            int version = rosterVersion;
            var target = FindFrontLivingEnemy();
            if (target == null) return;
            var data = actor.CharacterDefinition;
            // Launch on the next ordinary beat, independently of visual animation completion.
            actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Skill, null, null, null,
                effect => { if (IsCurrentHero(version, actor) && effect.location == FightCharacterCombatAnimator.EffectLocation.Self) actor.PlayFrameEffect(effect); });
            if (data.QteProjectilePrefab == null) return;
            var visual = Instantiate(data.QteProjectilePrefab, actor.CastEffectAnchor.position, data.QteProjectilePrefab.transform.rotation);
            visual.transform.localScale *= data.RetaliationProjectileScale;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
            visual.SetActive(true);
            visual.GetComponent<FightCameraFacingEffect>()?.Bind(statusIconCamera != null ? statusIconCamera : Camera.main);
            var flight = visual.GetComponent<FightBeatProjectile>() ?? visual.AddComponent<FightBeatProjectile>();
            flight.Initialize(this, target.ImpactEffectAnchor, data.QteProjectileFlightBeats,
                () => IsCurrentHeroAction(version, actor, target) && target.CurrentHp > 0 && !battleEnded && !WaveTransitionActive,
                () => ApplyMageSlime(actor, target), arcHeight: data.QteProjectileArcHeight);
        }
        private void ApplyMageSlime(FightUnitSlot actor, FightUnitSlot target)
        {
            var data = actor.CharacterDefinition;
            var state = slimeReductions.Find(s => s.target == target);
            if (state == null) { state = new SlimeReduction { target = target }; slimeReductions.Add(state); }
            state.until = NormalBattleBeatCount + data.SlimeAttackReductionBeats;
            state.amount = data.SlimeAttackReduction; state.minimum = data.SlimeMinimumAttackDamage;
            state.offset = data.RetaliationFloorOffset;
            state.foot = target.ActorInstance != null ? target.ActorInstance.transform.Find("FootAnchor") : null;
            if (state.visual == null)
            {
                state.visual = RentSkillVisual(data.SlimeFloorPrefab, MageSlimeFloorPosition(state));
                if (state.visual != null)
                {
                    state.visual.transform.rotation = Quaternion.Euler(90, 0, 0);
                    foreach (var sprite in state.visual.GetComponentsInChildren<SpriteRenderer>(true))
                    { var color = sprite.color; color.a *= data.RetaliationFloorOpacity; sprite.color = color; }
                }
            }
            MageSlimeAttached?.Invoke(target);
        }
        private Vector3 MageSlimeFloorPosition(SlimeReduction state)
        {
            var position = state.foot != null ? state.foot.position : state.target.transform.position;
            // Ground decal: do not ride the animated sprite bounds or the enemy's hit-hop.
            position.y = state.target.transform.position.y;
            return position + state.offset;
        }
        internal GameObject ApplyFeverMageSlime(FightUnitSlot actor, FightUnitSlot target)
        {
            if (!actor || !target || !target.HasCharacter || target.CurrentHp <= 0) return null;
            ApplyMageSlime(actor, target);
            var state = slimeReductions.Find(s => s.target == target);
            state.until = NormalBattleBeatCount + 8;
            return state.visual;
        }
        public int MageWeakenBeatsRemaining(FightUnitSlot target)
        {
            var state = slimeReductions.Find(s => s.target == target);
            return state == null ? 0 : Mathf.Max(0, (int)(state.until - NormalBattleBeatCount));
        }
        public float GetSlimeReducedDamage(FightUnitSlot source, float damage)
        {
            var state = slimeReductions.Find(s => s.target == source && NormalBattleBeatCount < s.until);
            return state == null ? damage : Mathf.Min(damage, Mathf.Max(state.minimum, damage - state.amount));
        }
        public bool HasMageWeaken(FightUnitSlot target) => target != null && target.CurrentHp > 0 &&
            slimeReductions.Exists(s => s.target == target && NormalBattleBeatCount < s.until);
        private void ClearMageAbility()
        { mageGuards.Clear(); foreach (var state in slimeReductions) ReturnSkillVisual(state.visual); slimeReductions.Clear(); }
    }
}
