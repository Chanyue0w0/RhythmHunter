using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Enemy Damage Numbers")]
        [SerializeField] private bool showEnemyDamageNumbers = true;
        [Tooltip("3D 場景指定相機，使傷害數字朝向畫面，偏移與上飄沿畫面座標。留空保留 2D 行為。")]
        [SerializeField] private Camera damageNumberCamera;
        [Tooltip("Digit prefabs in order: 0 through 9.")]
        [SerializeField] private GameObject[] damageDigitPrefabs = new GameObject[10];
        [SerializeField] private bool showHealingNumbers = true;
        [Tooltip("Green healing digits in order: 0 through 9. Uses separate glyph pools.")]
        [SerializeField] private GameObject[] healingDigitPrefabs = new GameObject[10];
        [Tooltip("Optional matching decimal point. Leave empty for the legacy white dot.")]
        [SerializeField] private GameObject damageDecimalPrefab;
        [SerializeField] private Vector3 damageNumberOffset = new Vector3(0, .25f, -.5f);
        [SerializeField, Min(.1f)] private float damageNumberLifetime = .8f;
        [SerializeField, Min(.01f)] private float damageNumberScale = 2f;
        [SerializeField, Min(.01f)] private float damageDigitSpacing = .3f;
        [SerializeField, Min(0)] private float damageNumberRise = .6f;
        [SerializeField] private int damageNumberSortingOrder = 100;
        [SerializeField] private bool showPartyDamageNumbers = true;
        [SerializeField, Min(0)] private float damageNumberHorizontalTravel = .65f;
        [SerializeField, Min(0)] private float damageNumberJumpHeight = .35f;
        [Tooltip("Camera-space stagger range for consecutive popups.")]
        [SerializeField] private Vector2 damageNumberStagger = new Vector2(.18f, .12f);
        [Tooltip("Preferred camera-plane separation. Crowded groups stay within the random range instead of moving upward.")]
        [SerializeField, Min(0)] private float damageNumberMinimumSeparation = .45f;
        [Tooltip("Additional random camera-space spawn range. Does not change damage timing.")]
        [SerializeField] private Vector2 damageNumberRandomSpread = new Vector2(.25f, .06f);
        private readonly System.Random damageNumberRandom = new System.Random();
        [Header("Damage Number Pool")]
        [SerializeField, Min(0)] private int damageNumberPrewarm = 8;
        [SerializeField, Min(0)] private int damageGlyphPrewarmPerDigit = 4;
        [Tooltip("At capacity, recycle the oldest popup. Does not limit actual damage.")]
        [SerializeField, Min(1)] private int damageNumberMaxActive = 64;
        [Tooltip("Maximum idle glyphs retained for each digit, including the decimal point.")]
        [SerializeField, Min(1)] private int damageGlyphMaxCachedPerDigit = 64;

        private Transform damagePoolRoot, damagePoolStorage;
        private Sprite damageDotSprite;
        private Gradient damageFade;
        private readonly Stack<FightDamageNumber> idleDamageNumbers = new();
        private readonly List<FightDamageNumber> activeDamageNumbers = new();
        private Stack<FightDamageNumber.Glyph>[] damageGlyphPools;
        public int ActiveDamageNumberCount => activeDamageNumbers.Count;
        public int CreatedDamageNumberCount { get; private set; }
        public int CreatedDamageGlyphCount { get; private set; }

        private void EnsureDamageNumberPool()
        {
            if (damagePoolRoot != null || (!showEnemyDamageNumbers && !showPartyDamageNumbers && !showHealingNumbers)) return;
            var root = new GameObject("DamageNumberPool");
            root.transform.SetParent(transform, false);
            damagePoolRoot = root.transform;
            var storage = new GameObject("Inactive");
            storage.transform.SetParent(damagePoolRoot, false);
            storage.SetActive(false);
            damagePoolStorage = storage.transform;
            damageFade = new Gradient();
            damageFade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .55f), new GradientAlphaKey(0, 1) });
            damageGlyphPools = new Stack<FightDamageNumber.Glyph>[21];
            for (int d = 0; d < 21; d++)
            {
                damageGlyphPools[d] = new Stack<FightDamageNumber.Glyph>();
                if (d != 10 && DigitPrefab(d) == null) continue;
                for (int i = 0; i < Mathf.Clamp(damageGlyphPrewarmPerDigit, 0, Mathf.Max(1, damageGlyphMaxCachedPerDigit)); i++)
                    damageGlyphPools[d].Push(CreateDamageGlyph(d));
            }
            for (int i = 0; i < Mathf.Clamp(damageNumberPrewarm, 0, Mathf.Max(1, damageNumberMaxActive)); i++)
                idleDamageNumbers.Push(CreateDamageNumber());
        }

        private FightDamageNumber CreateDamageNumber()
        {
            var root = new GameObject("DamageNumber");
            root.transform.SetParent(damagePoolStorage, false);
            root.SetActive(false);
            CreatedDamageNumberCount++;
            return root.AddComponent<FightDamageNumber>();
        }

        private GameObject DigitPrefab(int key)
        {
            var prefabs = key >= 11 ? healingDigitPrefabs : damageDigitPrefabs;
            int digit = key >= 11 ? key - 11 : key;
            return prefabs != null && digit >= 0 && digit < prefabs.Length ? prefabs[digit] : null;
        }

        private FightDamageNumber.Glyph CreateDamageGlyph(int digit)
        {
            var glyph = new FightDamageNumber.Glyph { digit = digit };
            if (digit == 10 && damageDecimalPrefab == null)
            {
                glyph.root = new GameObject("DecimalPoint");
                glyph.root.transform.SetParent(damagePoolStorage, false);
                glyph.root.transform.localScale = Vector3.one * .055f;
                glyph.point = glyph.root.AddComponent<SpriteRenderer>();
                if (damageDotSprite == null)
                    damageDotSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                glyph.point.sprite = damageDotSprite;
            }
            else glyph.root = Instantiate(digit == 10 ? damageDecimalPrefab : DigitPrefab(digit), damagePoolStorage);
            glyph.sprites = glyph.root.GetComponentsInChildren<SpriteRenderer>(true);
            glyph.spriteColors = new Color[glyph.sprites.Length];
            for (int i = 0; i < glyph.sprites.Length; i++) glyph.spriteColors[i] = glyph.sprites[i].color;
            glyph.root.SetActive(false);
            glyph.scale = glyph.root.transform.localScale;
            glyph.particles = glyph.root.GetComponentsInChildren<ParticleSystem>(true);
            glyph.renderers = new ParticleSystemRenderer[glyph.particles.Length];
            for (int i = 0; i < glyph.particles.Length; i++)
            {
                var particles = glyph.particles[i];
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.useAutoRandomSeed = false;
                var main = particles.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.loop = false;
                main.startDelay = 0;
                main.simulationSpeed = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startSpeed = 0;
                main.gravityModifier = 0;
                var shape = particles.shape; shape.enabled = false;
                var emission = particles.emission; emission.enabled = false;
                var velocity = particles.velocityOverLifetime; velocity.enabled = false;
                var force = particles.forceOverLifetime; force.enabled = false;
                var noise = particles.noise; noise.enabled = false;
                var color = particles.colorOverLifetime; color.enabled = true; color.color = damageFade;
                glyph.renderers[i] = particles.GetComponent<ParticleSystemRenderer>();
            }
            CreatedDamageGlyphCount++;
            return glyph;
        }

        internal void ReturnDamageGlyph(FightDamageNumber.Glyph glyph)
        {
            foreach (var particles in glyph.particles) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            glyph.root.SetActive(false);
            glyph.root.transform.SetParent(damagePoolStorage, false);
            var pool = damageGlyphPools[glyph.digit];
            if (pool.Count < Mathf.Max(1, damageGlyphMaxCachedPerDigit)) pool.Push(glyph);
            else if (Application.isPlaying) Destroy(glyph.root); else DestroyImmediate(glyph.root);
        }

        internal void ReturnDamageNumber(FightDamageNumber popup)
        {
            if (popup == null || !popup.Leased) return;
            popup.ReturnGlyphs();
            activeDamageNumbers.Remove(popup);
            popup.transform.SetParent(damagePoolStorage, false);
            idleDamageNumbers.Push(popup);
        }

        private void ResetDamageNumberPool()
        {
            while (activeDamageNumbers.Count > 0) ReturnDamageNumber(activeDamageNumbers[activeDamageNumbers.Count - 1]);
        }

        private void OnDestroy()
        {
            if (qteRingMaterial != null)
            {
                if (Application.isPlaying) Destroy(qteRingMaterial); else DestroyImmediate(qteRingMaterial);
            }
            // All pooled GameObjects are controller children and die with their scene.
            if (damageDotSprite != null)
            {
                if (Application.isPlaying) Destroy(damageDotSprite); else DestroyImmediate(damageDotSprite);
            }
        }

        private Vector3 ChooseDamagePopupPosition(Vector3 origin)
        {
            Vector3 right = damageNumberCamera != null ? damageNumberCamera.transform.right : Vector3.right;
            Vector3 up = damageNumberCamera != null ? damageNumberCamera.transform.up : Vector3.up;
            float separation = Mathf.Max(0, damageNumberMinimumSeparation);
            float spreadX = Mathf.Abs(damageNumberStagger.x) + Mathf.Abs(damageNumberRandomSpread.x);
            float spreadY = Mathf.Abs(damageNumberStagger.y) + Mathf.Abs(damageNumberRandomSpread.y);
            Vector3 best = origin;
            float bestDistance = -1;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 candidate = origin + right * ((float)damageNumberRandom.NextDouble() * 2 - 1) * spreadX
                    + up * ((float)damageNumberRandom.NextDouble() * 2 - 1) * spreadY;
                float nearest = float.PositiveInfinity;
                foreach (var existing in activeDamageNumbers)
                {
                    Vector3 delta = candidate - existing.transform.position;
                    float x = Vector3.Dot(delta, right), y = Vector3.Dot(delta, up);
                    nearest = Mathf.Min(nearest, x * x + y * y);
                }
                if (nearest >= separation * separation) return candidate;
                if (nearest > bestDistance) { bestDistance = nearest; best = candidate; }
            }
            // Crowding may overlap slightly, but must never push feedback far from the target.
            return best;
        }

        internal void ShowEnemyDamageNumber(FightUnitSlot target, float appliedDamage)
        {
            if (target == null || target.Team != FightUnitSlot.UnitTeam.Enemy) return;
            ShowDamageNumber(target, appliedDamage, false);
        }

        internal void ShowHealingNumber(FightUnitSlot target, float amount)
        {
            ShowCombatNumber(target, amount, target != null && target.Team != FightUnitSlot.UnitTeam.Enemy, true);
        }

        private void ShowDamageNumber(FightUnitSlot target, float appliedDamage, bool party) =>
            ShowCombatNumber(target, appliedDamage, party, false);

        private void ShowCombatNumber(FightUnitSlot target, float appliedDamage, bool party, bool healing)
        {
            if ((healing ? !showHealingNumbers : party ? !showPartyDamageNumbers : !showEnemyDamageNumbers) || target == null ||
                appliedDamage <= 0 || float.IsNaN(appliedDamage) || float.IsInfinity(appliedDamage)) return;
            string text = FightCombatUnits.Amount(appliedDamage).ToString("0", CultureInfo.InvariantCulture);
            foreach (char digit in text)
                if (DigitPrefab(digit - '0' + (healing ? 11 : 0)) == null) return;
            EnsureDamageNumberPool();
            while (activeDamageNumbers.Count >= Mathf.Max(1, damageNumberMaxActive)) ReturnDamageNumber(activeDamageNumbers[0]);
            Vector3 head = target.ImpactEffectAnchor != null ? target.ImpactEffectAnchor.position : target.transform.position;
            var renderer = target.ActorInstance != null ? target.ActorInstance.GetComponentInChildren<SpriteRenderer>() : null;
            if (renderer != null && renderer.sprite != null)
                head = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z);
            if (damageNumberCamera != null)
            {
                if (target.HeadAnchor != null) head = target.HeadAnchor.position;
                else if (renderer != null && renderer.sprite != null)
                {
                    Vector3 top = renderer.sprite.bounds.center + Vector3.up * renderer.sprite.bounds.extents.y;
                    if (renderer.flipX) top.x = -top.x;
                    if (renderer.flipY) top.y = -top.y;
                    head = renderer.transform.TransformPoint(top);
                }
            }
            var popup = idleDamageNumbers.Count > 0 ? idleDamageNumbers.Pop() : CreateDamageNumber();
            popup.transform.SetParent(damagePoolRoot, false);
            Vector3 offset = damageNumberCamera != null ? damageNumberCamera.transform.TransformDirection(damageNumberOffset) : damageNumberOffset;
            Vector3 spawn = ChooseDamagePopupPosition(head + offset);
            popup.Begin(this, spawn, Mathf.Max(.01f, damageNumberScale), Mathf.Max(.1f, damageNumberLifetime), damageNumberRise, damageNumberCamera);
            popup.SetMotion((party ? 1 : -1) * damageNumberHorizontalTravel, damageNumberJumpHeight);
            activeDamageNumbers.Add(popup);
            // Compact active order avoids both reuse collisions and an ever-growing sorting order.
            int baseOrder = Mathf.Clamp(damageNumberSortingOrder, -32768, 32767 - activeDamageNumbers.Count);
            for (int i = 0; i < activeDamageNumbers.Count; i++) activeDamageNumbers[i].SetSortingOrder(baseOrder + i);
            uint seed = (uint)Random.Range(1, int.MaxValue);
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0' + (healing ? 11 : 0);
                var pool = damageGlyphPools[digit];
                popup.Add(pool.Count > 0 ? pool.Pop() : CreateDamageGlyph(digit),
                    (i - (text.Length - 1) * .5f) * damageDigitSpacing, damageNumberSortingOrder, seed);
            }
        }
    }
}
