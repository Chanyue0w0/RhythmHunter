using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightHitFlashSetup
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Enemies = { "Goblin_Normal", "Goblin_Killer", "Goblin_Giant", "Goblin_King" };
        static FightHitFlashSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            const string request = "Temp/FightHitFlash.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Setup(); Validate(); }
            catch (Exception e) { File.WriteAllText("Temp/FightHitFlash.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Setup Enemy Hit Flash")]
        private static void Setup()
        {
            foreach (string name in Enemies)
            {
                string path = "Assets/FightDemo/Prefabs/ArtBattle/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<FightHitFlashController>()) continue;
                    var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                    Require(animator && animator.TargetRenderer, name + " missing animation renderer");
                    var flash = root.AddComponent<FightHitFlashController>();
                    flash.Configure(new[] { animator.TargetRenderer });
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        private static void Require(bool success, string message) { if (!success) throw new Exception(message); }
        private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        [MenuItem("Rhythm Hunter/Art2/Validate Enemy Hit Flash")]
        private static void Validate()
        {
            var shader = Shader.Find("RhythmHunter/Art2/SpriteCardLit");
            Require(shader && !ShaderUtil.ShaderHasError(shader), "SpriteCardLit shader error");
            var material = new Material(shader);
            try
            {
                foreach (string name in Enemies) ValidateEnemy(name, material);
                ValidateGpu(material);
                File.WriteAllText("Temp/FightHitFlash.result", "PASS: four enemy prefabs; Hit cannot restart; attack protection; death continues active Hit; confirmed damage and zero-damage gating; HP unchanged by flash; GPU white over black pixels, alpha preservation, right-to-left and flip, per-instance isolation, repeat/disable cleanup and unchanged shared material.");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
        private static void ValidateEnemy(string name, Material material)
        {
            var root = new GameObject("Hit validation " + name) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var slot = root.AddComponent<FightUnitSlot>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/" + name + ".prefab");
                slot.SpawnCharacter(prefab.GetComponent<FightCharacterDefinition>(), null);
                var animator = slot.CombatAnimator;
                Require(animator, name + " animator");
                var flash = slot.ActorInstance.GetComponent<FightHitFlashController>();
                Require(flash, name + " flash component");
                animator.TargetRenderer.sharedMaterial = material;
                Set(flash, "flashEnabled", true);
                Invoke(flash, "OnDisable"); Invoke(flash, "OnEnable");
                slot.TakeDamage(0); Require(!flash.IsFlashing, "Zero damage flashed");
                float hp = slot.CurrentHp;
                slot.TakeDamage(1); Require(flash.IsFlashing && Mathf.Approximately(slot.CurrentHp, hp - 1), "Confirmed hit integration: " + name);
                flash.ClearFlash();
                bool PlayHit() => slot.PlayCombatAnimation(FightCharacterCombatAnimator.CombatAnimation.Hit, null, null, null);
                Require(PlayHit(), name + " Hit missing");
                Invoke(animator, "AdvancePlayback", .1f);
                float progress = animator.ActiveSequenceElapsedBeats;
                Require(!PlayHit() && Mathf.Approximately(progress, animator.ActiveSequenceElapsedBeats), name + " Hit restarted");
                Invoke(slot, "BeginDeathPresentation");
                Require(Mathf.Approximately(progress, animator.ActiveSequenceElapsedBeats), "Death restarted active reaction");
                Invoke(animator, "AdvancePlayback", 10f);
                Require(!PlayHit() && slot.HitReactionCooldownRemaining > 0, "Hit skipped post-animation cooldown");
                Invoke(slot, "AdvanceHitReactionCooldown", .49f);
                Require(!PlayHit(), "Hit cooldown ended before two beats");
                Invoke(slot, "AdvanceHitReactionCooldown", .52f);
                Require(PlayHit(), "Hit did not unlock after two beats");
                animator.InterruptPlayback();
                Require(animator.Play(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, onFrameEffect: _ => { }), name + " attack missing");
                Require(!PlayHit() && animator.ActiveAnimation == FightCharacterCombatAnimator.CombatAnimation.NormalAttack, "Hit interrupted attack");
                slot.TakeDamage(slot.CurrentHp + 1000);
                Require(!flash.IsFlashing, "Death did not clear flash");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void ValidateGpu(Material material)
        {
            var root = new GameObject("Hit Flash GPU") { hideFlags = HideFlags.HideAndDontSave };
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var pixels = new Color[256];
            for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++) pixels[y * 16 + x] = new Color(0, 0, 0, x == 11 ? .5f : 1);
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 8);
            var rt = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var old = RenderTexture.active;
            try
            {
                material.SetFloat("_AmbientStrength", 0); material.SetFloat("_LightInfluence", 0);
                material.SetFloat("_EmissionStrength", 1);
                var camera = new GameObject("Flash camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 2; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 31; camera.targetTexture = rt;
                var pipeline = (UniversalRenderPipelineAsset)(QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline);
                int index = -1;
                for (int i = 0; i < pipeline.rendererDataList.Length; i++)
                    if (pipeline.rendererDataList[i] && pipeline.rendererDataList[i].name == "Art2ForwardPlusRenderer") index = i;
                Require(index >= 0, "Art2 renderer missing"); camera.GetUniversalAdditionalCameraData().SetRenderer(index);
                SpriteRenderer MakeSprite(string name, Vector3 position)
                {
                    var sr = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    sr.transform.SetParent(root.transform); sr.transform.localPosition = position; sr.gameObject.layer = 31;
                    sr.sprite = sprite; sr.sharedMaterial = material; return sr;
                }
                var renderer = MakeSprite("Flashing", Vector3.zero);
                var other = MakeSprite("Unaffected", new Vector3(1.5f, 0, 0));
                var flash = renderer.gameObject.AddComponent<FightHitFlashController>(); flash.Configure(new[] { renderer }, camera);
                var block = new MaterialPropertyBlock(); block.SetFloat("_EntranceReveal", .7f); renderer.SetPropertyBlock(block);
                Color[] before = Render();
                Set(flash, "flashEnabled", true);
                Set(flash, "flashStrength", 1f);
                flash.TriggerFlash(); Color[] white = Render();
                Require(white[32 * 64 + 32].r > .6f && before[32 * 64 + 32].r < .01f, "Black sprite did not turn white");
                for (int i = 0; i < before.Length; i++) Require(Mathf.Abs(before[i].a - white[i].a) < .01f, "Alpha changed");
                Require(white[32 * 64 + 56].r < .01f, "Shared material leaked to second enemy");
                renderer.GetPropertyBlock(block); Require(Mathf.Approximately(block.GetFloat("_EntranceReveal"), .7f), "Unrelated block erased");
                Invoke(flash, "Advance", .05f); flash.TriggerFlash(); Invoke(flash, "Advance", .05f);
                Require(flash.IsFlashing, "Repeated hit did not refresh duration");
                Invoke(flash, "Advance", .04f); Require(!flash.IsFlashing && Render()[32 * 64 + 32].r < .01f, "Flash stuck");
                block.SetFloat("_EntranceReveal", 1); renderer.SetPropertyBlock(block);
                Set(flash, "mode", FightHitFlashController.FlashMode.RightToLeft);
                foreach (bool flip in new[] { false, true })
                {
                    renderer.flipX = flip; flash.TriggerFlash(); Invoke(flash, "Advance", .02f);
                    Color[] swept = Render(); float left = 0, right = 0;
                    for (int y = 24; y < 40; y++) for (int x = 24; x < 40; x++)
                        if (x < 32) left += swept[y * 64 + x].r; else right += swept[y * 64 + x].r;
                    Require(right > left + 1, "Sweep/flip not camera right-to-left");
                    flash.ClearFlash();
                }
                flash.TriggerFlash(); Invoke(flash, "OnDisable");
                Require(!flash.IsFlashing && renderer.sharedMaterial == material && material.GetFloat("_FlashAmount") == 0, "Disable/material restoration");
                Require(Render()[32 * 64 + 32].r < .01f, "Disable left a white sprite");
                Color[] Render()
                {
                    camera.Render(); RenderTexture.active = rt; readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply(); return readback.GetPixels();
                }
            }
            finally
            {
                RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(readback); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
