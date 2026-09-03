using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.RhythmArena
{
    public sealed class RhythmArenaRing : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RhythmClock rhythmClock;
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private EnemyPatternController enemyPattern;

        [Header("World-space Ring")]
        [SerializeField, Min(1f)] private float radius = 3.6f;
        [SerializeField] private Renderer[] ringSegments;
        [SerializeField] private Transform currentBeatCursor;
        [SerializeField] private Transform[] beatPoints;
        [SerializeField, Range(0.03f, 0.45f)] private float enemyMarkerHalfWidthBeats = 0.12f;

        [Header("Color Priority")]
        [SerializeField] private Color baseRingColor = Color.white;
        [SerializeField] private Color actionColor = new(0.42f, 0.42f, 0.42f, 1f);
        [SerializeField] private Color enemyAttackColor = new(1f, 0.08f, 0.08f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<double> upcomingAttacks = new();
        private MaterialPropertyBlock propertyBlock;

        public Renderer[] RingSegments => ringSegments;
        public Transform CurrentBeatCursor => currentBeatCursor;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (rhythmClock == null || !rhythmClock.IsReady)
                return;

            UpdateCursor();
            UpdateRingColors();
        }

        public void Configure(
            RhythmClock clock,
            PlayerCombatController combatPlayer,
            EnemyPatternController pattern,
            float ringRadius,
            Renderer[] segments,
            Transform cursor,
            Transform[] points)
        {
            rhythmClock = clock;
            player = combatPlayer;
            enemyPattern = pattern;
            radius = ringRadius;
            ringSegments = segments;
            currentBeatCursor = cursor;
            beatPoints = points;
        }

        private void UpdateCursor()
        {
            float phase = rhythmClock.LoopPhase;
            currentBeatCursor.localPosition = PhaseToLocalPosition(phase, radius);
            float pulse = 1f + 0.16f * (1f - Mathf.Abs(Mathf.Repeat(phase + 0.5f, 1f) - 0.5f) * 2f);
            currentBeatCursor.localScale = Vector3.one * (0.25f * pulse);
        }

        private void UpdateRingColors()
        {
            if (ringSegments == null || ringSegments.Length == 0)
                return;

            double now = rhythmClock.AbsoluteBeatTime;
            float nowPhase = rhythmClock.LoopPhase;
            float remainingActionBeats = player != null && player.IsBusy
                ? Mathf.Max(0f, (float)(player.ActionEndBeat - now))
                : 0f;

            upcomingAttacks.Clear();
            enemyPattern?.GetUpcomingAttackBeats(now, rhythmClock.BeatsPerLoop, upcomingAttacks);

            for (int i = 0; i < ringSegments.Length; i++)
            {
                float segmentPhase = i * rhythmClock.BeatsPerLoop / (float)ringSegments.Length;
                float futureDistance = Mathf.Repeat(segmentPhase - nowPhase, rhythmClock.BeatsPerLoop);
                bool insideAction = remainingActionBeats > 0f && futureDistance <= remainingActionBeats;
                bool insideEnemyAttack = IsEnemyAttackPhase(segmentPhase);
                Color color = insideEnemyAttack ? enemyAttackColor : insideAction ? actionColor : baseRingColor;
                SetRendererColor(ringSegments[i], color);
            }
        }

        private bool IsEnemyAttackPhase(float segmentPhase)
        {
            for (int i = 0; i < upcomingAttacks.Count; i++)
            {
                float attackPhase = Mathf.Repeat((float)upcomingAttacks[i], rhythmClock.BeatsPerLoop);
                float forward = Mathf.Repeat(segmentPhase - attackPhase, rhythmClock.BeatsPerLoop);
                float backward = Mathf.Repeat(attackPhase - segmentPhase, rhythmClock.BeatsPerLoop);
                if (Mathf.Min(forward, backward) <= enemyMarkerHalfWidthBeats)
                    return true;
            }

            return false;
        }

        private void SetRendererColor(Renderer target, Color color)
        {
            if (target == null)
                return;

            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            target.SetPropertyBlock(propertyBlock);
        }

        public static Vector3 PhaseToLocalPosition(float phase, float ringRadius)
        {
            float radians = phase * Mathf.PI * 0.5f;
            return new Vector3(Mathf.Sin(radians) * ringRadius, Mathf.Cos(radians) * ringRadius, 0f);
        }
    }
}
