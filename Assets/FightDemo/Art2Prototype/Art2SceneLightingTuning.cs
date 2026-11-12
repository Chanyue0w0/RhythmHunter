using System;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // Per-scene material copies: original art/material assets and focus-wall property blocks stay intact.
    [DisallowMultipleComponent]
    public sealed class Art2SceneLightingTuning : MonoBehaviour
    {
        [Serializable] public sealed class Target
        {
            public Renderer renderer;
            public bool foreground;
            public Texture normalMap;
            [NonSerialized] internal Material original, instance;
        }
        [Header("2. Scene Light Separation")]
        public bool enableSceneSeparation = true;
        [Range(0, 1)] public float backgroundAmbientMultiplier = .8f;
        [Range(0, 1)] public float foregroundAmbientMultiplier = .65f;
        [Header("4. Rock / Barricade Surface Lighting")]
        public bool enableSurfaceLighting = true;
        [Range(0, 2)] public float normalStrength = 1.2f;
        [Range(0, 1)] public float surfaceAmbientMultiplier = .8f;
        [Range(0, 1)] public float surfaceDiffuseWrap = .12f;
        public Target[] targets = Array.Empty<Target>();

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        private void Apply()
        {
            foreach (var target in targets)
            {
                if (target.renderer == null) continue;
                if (target.instance == null)
                {
                    target.original = target.renderer.sharedMaterial;
                    if (target.original == null || !target.original.HasProperty("_AmbientStrength")) continue;
                    target.instance = new Material(target.original) { name = target.original.name + " (Scene Lighting)", hideFlags = HideFlags.HideAndDontSave };
                    target.renderer.sharedMaterial = target.instance;
                }
                var original = target.original; var material = target.instance;
                bool surface = target.normalMap != null;
                float multiplier = surface ? (enableSurfaceLighting ? surfaceAmbientMultiplier : 1)
                    : enableSceneSeparation ? (target.foreground ? foregroundAmbientMultiplier : backgroundAmbientMultiplier) : 1;
                material.SetFloat("_AmbientStrength", original.GetFloat("_AmbientStrength") * multiplier);
                if (!surface) continue;
                material.SetFloat("_UseNormalMap", enableSurfaceLighting ? 1 : original.GetFloat("_UseNormalMap"));
                material.SetTexture("_NormalMap", enableSurfaceLighting ? target.normalMap : original.GetTexture("_NormalMap"));
                material.SetFloat("_NormalStrength", enableSurfaceLighting ? normalStrength : original.GetFloat("_NormalStrength"));
                material.SetFloat("_DiffuseFloor", enableSurfaceLighting ? surfaceDiffuseWrap : original.GetFloat("_DiffuseFloor"));
            }
        }
        private void OnDisable()
        {
            foreach (var target in targets)
            {
                if (target.instance == null) continue;
                if (target.renderer != null && target.renderer.sharedMaterial == target.instance) target.renderer.sharedMaterial = target.original;
                if (Application.isPlaying) Destroy(target.instance); else DestroyImmediate(target.instance);
                target.instance = null;
            }
        }
    }
}
