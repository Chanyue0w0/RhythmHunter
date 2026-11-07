using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmHunter.FightDemo.Art2
{
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer)), DisallowMultipleComponent]
    public sealed class Art2SceneryContactShadows : MonoBehaviour
    {
        [Serializable]
        public struct Contact
        {
            [Tooltip("Foot point in source sprite pixels, measured from its top-left corner.")]
            public Vector2 footPixel;
            [Min(1)] public float widthPixels;
            [Min(.01f)] public float depth;
            public Vector2 worldOffset;
        }

        [SerializeField] private Material shadowMaterial;
        [SerializeField] private bool showContactShadows = true;
        [Range(0, 1)] [SerializeField] private float opacity = .28f;
        [SerializeField] private float groundHeight = .025f;
        [SerializeField] private Contact[] contacts = Array.Empty<Contact>();
        private SpriteRenderer source;
        private Mesh quad;
        private GameObject[] visuals;
        private MeshRenderer[] renderers;
        private MaterialPropertyBlock properties;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void OnEnable()
        {
            source = GetComponent<SpriteRenderer>();
            properties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            int count = contacts != null ? contacts.Length : 0;
            if (visuals == null || visuals.Length != count) Rebuild(count);
            bool visible = showContactShadows && shadowMaterial != null && source != null && source.enabled && source.sprite != null;
            for (int i = 0; i < count; i++)
            {
                visuals[i].SetActive(visible);
                if (!visible) continue;
                var sprite = source.sprite;
                var contact = contacts[i];
                Vector3 local = new Vector3(contact.footPixel.x - sprite.pivot.x,
                    sprite.rect.height - contact.footPixel.y - sprite.pivot.y, 0) / sprite.pixelsPerUnit;
                if (source.flipX) local.x = -local.x;
                if (source.flipY) local.y = -local.y;
                Vector3 foot = transform.TransformPoint(local);
                var shadow = visuals[i].transform;
                shadow.SetPositionAndRotation(new Vector3(foot.x + contact.worldOffset.x, groundHeight,
                    foot.z + contact.worldOffset.y), Quaternion.Euler(90, 0, 0));
                float width = contact.widthPixels / sprite.pixelsPerUnit * transform.TransformVector(Vector3.right).magnitude;
                shadow.localScale = new Vector3(Mathf.Max(.01f, width), Mathf.Max(.01f, contact.depth), 1);
                renderers[i].sharedMaterial = shadowMaterial;
                properties.SetColor(ColorId, new Color(.015f, .025f, .035f, opacity));
                renderers[i].SetPropertyBlock(properties);
            }
        }

        private void Rebuild(int count)
        {
            Cleanup();
            quad = new Mesh { name = "Art2 Scenery Contact Quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateBounds();
            visuals = new GameObject[count];
            renderers = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                // World-space quads stay flat on XZ regardless of card tilt/scale.
                var item = new GameObject(name + "_Contact_" + i) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(item, gameObject.scene);
                item.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                visuals[i] = item;
                renderers[i] = renderer;
            }
        }

        private void OnDisable() => Cleanup();
        private void Cleanup()
        {
            if (visuals != null) foreach (var item in visuals) Release(item);
            Release(quad);
            visuals = null;
            renderers = null;
            quad = null;
        }

        private static void Release(UnityEngine.Object item)
        {
            if (item == null) return;
            if (item is GameObject go) go.SetActive(false);
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
