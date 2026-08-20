using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototype
{
    public enum AquariumObstacleType
    {
        Rock,
        Wall,
        Decoration
    }

    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class AquariumObstacle : MonoBehaviour
    {
        [SerializeField] private AquariumObstacleType obstacleType = AquariumObstacleType.Rock;

        public AquariumObstacleType ObstacleType => obstacleType;

        public void Configure(AquariumObstacleType type)
        {
            obstacleType = type;
            PolygonCollider2D obstacleCollider = GetComponent<PolygonCollider2D>();
            obstacleCollider.isTrigger = false;
        }

        private void Reset()
        {
            GetComponent<PolygonCollider2D>().isTrigger = false;
        }

        private void OnValidate()
        {
            PolygonCollider2D obstacleCollider = GetComponent<PolygonCollider2D>();
            if (obstacleCollider != null)
                obstacleCollider.isTrigger = false;
        }
    }
}
