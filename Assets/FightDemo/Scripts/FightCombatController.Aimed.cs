using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Automatic Skills / Fever Migration")]
        [Tooltip("Keep A rhythm feedback, but do not spend MP or start the old Team Skill/Fever.")]
        [SerializeField] private bool suspendFeverActivation = true;
        [Tooltip("Normal melee impact and Aimed bullet arrival share this song-beat delay from the accepted call.")]
        [SerializeField, Min(.01f)] private float normalAttackImpactBeats = .4f;
        private sealed class NormalImpact
        {
            public int roster;
            public double progress;
            public FightUnitSlot actor, target;
            public Action resolve;
            public GameObject projectile;
            public Vector3 origin;
        }
        private readonly List<NormalImpact> normalImpacts = new();
        private int normalImpactLastMs;
        private bool hasNormalImpactTime;

        private FightUnitSlot FindAimedTarget(FightUnitSlot owner, bool goldOnly)
        {
            foreach (var enemy in fightScene2Enemies)
                if (enemy != null && enemy.AimedOwner == owner && (!goldOnly || enemy.HasGoldAimed)) return enemy;
            return null;
        }

        private bool IsAttackCall(FightCharacterDefinition.AbilityBehavior behavior) => behavior == FightCharacterDefinition.AbilityBehavior.DamageFront ||
            behavior == FightCharacterDefinition.AbilityBehavior.DamageAll || behavior == FightCharacterDefinition.AbilityBehavior.GuardAndDamageFront;

        private void TriggerAimedFollowUp(FightUnitSlot caller)
        {
            if (WaveTransitionActive || caller == null || caller.NormalAbilityBehavior == FightCharacterDefinition.AbilityBehavior.AimedShot) return;
            // Called once at the successful command boundary, never by damage/animation events.
            foreach (var hero in new[] { frontHero, secondHero, thirdHero })
            {
                var shooter = hero?.UnitSlot;
                if (shooter == null || shooter == caller || !shooter.HasCharacter || shooter.CharacterDefinition == null ||
                    !shooter.CharacterDefinition.EnableAimedFollowUp) continue;
                var target = FindAimedTarget(shooter, true);
                if (target != null) LaunchAimedShot(shooter, target, shooter.CharacterDefinition.AimedFollowUpDamage, true);
            }
        }

        private void LaunchAimedShot(FightUnitSlot actor, FightUnitSlot target, float damage, bool followUp)
        {
            var data = actor.CharacterDefinition;
            var prefab = data.AimedGoldProjectile != null ? data.AimedGoldProjectile : actor.NormalAttackEffectPrefab;
            GameObject visual = null;
            if (prefab != null)
            {
                var anchor = actor.CastEffectAnchor;
                var settings = prefab.GetComponent<FightEffectSettings>();
                var position = anchor.position + (target.ImpactEffectAnchor.position - anchor.position).normalized *
                    (settings != null ? settings.forwardSpawnDistance : .25f);
                visual = Instantiate(prefab, position, anchor.rotation * Quaternion.Euler(0, 0, prefab.transform.eulerAngles.z));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
                (visual.GetComponent<FightBattleEffect>() ?? visual.AddComponent<FightBattleEffect>()).Configure(this, false, 0);
            }
            int version = rosterVersion;
            ScheduleNormalImpact(actor, target, () =>
            {
                // Contact and damage are distinct: armor/guard may make damage zero.
                // Entry invulnerability, death, and a different wave are not legal contacts.
                if (WaveTransitionActive || !IsCurrentHeroAction(version, actor, target) || target.CurrentHp <= 0) return;
                ApplyHeroDamage(version, actor, target, damage, false, followUp);
                if (target.CurrentHp <= 0 || !IsCurrentHeroAction(version, actor, target)) return;
                if (!followUp)
                {
                    target.ApplyAimed(actor, true);
                    if (data.AimedTargetEffect != null)
                        actor.EffectPlayer.SpawnConfiguredAbility(data.AimedTargetEffect, target.ImpactEffectAnchor, 0,
                            FightAttackEffect.VisualStyle.Skill, Color.yellow);
                }
                else PulseAimedIcon(target);
            }, visual);
            // This is presentation only: no callback can recursively create a new attack call.
            actor.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, null, null, null,
                effect => { if (effect.location == FightCharacterCombatAnimator.EffectLocation.Self) actor.PlayFrameEffect(effect); });
        }

        private void ScheduleNormalImpact(FightUnitSlot actor, FightUnitSlot target, Action resolve, GameObject projectile = null)
        {
            if (normalImpacts.Count == 0 && beatClock != null)
                hasNormalImpactTime = beatClock.TryGetTimelinePositionMs(out normalImpactLastMs);
            normalImpacts.Add(new NormalImpact { roster = rosterVersion, actor = actor, target = target, resolve = resolve,
                projectile = projectile, origin = projectile != null ? projectile.transform.position : Vector3.zero });
        }

        private void UpdateNormalImpacts()
        {
            if (beatClock == null || !beatClock.TryGetTimelinePositionMs(out int now)) return;
            double delta = hasNormalImpactTime ? Math.Max(0, now - normalImpactLastMs) / Math.Max(1d, beatClock.MillisecondsPerBeat) : 0;
            normalImpactLastMs = now; hasNormalImpactTime = true;
            if (IsPaused || TimingCalibrationActive || AwaitingBattleStart) return;
            AdvanceNormalImpacts(delta);
        }

        private void AdvanceNormalImpacts(double beats)
        {
            for (int i = 0; i < normalImpacts.Count;)
            {
                var hit = normalImpacts[i];
                bool valid = !battleEnded && IsCurrentHeroAction(hit.roster, hit.actor, hit.target) && hit.target.CurrentHp > 0;
                hit.progress += beats;
                float progress = Mathf.Clamp01((float)(hit.progress / Mathf.Max(.01f, normalAttackImpactBeats)));
                if (valid && hit.projectile != null) hit.projectile.transform.position = Vector3.Lerp(hit.origin, hit.target.ImpactEffectAnchor.position, progress);
                if (valid && progress < 1) { i++; continue; }
                normalImpacts.RemoveAt(i);
                if (hit.projectile != null) hit.projectile.GetComponent<FightBattleEffect>().Cancel();
                if (valid) hit.resolve?.Invoke();
                // A lethal hit can synchronously clear/rebuild the wave and this list.
            }
        }

        private void ClearNormalImpacts()
        {
            foreach (var hit in normalImpacts)
                if (hit.projectile != null) hit.projectile.GetComponent<FightBattleEffect>().Cancel();
            normalImpacts.Clear(); hasNormalImpactTime = false;
        }
    }
}
