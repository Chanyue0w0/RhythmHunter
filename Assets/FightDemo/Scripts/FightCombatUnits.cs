using UnityEngine;
namespace RhythmHunter.FightDemo
{
    /// <summary>Combat points are integers; a full heart or armor icon represents two points.</summary>
    public static class FightCombatUnits
    {
        public const float PointsPerIcon = 2f;
        public const float BossHpFloor = PointsPerIcon;
        // Half-up rounding, with a minimum of one for positive damage. Zero stays zero.
        public static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 0 : Mathf.Max(1, Mathf.Floor(value + .5f));
        public static float Positive(float value) => Mathf.Max(1, Amount(value));
    }
}
