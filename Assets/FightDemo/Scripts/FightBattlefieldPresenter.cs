using RhythmHunter.RhythmDemo;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    /// <summary>
    /// Drives only world-space battlefield objects. HUD is owned by FightScenePresenter.
    /// </summary>
    public sealed class FightBattlefieldPresenter : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [SerializeField] private FightUnitSlot[] enemySlots;
        [SerializeField] private FightUnitSlot[] heroSlots;
        [SerializeField] private SpriteRenderer tankShield;
        [SerializeField] private SpriteRenderer enemyTelegraph;

        private float shieldTimer;
        private float telegraphTimer;

        public FightUnitSlot[] EnemySlots => enemySlots;
        public FightUnitSlot[] HeroSlots => heroSlots;

        public void Configure(
            FightCombatController controller,
            FightUnitSlot[] enemies,
            FightUnitSlot[] heroes,
            SpriteRenderer shield,
            SpriteRenderer telegraph)
        {
            fight = controller;
            enemySlots = enemies;
            heroSlots = heroes;
            tankShield = shield;
            enemyTelegraph = telegraph;
        }

        private void OnEnable()
        {
            if (fight == null)
                return;

            fight.FightBeat += OnFightBeat;
            fight.HeroCalled += OnHeroCalled;
            fight.EnemyAttackResolved += OnEnemyAttackResolved;
        }

        private void Start()
        {
            SetAlpha(tankShield, 0f);
            SetAlpha(enemyTelegraph, 0f);
        }

        private void OnDisable()
        {
            if (fight == null)
                return;

            fight.FightBeat -= OnFightBeat;
            fight.HeroCalled -= OnHeroCalled;
            fight.EnemyAttackResolved -= OnEnemyAttackResolved;
        }

        private void Update()
        {
            shieldTimer = Mathf.Max(0f, shieldTimer - Time.deltaTime);
            float shieldAlpha = Mathf.Clamp01(shieldTimer * 3f) * 0.75f;
            SetAlpha(tankShield, shieldAlpha);
            if (tankShield != null)
                tankShield.transform.localScale = Vector3.one * Mathf.Lerp(1.7f, 2.1f, shieldAlpha);

            telegraphTimer = Mathf.Max(0f, telegraphTimer - Time.deltaTime);
            SetAlpha(enemyTelegraph, Mathf.Clamp01(telegraphTimer * 5f) * 0.65f);
        }

        private void OnFightBeat(FmodBeatClock.BeatSnapshot beat)
        {
            FightUnitSlot attacker = fight != null && fight.UsesFrontHeroControls
                ? fight.ActiveEnemySlot
                : SlotAt(enemySlots, 1);
            bool attackBeat = fight != null && fight.UsesFrontHeroControls
                ? fight.IsEnemyAttackBeat(beat.GlobalBeat)
                : beat.Beat == 4;
            if (fight != null && fight.UsesFrontHeroControls)
            {
                attacker?.PlayScheduledAttackCountdown(
                    fight.GetEnemyBeatsUntilAttack(beat.GlobalBeat),
                    fight.EnemyAttackIntervalBeats,
                    true);
                if (attackBeat)
                    attacker?.PlayImmediateNormalAttack();
                PlayHeroCountdown(fight.SecondHero, beat.GlobalBeat);
                PlayHeroCountdown(fight.ThirdHero, beat.GlobalBeat);
            }
            else
                attacker?.Pulse(attackBeat ? 0.3f : 0.1f);
            telegraphTimer = attackBeat ? 0.45f : 0.16f;
        }

        private void OnHeroCalled(FightCombatController.HeroCallResult call)
        {
            if (fight != null && fight.UsesFrontHeroControls)
            {
                if (call.Command == FightInputRouter.HeroCommand.Damage &&
                    call.RhythmResult.Judgement == FmodRhythmJudge.Grade.Perfect)
                {
                    shieldTimer = 0.55f;
                    fight.FrontHero.UnitSlot?.Pulse();
                }

                return;
            }

            FightUnitSlot hero = SlotAt(heroSlots, HeroIndex(call.Command));
            hero?.Pulse();

            if (hero != null && call.RhythmResult.Judgement == FmodRhythmJudge.Grade.Perfect && !call.IsHeavyBeat)
                hero.PlayNormalAttack();

            if (call.SkillActivated && call.Command == FightInputRouter.HeroCommand.Tank)
                shieldTimer = 0.55f;
        }

        private void OnEnemyAttackResolved(FightCombatController.EnemyAttackResult attack)
        {
            FightUnitSlot attacker = fight != null && fight.UsesFrontHeroControls
                ? fight.ActiveEnemySlot
                : SlotAt(enemySlots, 1);
            if (fight == null || !fight.UsesFrontHeroControls)
                attacker?.PlayNormalAttack();
            if (attack.Blocked)
                shieldTimer = 0.85f;
        }

        private void PlayHeroCountdown(FightCombatController.HeroBeatSettings hero, long globalBeat)
        {
            if (fight == null || hero?.UnitSlot == null || hero.PlayerControlled)
                return;

            hero.UnitSlot.PlayScheduledAttackCountdown(
                fight.GetBeatsUntilScheduledAttack(globalBeat, hero.AttackIntervalBeats),
                hero.AttackIntervalBeats,
                false);
        }

        private static FightUnitSlot SlotAt(FightUnitSlot[] slots, int index)
        {
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        }

        private static int HeroIndex(FightInputRouter.HeroCommand command)
        {
            return command switch
            {
                FightInputRouter.HeroCommand.Tank => 0,
                FightInputRouter.HeroCommand.Support => 1,
                FightInputRouter.HeroCommand.Damage => 2,
                _ => -1
            };
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}
