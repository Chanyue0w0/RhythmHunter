using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class Art2CharacterAnchorSetup
    {
        private static readonly string[] Characters = { "LongswordWarrior", "Mage", "Skeleton", "Dwarf", "Minotaur", "Goblin_Normal", "Goblin_Killer", "Goblin_Giant" };
        static Art2CharacterAnchorSetup() => EditorApplication.update += RunRequested;

        private static void RunRequested()
        {
            const string request = "Temp/Art2CharacterAnchors.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool fitVisiblePixels = File.ReadAllText(request).Trim() == "fit-visible-pixels";
            File.Delete(request);
            try
            {
                ConfigureAnchors(fitVisiblePixels);
                FightStatusIconValidation.Run();
                FightDamageNumberPoolValidation.Run();
                ValidateSlotInformation();
                File.WriteAllText("Temp/Art2CharacterAnchors.result", "PASS: eight authored head anchors; status/damage pool regressions; slot overlays at 30/45/60 degrees and transform restoration.");
            }
            catch (Exception ex) { File.WriteAllText("Temp/Art2CharacterAnchors.result", "FAIL: " + ex); Debug.LogException(ex); }
        }

        [MenuItem("Rhythm Hunter/Art2/Add Missing Character Head Anchors")]
        public static void Configure()
        {
            ConfigureAnchors(false);
        }

        private static void ConfigureAnchors(bool fitVisiblePixels)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            foreach (string name in Characters)
            {
                string path = "Assets/FightDemo/Prefabs/ArtBattle/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var definition = root.GetComponent<FightCharacterDefinition>();
                    if (definition == null) throw new InvalidOperationException("Missing definition: " + name);
                    // Never overwrite an artist's existing placement on repeated use.
                    if (definition.HeadAnchor != null && !fitVisiblePixels) continue;
                    Transform anchor = definition.HeadAnchor != null ? definition.HeadAnchor : root.transform.Find("HeadAnchor");
                    if (anchor == null || fitVisiblePixels)
                    {
                        var animator = root.GetComponentInChildren<FightCharacterCombatAnimator>(true);
                        var renderer = animator != null ? animator.TargetRenderer : null;
                        if (renderer == null) renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                        if (renderer == null || renderer.sprite == null) throw new InvalidOperationException("Missing sprite: " + name);
                        Vector3 top = VisibleTop(renderer.sprite);
                        if (renderer.flipX) top.x = -top.x;
                        if (renderer.flipY) top.y = -top.y;
                        if (anchor == null)
                        {
                            anchor = new GameObject("HeadAnchor").transform;
                            anchor.SetParent(root.transform, false);
                        }
                        anchor.localPosition = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(top));
                    }
                    var serialized = new SerializedObject(definition);
                    serialized.FindProperty("headAnchor").objectReferenceValue = anchor;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("ART2_HEAD_ANCHORS_PASS: eight ArtBattle characters configured; cast/impact anchors unchanged.");
        }

        private static Vector3 VisibleTop(Sprite sprite)
        {
            // Read a temporary copy, without changing the texture importer's Read/Write setting.
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                string path = AssetDatabase.GetAssetPath(sprite.texture);
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
                Color32[] pixels = image.GetPixels32();
                Rect rect = sprite.rect;
                int minX = (int)rect.width, maxX = -1, maxY = -1, minY = (int)rect.height;
                for (int y = 0; y < (int)rect.height; y++)
                    for (int x = 0; x < (int)rect.width; x++)
                        if (pixels[((int)rect.y + y) * image.width + (int)rect.x + x].a > 25)
                        {
                            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                        }
                if (maxY < 0) throw new InvalidOperationException("Empty sprite: " + sprite.name);
                // Centre on the top silhouette, not the full width of a sword or shield.
                int band = Math.Max(1, (maxY - minY + 1) / 10);
                for (int y = Math.Max(minY, maxY - band + 1); y <= maxY; y++)
                    for (int x = 0; x < (int)rect.width; x++)
                        if (pixels[((int)rect.y + y) * image.width + (int)rect.x + x].a > 25)
                        { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); }
                return new Vector3((minX + maxX + 1) * .5f - sprite.pivot.x, maxY + 1 - sprite.pivot.y, 0) / sprite.pixelsPerUnit;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void ValidateSlotInformation()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RhythmHunter.FightDemo.Art2.Art2ActorPresentation>(true)).Single();
                Type type = presentation.GetType();
                var slots = (FightUnitSlot[])type.GetField("slots", flags).GetValue(presentation);
                var view = (Camera)type.GetField("informationCamera", flags).GetValue(presentation);
                if (view == null) throw new InvalidOperationException("Art2 information camera unassigned.");
                type.GetField("showContactShadows", flags).SetValue(presentation, false);
                var targets = slots.SelectMany(s => s.GetComponentsInChildren<TextMesh>(true)).Select(t => t.transform).Where(t => slots.Any(s => t.parent == s.transform)).ToArray();
                if (targets.Length == 0) throw new InvalidOperationException("Missing slot information fixtures.");
                var poses = targets.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
                type.GetMethod("OnEnable", flags).Invoke(presentation, null);
                foreach (float pitch in new[] { 30f, 45f, 60f })
                {
                    view.transform.rotation = Quaternion.Euler(pitch, 12, 0);
                    type.GetMethod("LateUpdate", flags).Invoke(presentation, null);
                    foreach (var target in targets)
                        if (Vector3.Dot(target.forward, view.transform.forward) < .9999f)
                            throw new InvalidOperationException("Slot label failed camera alignment.");
                }
                type.GetMethod("OnDisable", flags).Invoke(presentation, null);
                for (int i = 0; i < targets.Length; i++)
                    if (Vector3.Distance(targets[i].localPosition, poses[i].localPosition) > .0001f ||
                        Quaternion.Angle(targets[i].localRotation, poses[i].localRotation) > .001f ||
                        Vector3.Distance(targets[i].localScale, poses[i].localScale) > .0001f)
                        throw new InvalidOperationException("Slot overlay did not restore its authored transform.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
