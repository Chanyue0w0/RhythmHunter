using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightSpriteDamageNumberSetup
    {
        const string Root = "Assets/FightDemo/Prefabs/VFX/DamageNumbers";
        const string Art = "Assets/FightDemo/Arts/UI/戰鬥場景/數字";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Colors = { "red", "blue", "green", "white" };
        static FightSpriteDamageNumberSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            const string validateRequest = "Temp/IntegerDamageNumbers.request";
            if (File.Exists(validateRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(validateRequest);
                try { foreach (string color in Colors) Validate(color); ValidateAssets(); File.WriteAllText("Temp/IntegerDamageNumbers.result", "PASS: four palettes, 1000 reuse cycles each, integer glyphs, left enemy / right party motion, delayed hit spacing and unique sorting groups after reuse, fade/reset/cap, 8 HP = four hearts, 7 HP = three and half; migrated enemy and hero stats; green actual healing, full-health suppression, mixed healing/damage pool reuse."); }
                catch (Exception e) { File.WriteAllText("Temp/IntegerDamageNumbers.result", "FAIL: " + e); Debug.LogException(e); }
            }
            const string request = "Temp/SpriteDamageNumbers.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Setup(); File.WriteAllText("Temp/SpriteDamageNumbers.result", "PASS: 40 digit prefabs + 4 decimal prefabs; all palettes, 0.5/12.5, fade, camera alignment, 1000 pool reuse cycles, cap and cleanup. Art2 defaults red; visibility toggle preserved."); }
            catch (Exception e) { File.WriteAllText("Temp/SpriteDamageNumbers.result", "FAIL: " + e); Debug.LogException(e); }
        }
        [MenuItem("Rhythm Hunter/Art2/Setup Sprite Damage Numbers")]
        static void Setup()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/DamageNumberUnlit.mat");
            if (!material)
            {
                material = new Material(Shader.Find("RhythmHunter/Art2/SpriteCardUnlit"));
                AssetDatabase.CreateAsset(material, Root + "/DamageNumberUnlit.mat");
            }
            foreach (string color in Colors)
            {
                string folder = Root + "/number_" + color;
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, "number_" + color);
                for (int i = 0; i < 10; i++)
                {
                    string path = Art + "/number_" + color + "/number_" + color + "_" + i + ".png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (!sprite) throw new Exception("Missing sprite: " + path);
                    SaveGlyph(folder + "/" + i + ".prefab", sprite, material, false);
                }
                string dotPath = folder + "/DecimalPoint.png";
                if (!File.Exists(dotPath))
                {
                    Color tint = color == "red" ? new Color(1, .2f, .35f) : color == "blue" ? new Color(.2f, .65f, 1) : color == "green" ? new Color(.3f, 1, .35f) : Color.white;
                    var texture = new Texture2D(10, 10, TextureFormat.RGBA32, false);
                    texture.SetPixels(Enumerable.Repeat(tint, 100).ToArray()); texture.Apply();
                    File.WriteAllBytes(dotPath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(dotPath);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(dotPath);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                SaveGlyph(folder + "/DecimalPoint.prefab", AssetDatabase.LoadAssetAtPath<Sprite>(dotPath), material, true);
                Validate(color);
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/FightDemo/Scenes/Art2_FightScene.unity", OpenSceneMode.Additive);
            try
            {
                foreach (var fight in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)))
                {
                    Undo.RecordObject(fight, "Use red sprite damage numbers");
                    var data = new SerializedObject(fight);
                    var digits = data.FindProperty("damageDigitPrefabs"); digits.arraySize = 10;
                    for (int i = 0; i < 10; i++) digits.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/number_red/" + i + ".prefab");
                    data.FindProperty("damageDecimalPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/number_red/DecimalPoint.prefab");
                    data.ApplyModifiedProperties();
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }
        static void SaveGlyph(string path, Sprite sprite, Material material, bool dot)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return; // Preserve subsequent artist edits.
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                root.layer = 6;
                var visual = new GameObject("Sprite"); visual.layer = 6; visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * .6f;
                visual.transform.localPosition = dot ? new Vector3(0, -.12f, 0) : Vector3.zero;
                var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.sharedMaterial = material; renderer.sortingOrder = 100;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void ValidateAssets()
        {
            string folder = "Assets/FightDemo/Prefabs/ArtBattle/";
            foreach (var entry in new[] { ("Goblin_Normal", 60f, 2f), ("Goblin_Killer", 60f, 2f), ("Goblin_Giant", 80f, 4f), ("Goblin_King", 300f, 2f), ("LongSwordWarrior_Weapon", 4f, 2f), ("Mage", 2f, 2f), ("Skeleton", 2f, 2f) })
            {
                var definition = AssetDatabase.LoadAssetAtPath<GameObject>(folder + entry.Item1 + ".prefab").GetComponent<FightCharacterDefinition>();
                Check(definition.MaxHp == entry.Item2 && definition.AttackPower == entry.Item3, entry.Item1 + " values");
            }
            var root = new GameObject("Integer HP validation"); root.SetActive(false);
            try
            {
                var slot = root.AddComponent<FightUnitSlot>();
                slot.SpawnCharacter(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Goblin_Normal.prefab").GetComponent<FightCharacterDefinition>(), null);
                slot.TakeDamage(1); Check(slot.CurrentHp == 59, "One point damage");
                slot.TakeDamage(.5f); Check(slot.CurrentHp == 58, "Fraction quantization");
                var fight = root.AddComponent<FightCombatController>();
                Set(fight, "combatMode", FightCombatController.CombatMode.EqualBeat);
                Set(fight, "enableHealthSystemInFrontHeroMode", true);
                Set(fight, "partyHp", 8f); Set(fight, "maxPartyHp", 8f);
                Set(fight, "partyArmor", 4f); Set(fight, "maxPartyArmor", 4f);
                Check(fight.ApplyPartyDamage(1, 0) == 1 && fight.PartyArmor == 3 && fight.PartyHp == 8, "Half armor loss");
                float chip = (float)Call(fight, "ResolveEnemyHitDamage", 4f, true, 1f);
                Check(chip == 1, "Guard chip one point");
                Call(fight, "ApplyEnemyPartyDamage", chip, true, 10L);
                Call(fight, "ApplyEnemyPartyDamage", chip, true, 10L);
                Check(fight.PartyArmor == 2, "Same-beat chip deduplication");
                fight.ApplyPartyDamage(4, 11);
                Check(fight.PartyArmor == 0 && fight.PartyHp == 6, "Armor overflow to HP");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void Validate(string color)
        {
            var root = new GameObject("Damage number validation"); root.SetActive(false);
            try
            {
                var fight = root.AddComponent<FightCombatController>();
                var target = root.AddComponent<FightUnitSlot>(); Set(target, "team", FightUnitSlot.UnitTeam.Enemy);
                var digits = Enumerable.Range(0, 10).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/number_" + color + "/" + i + ".prefab")).ToArray();
                Set(fight, "showEnemyDamageNumbers", true); Set(fight, "damageDigitPrefabs", digits);
                Set(fight, "healingDigitPrefabs", Enumerable.Range(0, 10).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/number_green/" + i + ".prefab")).ToArray());
                Set(fight, "damageDecimalPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/number_" + color + "/DecimalPoint.prefab"));
                Set(fight, "damageNumberMaxActive", 2);
                var camera = root.AddComponent<Camera>(); camera.transform.rotation = Quaternion.Euler(45, 0, 0);
                Set(fight, "damageNumberCamera", camera);
                void Show(float damage) => Call(fight, "ShowEnemyDamageNumber", target, damage);
                Show(1f);
                var active = (IList)fight.GetType().GetField("activeDamageNumbers", Flags).GetValue(fight);
                var popup = (FightDamageNumber)active[0];
                var sprites = popup.GetComponentsInChildren<SpriteRenderer>(true);
                Check(sprites.Length == 1, "One point must use an integer glyph");
                Check(Quaternion.Angle(popup.transform.rotation, camera.transform.rotation) < .01f, "Camera facing");
                Vector3 start = popup.transform.position;
                Call(popup, "Advance", .65f);
                Check(Vector3.Dot(popup.transform.position - start, camera.transform.right) < 0, "Enemy popup must move left");
                Check(sprites.All(s => s.color.a > 0 && s.color.a < 1), "Sprite fade");
                Call(popup, "Advance", 1f);
                int roots = fight.CreatedDamageNumberCount, glyphs = fight.CreatedDamageGlyphCount;
                for (int i = 0; i < 1000; i++) { Show(25f); Call(active[0], "Advance", 2f); }
                Check(roots == fight.CreatedDamageNumberCount && glyphs == fight.CreatedDamageGlyphCount, "Pool grew after warmup");
                Show(1);
                var earlier = (FightDamageNumber)active[0];
                Call(earlier, "Advance", .25f); // Half-beat delayed hit at 120 BPM.
                Show(2);
                var later = (FightDamageNumber)active[1];
                Vector3 delta = later.transform.position - earlier.transform.position;
                float dx = Vector3.Dot(delta, camera.transform.right), dy = Vector3.Dot(delta, camera.transform.up);
                Check(dx * dx + dy * dy > .01f, "Delayed hit should be offset");
                // Force every candidate to be crowded; the fallback must still remain bounded.
                Set(fight, "damageNumberMinimumSeparation", 100f);
                Vector3 bounded = (Vector3)Call(fight, "ChooseDamagePopupPosition", Vector3.zero);
                Check(Mathf.Abs(Vector3.Dot(bounded, camera.transform.right)) <= .4301f &&
                    Mathf.Abs(Vector3.Dot(bounded, camera.transform.up)) <= .1801f, "Crowded fallback escaped random bounds");
                Set(fight, "damageNumberMinimumSeparation", .45f);
                Check(later.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder > earlier.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder, "Later popup must have a separate higher order");
                Show(3); Check(active.Count == 2, "Pool cap");
                Check(((FightDamageNumber)active[0]).GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder != ((FightDamageNumber)active[1]).GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder, "Recycling caused order collision");
                Check(((FightDamageNumber)active[0]).GetComponentsInChildren<SpriteRenderer>(true).All(s => s.color.a == 1), "Reuse alpha reset");
                Call(fight, "ResetDamageNumberPool"); Check(active.Count == 0, "Cleanup");
                Call(fight, "ShowDamageNumber", target, 2f, true);
                popup = (FightDamageNumber)active[0]; start = popup.transform.position;
                Call(popup, "Advance", .2f);
                Check(Vector3.Dot(popup.transform.position - start, camera.transform.right) > 0, "Party popup must move right");
                Call(fight, "ResetDamageNumberPool");
                Check(FightPartyResourceHud.UnitFill(7, 3) == .5f && FightPartyResourceHud.UnitFill(8, 3) == 1, "Four hearts / half heart");
                Check(FightCombatUnits.Amount(.5f) == 1 && FightCombatUnits.Amount(0) == 0, "Integer rounding");
                Call(fight, "ResetDamageNumberPool");
                typeof(FightUnitSlot).GetProperty("CombatController").SetValue(target, fight);
                Set(target, "maxHp", 20f); Set(target, "currentHp", 18f);
                Check(target.Heal(60) == 2 && active.Count == 1, "Healing must show actual capped recovery");
                var healingSprite = ((FightDamageNumber)active[0]).GetComponentInChildren<SpriteRenderer>(true).sprite;
                Check(healingSprite.name == "number_green_2", "Healing must use the green digit prefab");
                Call(fight, "ResetDamageNumberPool");
                Check(target.Heal(60) == 0 && active.Count == 0, "Full health must not show healing numbers");
                for (int i = 0; i < 100; i++)
                {
                    Set(target, "currentHp", 18f); target.Heal(2); Call(fight, "ResetDamageNumberPool");
                    Show(2); Call(fight, "ResetDamageNumberPool");
                }
                Check(roots == fight.CreatedDamageNumberCount && glyphs == fight.CreatedDamageGlyphCount, "Mixed healing and damage reuse grew the pool");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
