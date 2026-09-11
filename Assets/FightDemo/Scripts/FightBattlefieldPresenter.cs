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
        private FightCombatController subscribedFight;

        public FightUnitSlot[] EnemySlots => enemySlots;
        public FightUnitSlot[] HeroSlots => heroSlots;

        public void Configure(
            FightCombatController controller,
            FightUnitSlot[] enemies,
            FightUnitSlot[] heroes,
            SpriteRenderer shield,
            SpriteRenderer telegraph)
        {
            Unsubscribe();
            fight = controller;
            enemySlots = enemies;
            heroSlots = heroes;
            tankShield = shield;
            enemyTelegraph = telegraph;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || fight == null)
                return;

            Unsubscribe();
            subscribedFight = fight;
            subscribedFight.FightBeat += OnFightBeat;
            subscribedFight.HeroCalled += OnHeroCalled;
            subscribedFight.EnemyAttackResolved += OnEnemyAttackResolved;
            subscribedFight.RosterRebuilt += RefreshFrontHeroPresentation;
        }

        private void Start()
        {
            SetAlpha(tankShield, 0f);
            SetAlpha(enemyTelegraph, 0f);
            RefreshFrontHeroPresentation();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (subscribedFight == null)
                return;

            subscribedFight.FightBeat -= OnFightBeat;
            subscribedFight.HeroCalled -= OnHeroCalled;
            subscribedFight.EnemyAttackResolved -= OnEnemyAttackResolved;
            subscribedFight.RosterRebuilt -= RefreshFrontHeroPresentation;
            subscribedFight = null;
        }

        private void RefreshFrontHeroPresentation()
        {
            if (fight == null || !fight.UsesFrontHeroControls)
                return;

            fight.FrontHero.UnitSlot?.SetRoleLabel("PLAYER  •  Q/X LIGHT  W/Y HEAVY  E/B GUARD");
            fight.SecondHero.UnitSlot?.SetRoleLabel($"AUTO  •  EVERY {fight.SecondHero.AttackIntervalBeats} BEATS");
            fight.ThirdHero.UnitSlot?.SetRoleLabel($"AUTO  •  EVERY {fight.ThirdHero.AttackIntervalBeats} BEATS");
            SetHealthVisibility(heroSlots, fight.HealthSystemEnabled);
            SetHealthVisibility(enemySlots, fight.HealthSystemEnabled);
        }

        private static void SetHealthVisibility(FightUnitSlot[] slots, bool visible)
        {
            if (slots == null)
                return;
            foreach (FightUnitSlot slot in slots)
                slot?.SetHealthDisplayVisible(visible);
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
                PlayHeroCountdown(fight.SecondHero, beat.GlobalBeat);
                PlayHeroCountdown(fight.ThirdHero, beat.GlobalBeat);
            }
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
                }

                return;
            }

            FightUnitSlot hero = SlotAt(heroSlots, HeroIndex(call.Command));

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
