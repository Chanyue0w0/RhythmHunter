using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemo
{
    /// <summary>Small reusable off-screen particle stage for an Overlay Canvas.</summary>
    public sealed class FightUiParticleBurst : MonoBehaviour
    {
        private GameObject stage;
        private Camera view;
        private RenderTexture texture;
        private RawImage image;
        private readonly GameObject[] instances = new GameObject[4];
        private readonly ParticleSystem[][] particles = new ParticleSystem[4][];
        private readonly float[] remaining = new float[4];
        private int next;
        private float lifetime;

        public void Initialize(GameObject prefab, Vector2 size, float halfHeight, float seconds)
        {
            lifetime = Mathf.Max(.1f, seconds);
            image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            texture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32) { name = "Stone Particle Texture" };
            texture.Create(); image.texture = texture;
            stage = new GameObject("Stone UI Particle Stage");
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.transform.position = new Vector3(10000, 10000, 10000);
            view = stage.AddComponent<Camera>();
            view.orthographic = true; view.orthographicSize = Mathf.Max(.1f, halfHeight);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.clear;
            view.nearClipPlane = .1f; view.farClipPlane = 20; view.cullingMask = 1 << 31;
            view.allowHDR = false; view.allowMSAA = false; view.useOcclusionCulling = false;
            view.targetTexture = texture;
            var cameraData = view.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(2); // Existing Art2 forward renderer; no project renderer changes.
            cameraData.renderPostProcessing = false; cameraData.renderShadows = false;
            cameraData.volumeLayerMask = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = Instantiate(prefab, stage.transform);
                instance.transform.localPosition = Vector3.forward * 5;
                instance.transform.localRotation = Quaternion.identity;
                foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var effect in instance.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true)) effect.enabled = false;
                particles[i] = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particles[i])
                {
                    var main = ps.main; main.loop = false; main.stopAction = ParticleSystemStopAction.None;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer) renderer.alignment = ParticleSystemRenderSpace.View;
                }
                instance.SetActive(false); instances[i] = instance;
            }
            image.enabled = false; view.enabled = false;
        }

        public void Play()
        {
            int index = next++ % instances.Length;
            instances[index].SetActive(true);
            foreach (var ps in particles[index]) { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(false); }
            remaining[index] = lifetime;
            image.enabled = true; view.enabled = true;
        }
        private void Update()
        {
            bool active = false;
            for (int i = 0; i < instances.Length; i++)
            {
                if (remaining[i] <= 0) continue;
                remaining[i] -= Time.unscaledDeltaTime;
                if (remaining[i] <= 0) instances[i].SetActive(false); else active = true;
            }
            if (view) view.enabled = active;
            if (image) image.enabled = active;
        }
        private void OnDisable()
        {
            for (int i = 0; i < instances.Length; i++) { remaining[i] = 0; if (instances[i]) instances[i].SetActive(false); }
            if (view) view.enabled = false;
            if (image) image.enabled = false;
        }
        private void OnDestroy()
        {
            if (view) view.targetTexture = null;
            if (stage) { if (Application.isPlaying) Destroy(stage); else DestroyImmediate(stage); }
            if (texture) { texture.Release(); if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }
    }
}
