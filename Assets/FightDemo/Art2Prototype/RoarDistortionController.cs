using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    /// <summary>Presentation-only seconds, paused with battle; never owns a beat clock.</summary>
    public sealed class RoarDistortionController : MonoBehaviour
    {
        internal static readonly List<RoarDistortionController> Active = new();
        [SerializeField] private Transform origin;
        [SerializeField] private Vector3 originOffset;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(.01f)] private float duration = .5f;
        [SerializeField, Min(0)] private float distortionStrength = 20f;
        [Tooltip("Radius growth in screen heights per second.")]
        [SerializeField, Min(.01f)] private float expansionSpeed = 1.1f;
        [Tooltip("Radius measured in screen heights, independent of aspect ratio.")]
        [SerializeField, Min(.01f)] private float maxRadius = .48f;
        [SerializeField, Min(.001f)] private float waveWidth = .1f;
        [SerializeField, Min(0)] private float attackTime = .06f;
        [SerializeField, Min(0)] private float fadeOut = .15f;
        [Tooltip("Quantize distortion displacement to this many render pixels. Zero disables quantization.")]
        [SerializeField, Min(0)] private float pixelStep = 1f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool destroyOnComplete = true;
        [Tooltip("Optional old sprite visual, disabled by default on the new prefab.")]
        [SerializeField] private GameObject optionalSpriteEffect;
        [SerializeField] private bool showSpriteEffect;
        private FightCombatController fight;
        private float elapsed;
        private bool playing;
        public bool IsPlaying => playing;
        public Vector3 WorldOrigin => origin ? origin.TransformPoint(originOffset) : transform.TransformPoint(originOffset);
        public void Bind(FightCombatController controller, Transform anchor)
        {
            fight = controller;
            if (!origin) origin = anchor;
        }
        private void OnEnable() { if (playOnEnable && Application.isPlaying) Play(); }
        public void Play()
        {
            elapsed = 0; playing = true;
            if (!Active.Contains(this)) Active.Add(this);
            if (optionalSpriteEffect) optionalSpriteEffect.SetActive(showSpriteEffect);
            if (!fight) fight = FindFirstObjectByType<FightCombatController>();
            if (!targetCamera)
            {
                var rig = FindFirstObjectByType<Art2CameraController>();
                targetCamera = rig ? rig.GetComponent<Camera>() : null;
            }
        }
        public void Stop()
        {
            playing = false; Active.Remove(this);
            if (optionalSpriteEffect) optionalSpriteEffect.SetActive(false);
        }
        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            if (!playing || fight && (fight.IsPaused || fight.TimingCalibrationActive)) return;
            elapsed += Mathf.Max(0, seconds);
            if (elapsed < duration) return;
            Stop(); if (destroyOnComplete && Application.isPlaying) Destroy(gameObject);
        }
        internal bool TryParameters(Camera view, out Vector4 centerRadius, out Vector4 wave)
        {
            centerRadius = wave = Vector4.zero;
            if (!playing || !view || view != targetCamera || !view.isActiveAndEnabled) return false;
            Vector3 position = view.WorldToViewportPoint(WorldOrigin);
            if (position.z <= 0) return false;
            float strength = Mathf.Min(attackTime > 0 ? elapsed / attackTime : 1,
                fadeOut > 0 ? (duration - elapsed) / fadeOut : 1);
            centerRadius = new Vector4(position.x, position.y, Mathf.Min(maxRadius, elapsed * expansionSpeed), waveWidth);
            wave = new Vector4(distortionStrength * Mathf.Clamp01(strength), pixelStep, 0, 0);
            return wave.x > 0 && elapsed < duration;
        }
    }
}
