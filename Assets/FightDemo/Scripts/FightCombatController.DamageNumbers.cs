using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightCombatController
    {
        [Header("Enemy Damage Numbers")]
        [SerializeField] private bool showEnemyDamageNumbers = true;
        [Tooltip("Digit prefabs in order: 0 through 9.")]
        [SerializeField] private GameObject[] damageDigitPrefabs = new GameObject[10];
        [SerializeField] private Vector3 damageNumberOffset = new Vector3(0, .25f, -.5f);
        [SerializeField, Min(.1f)] private float damageNumberLifetime = .8f;
        [SerializeField, Min(.01f)] private float damageNumberScale = 1f;
        [SerializeField, Min(.01f)] private float damageDigitSpacing = .3f;
        [SerializeField, Min(0)] private float damageNumberRise = .6f;
        [SerializeField] private int damageNumberSortingOrder = 100;
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
            if (damagePoolRoot != null || !showEnemyDamageNumbers) return;
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
            damageGlyphPools = new Stack<FightDamageNumber.Glyph>[11];
            for (int d = 0; d < 11; d++)
            {
                damageGlyphPools[d] = new Stack<FightDamageNumber.Glyph>();
                if (d != 10 && (damageDigitPrefabs == null || d >= damageDigitPrefabs.Length || damageDigitPrefabs[d] == null)) continue;
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

        private FightDamageNumber.Glyph CreateDamageGlyph(int digit)
        {
            var glyph = new FightDamageNumber.Glyph { digit = digit };
            if (digit == 10)
            {
                glyph.root = new GameObject("DecimalPoint");
                glyph.root.transform.SetParent(damagePoolStorage, false);
                glyph.root.transform.localScale = Vector3.one * .055f;
                glyph.point = glyph.root.AddComponent<SpriteRenderer>();
                if (damageDotSprite == null)
                    damageDotSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                glyph.point.sprite = damageDotSprite;
            }
            else glyph.root = Instantiate(damageDigitPrefabs[digit], damagePoolStorage);
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
            // All pooled GameObjects are controller children and die with their scene.
            if (damageDotSprite != null)
            {
                if (Application.isPlaying) Destroy(damageDotSprite); else DestroyImmediate(damageDotSprite);
            }
        }

        internal void ShowEnemyDamageNumber(FightUnitSlot target, float appliedDamage)
        {
            if (!showEnemyDamageNumbers || target == null || target.Team != FightUnitSlot.UnitTeam.Enemy ||
                appliedDamage <= 0 || float.IsNaN(appliedDamage) || float.IsInfinity(appliedDamage)) return;
            string text = appliedDamage.ToString("0.#", CultureInfo.InvariantCulture);
            foreach (char digit in text)
                if (digit != '.' && (damageDigitPrefabs == null || digit - '0' >= damageDigitPrefabs.Length || damageDigitPrefabs[digit - '0'] == null)) return;
            EnsureDamageNumberPool();
            while (activeDamageNumbers.Count >= Mathf.Max(1, damageNumberMaxActive)) ReturnDamageNumber(activeDamageNumbers[0]);
            Vector3 head = target.ImpactEffectAnchor != null ? target.ImpactEffectAnchor.position : target.transform.position;
            var renderer = target.ActorInstance != null ? target.ActorInstance.GetComponentInChildren<SpriteRenderer>() : null;
            if (renderer != null && renderer.sprite != null)
                head = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z);
            var popup = idleDamageNumbers.Count > 0 ? idleDamageNumbers.Pop() : CreateDamageNumber();
            popup.transform.SetParent(damagePoolRoot, false);
            popup.Begin(this, head + damageNumberOffset, Mathf.Max(.01f, damageNumberScale), Mathf.Max(.1f, damageNumberLifetime), damageNumberRise);
            activeDamageNumbers.Add(popup);
            uint seed = (uint)Random.Range(1, int.MaxValue);
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] == '.' ? 10 : text[i] - '0';
                var pool = damageGlyphPools[digit];
                popup.Add(pool.Count > 0 ? pool.Pop() : CreateDamageGlyph(digit),
                    (i - (text.Length - 1) * .5f) * damageDigitSpacing, damageNumberSortingOrder, seed);
            }
        }
    }
}
