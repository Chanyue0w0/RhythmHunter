using System.IO;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterBeatProjectilePrefabConverter
    {
        public const string PrefabFolder = "Assets/OtterAquariumPrototype/Prefabs/BeatProjectiles";
        private const int ExpectedItemCount = 27;

        [InitializeOnLoadMethod]
        private static void QueueInitialConversion()
        {
            EditorApplication.delayCall += TryCreateMissingPrefabs;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryCreateMissingPrefabs;
        }

        private static void TryCreateMissingPrefabs()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += TryCreateMissingPrefabs;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || HasCompletePrefabSet())
                return;

            CreateMissingPrefabsFromItemTmp(false);
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Create Missing Beat Projectile Prefabs From ItemTMP")]
        public static void CreateMissingPrefabsFromMenu()
        {
            CreateMissingPrefabsFromItemTmp(true);
        }

        private static void CreateMissingPrefabsFromItemTmp(bool showResultDialog)
        {
            Scene scene = SceneManager.GetSceneByPath(OtterGoblinDemo1SceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(OtterGoblinDemo1SceneBuilder.ScenePath, OpenSceneMode.Additive);

            Transform itemRoot = FindNamedTransform(scene, "ItemTMP");
            if (itemRoot == null)
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                Debug.LogWarning("[BeatProjectiles] ItemTMP was not found in the shared Demo1 scene.");
                if (showResultDialog)
                    EditorUtility.DisplayDialog("找不到 ItemTMP", "請確認共用 Demo1 Scene 內仍保留 ItemTMP。", "好");
                return;
            }

            EnsurePrefabFolder();
            int createdCount = 0;
            foreach (Transform child in itemRoot)
            {
                SpriteRenderer sourceRenderer = child.GetComponent<SpriteRenderer>();
                if (sourceRenderer == null || sourceRenderer.sprite == null)
                    continue;

                string prefabPath = $"{PrefabFolder}/{SanitizeFileName(child.name)}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    continue;

                GameObject temporary = new(child.name, typeof(SpriteRenderer), typeof(RhythmTimelineProjectile));
                temporary.transform.localScale = child.localScale;
                SpriteRenderer renderer = temporary.GetComponent<SpriteRenderer>();
                renderer.sprite = sourceRenderer.sprite;
                renderer.color = sourceRenderer.color;
                renderer.flipX = sourceRenderer.flipX;
                renderer.flipY = sourceRenderer.flipY;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = 38;
                RhythmTimelineProjectile projectile = temporary.GetComponent<RhythmTimelineProjectile>();
                projectile.ConfigureAppearance(DefaultShouldRotate(child.name));
                ConfigureDefaultInterference(projectile, child.name);

                PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
                Object.DestroyImmediate(temporary);
                createdCount++;
            }

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BeatProjectiles] Created {createdCount} missing prefab(s) in {PrefabFolder}.");
            if (showResultDialog)
            {
                EditorUtility.DisplayDialog(
                    "節拍投擲物 Prefab",
                    createdCount > 0
                        ? $"已建立 {createdCount} 個 Prefab。旋轉可在各 Prefab 的 RhythmTimelineProjectile 上設定。"
                        : "所有 ItemTMP Prefab 都已存在。",
                    "好");
            }
        }

        private static bool HasCompletePrefabSet()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                return false;

            Scene scene = SceneManager.GetSceneByPath(OtterGoblinDemo1SceneBuilder.ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                Transform itemRoot = FindNamedTransform(scene, "ItemTMP");
                if (itemRoot != null)
                {
                    foreach (Transform child in itemRoot)
                    {
                        SpriteRenderer sourceRenderer = child.GetComponent<SpriteRenderer>();
                        if (sourceRenderer == null || sourceRenderer.sprite == null)
                            continue;

                        string prefabPath = $"{PrefabFolder}/{SanitizeFileName(child.name)}.prefab";
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                            return false;
                    }
                    return true;
                }
            }

            // Avoid opening the shared scene on every domain reload. When it is already
            // loaded we perform the exact name check above, including newly added items.
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }).Length >= ExpectedItemCount;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/OtterAquariumPrototype/Prefabs", "BeatProjectiles");
        }

        private static Transform FindNamedTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == objectName)
                        return transform;
                }
            }
            return null;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "BeatProjectile" : value.Trim();
        }

        private static bool DefaultShouldRotate(string objectName)
        {
            string value = objectName.ToLowerInvariant();
            return value.Contains("axe")
                || value.Contains("anchor")
                || value.Contains("sword")
                || value.Contains("spear")
                || value.Contains("shuriken");
        }

        private static void ConfigureDefaultInterference(
            RhythmTimelineProjectile projectile,
            string objectName)
        {
            string value = objectName.ToLowerInvariant();
            if (value.Contains("dying_bottle"))
            {
                projectile.ConfigureScreenInterference(
                    RhythmScreenInterference.InterferenceKind.OrangeInk,
                    4f);
            }
            else if (value.Contains("save_icon"))
            {
                projectile.ConfigureScreenInterference(
                    RhythmScreenInterference.InterferenceKind.SaveLoading,
                    3f);
            }
        }
    }
}
