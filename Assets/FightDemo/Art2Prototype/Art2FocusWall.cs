using System.Collections.Generic;
using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Art2FocusWall : MonoBehaviour
    {
        [Tooltip("Only these environment renderers are affected. Actors and UI are excluded.")]
        [SerializeField] private Renderer[] targets;
        [Tooltip("Additional hierarchies, including runtime torch halos and particles.")]
        [SerializeField] private Transform[] additionalRoots;
        [Header("Background Detail Blur")]
        [Tooltip("Hierarchies that need more visible blur, without increasing fog.")]
        [SerializeField] private Transform[] emphasizedRoots;
        [SerializeField, Range(1, 3)] private float detailBlurMultiplier = 1.5f;
        [Tooltip("Fraction of Transition Depth used for these objects' blur only.")]
        [SerializeField, Range(.05f, 1)] private float detailBlurTransitionScale = .2f;
        private readonly List<Renderer> extraRenderers = new();
        [SerializeField, Min(.01f)] private float transitionDepth = 3;
        [SerializeField, Range(0, 2)] private float blurPixels = 1.2f;
        [SerializeField, Range(0, 1)] private float fogDensity = .16f;
        [SerializeField] private Color fogColor = new(.12f, .18f, .22f, 1);
        [Tooltip("Scene gizmo dimensions only. The effect plane extends infinitely sideways/upwards.")]
        [SerializeField] private Vector2 guideSize = new(24, 10);
        private readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new();
        private MaterialPropertyBlock block;

        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        private void Apply()
        {
            block ??= new MaterialPropertyBlock();
            var normal = transform.forward;
            var plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, transform.position));
            extraRenderers.Clear();
            if (targets != null) extraRenderers.AddRange(targets);
            if (additionalRoots != null)
                foreach (var root in additionalRoots)
                    if (root) { root.GetComponentsInChildren(true, rootRenderers); extraRenderers.AddRange(rootRenderers); }
            foreach (var target in extraRenderers)
            {
                if (!target) continue;
                if (!originals.ContainsKey(target))
                {
                    var original = new MaterialPropertyBlock();
                    target.GetPropertyBlock(original);
                    originals.Add(target, original);
                }
                target.GetPropertyBlock(block);
                block.SetVector("_FocusWallPlane", plane);
                block.SetVector("_FocusWallSettings", new Vector4(1, Mathf.Max(.01f, transitionDepth), blurPixels, fogDensity));
                block.SetColor("_FocusWallColor", fogColor);
                bool emphasized = false;
                if (emphasizedRoots != null)
                    foreach (var root in emphasizedRoots)
                        if (root && target.transform.IsChildOf(root)) { emphasized = true; break; }
                block.SetVector("_FocusWallBlurProfile", emphasized
                    ? new Vector4(detailBlurMultiplier, detailBlurTransitionScale, 0, 0)
                    : new Vector4(1, 1, 0, 0));
                target.SetPropertyBlock(block);
            }
        }
        private readonly List<Renderer> rootRenderers = new();
        private void OnDisable()
        {
            foreach (var entry in originals) if (entry.Key) entry.Key.SetPropertyBlock(entry.Value);
            originals.Clear();
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(0, guideSize.y / 2, 0), new Vector3(guideSize.x, guideSize.y, 0));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * transitionDepth);
            Gizmos.color = new Color(.3f, .7f, 1, .4f);
            Gizmos.DrawWireCube(new Vector3(0, guideSize.y / 2, transitionDepth), new Vector3(guideSize.x, guideSize.y, 0));
        }
    }
}
