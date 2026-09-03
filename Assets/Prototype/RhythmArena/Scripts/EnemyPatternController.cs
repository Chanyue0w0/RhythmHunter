using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.RhythmArena
{
    public sealed class EnemyPatternController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private CombatResolver combatResolver;

        [Header("Absolute Beat Pattern")]
        [SerializeField] private float[] attackBeatOffsets = { 2f, 3.5f, 5.25f, 7f, 9.5f, 11.75f };
        [SerializeField, Min(1f)] private float patternLoopBeats = 12f;
        [SerializeField, Min(1)] private int attackDamage = 1;

        private double[] scheduledAttackBeats = Array.Empty<double>();

        public double NextAttackBeat => FindNextAttackIndex() >= 0
            ? scheduledAttackBeats[FindNextAttackIndex()]
            : double.PositiveInfinity;

        private void Start()
        {
            ResetPattern();
        }

        private void Update()
        {
            if (rhythmClock == null || !rhythmClock.IsReady || combatResolver == null || !combatResolver.CombatActive)
                return;

            ResolveDueAttacks();
        }

        public void Configure(
            RhythmClock clock,
            CombatResolver resolver,
            float[] beatOffsets,
            float loopBeats,
            int damage)
        {
            rhythmClock = clock;
            combatResolver = resolver;
            attackBeatOffsets = beatOffsets ?? Array.Empty<float>();
            patternLoopBeats = Mathf.Max(1f, loopBeats);
            attackDamage = Mathf.Max(1, damage);
            ResetPattern();
        }

        public void ResetPattern()
        {
            scheduledAttackBeats = new double[attackBeatOffsets.Length];
            for (int i = 0; i < attackBeatOffsets.Length; i++)
                scheduledAttackBeats[i] = Math.Max(0.01, attackBeatOffsets[i]);
        }

        public void ShiftNextAttack(float delayBeats)
        {
            int index = FindNextAttackIndex();
            if (index < 0 || delayBeats <= 0f)
                return;

            scheduledAttackBeats[index] += delayBeats;
        }

        public void GetUpcomingAttackBeats(double now, float lookAheadBeats, List<double> results)
        {
            if (results == null)
                return;

            double end = now + lookAheadBeats;
            for (int i = 0; i < scheduledAttackBeats.Length; i++)
            {
                double attackBeat = scheduledAttackBeats[i];
                if (attackBeat >= now - 0.01 && attackBeat <= end + 0.01)
                    results.Add(attackBeat);
            }

            results.Sort();
        }

        private void ResolveDueAttacks()
        {
            int safety = 0;
            while (safety++ < scheduledAttackBeats.Length)
            {
                int index = FindNextAttackIndex();
                if (index < 0 || scheduledAttackBeats[index] > rhythmClock.AbsoluteBeatTime)
                    return;

                double resolvedBeat = scheduledAttackBeats[index];
                scheduledAttackBeats[index] += patternLoopBeats;
                combatResolver.ResolveEnemyAttack(resolvedBeat, attackDamage);

                if (!combatResolver.CombatActive)
                    return;
            }
        }

        private int FindNextAttackIndex()
        {
            if (scheduledAttackBeats == null || scheduledAttackBeats.Length == 0)
                return -1;

            int bestIndex = 0;
            double bestBeat = scheduledAttackBeats[0];
            for (int i = 1; i < scheduledAttackBeats.Length; i++)
            {
                if (scheduledAttackBeats[i] >= bestBeat)
                    continue;

                bestBeat = scheduledAttackBeats[i];
                bestIndex = i;
            }

            return bestIndex;
        }
    }
}
