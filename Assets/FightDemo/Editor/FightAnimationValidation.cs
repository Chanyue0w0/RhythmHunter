using System;
using System.IO;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>Isolated animation regressions; does not enter Play Mode or modify open scenes.</summary>
    [InitializeOnLoad]
    public static class FightAnimationValidation
    {
        private const string RequestPath = "Temp/FightAnimationValidation.request";
        private const string ResultPath = "Temp/FightAnimationValidation.result";
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static FightAnimationValidation()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                File.Delete(RequestPath);
                Run();
            };
        }

        [MenuItem("Rhythm Hunter/Validate Fight Character Animations")]
        public static void Run()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                ValidatePrefabs();
                ValidatePlayback(preview);
                File.WriteAllText(ResultPath, "PASS: six prefabs; frame events; interruption; reentrant callbacks; disable; idle restoration.");
                Debug.Log("FIGHT_ANIMATION_VALIDATION_PASS");
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ValidatePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[]
            {
                "Assets/FightDemo/Prefabs/Heroes", "Assets/FightDemo/Prefabs/Enemies"
            });
            Require(guids.Length == 6, "Expected six character prefabs.");
            foreach (string guid in guids)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0, prefab.name + ": missing script.");
                FightCharacterCombatAnimator[] players = prefab.GetComponentsInChildren<FightCharacterCombatAnimator>(true);
                Require(players.Length == 1, prefab.name + ": expected one animation owner.");
                FightCharacterCombatAnimator player = players[0];
                Require(player.FrameCount > 0 && player.TargetRenderer != null, prefab.name + ": idle data missing.");
                foreach (FightCharacterCombatAnimator.CombatAnimation animation in Enum.GetValues(typeof(FightCharacterCombatAnimator.CombatAnimation)))
                    Require(player.HasSequence(animation), prefab.name + ": missing " + animation);
            }
        }

        private static void ValidatePlayback(Scene preview)
        {
            GameObject root = new("Animation validation");
            SceneManager.MoveGameObjectToScene(root, preview);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            FightCharacterCombatAnimator player = root.AddComponent<FightCharacterCombatAnimator>();
            Sprite idle = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            Sprite attack = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one);
            try
            {
                player.ConfigureIdle(null, renderer, new[] { idle });
                var normal = Sequence(FightCharacterCombatAnimator.CombatAnimation.NormalAttack, attack, 0, 1, 2);
                var hit = Sequence(FightCharacterCombatAnimator.CombatAnimation.Hit, attack, -1, -1, 2);
                player.Configure(renderer, new[] { normal, hit });
                int warnings = 0, effects = 0, damage = 0, completed = 0;
                Require(player.Play(normal.Animation, () => warnings++, () => effects++, () => damage++, () => completed++), "Play failed.");
                Require(warnings == 1 && effects == 0 && damage == 0, "First-frame timing changed.");
                AdvanceToEnd(player);
                Require(effects == 1 && damage == 1 && completed == 1 && !player.IsPlaying, "Skipped frames lost or duplicated events.");
                Require(renderer.sprite == idle, "Idle was not restored after combat.");

                int effectCount = player.AttackEffectEventCount;
                player.Play(hit.Animation, onAttackEffect: () => effects++);
                AdvanceToEnd(player);
                Require(player.AttackEffectEventCount == effectCount && effects == 1, "Disabled effect event was flushed.");

                player.Play(normal.Animation, onAttackEffect: () => effects++, onDamage: () => damage++);
                player.Play(hit.Animation);
                Require(effects == 2 && damage == 2, "Interrupted attack must settle its events once.");
                AdvanceToEnd(player);
                Require(effects == 2 && damage == 2, "Interrupted callbacks repeated.");

                player.Play(normal.Animation, onWarning: () => player.Play(hit.Animation));
                Require(player.ActiveAnimation == hit.Animation, "Reentrant warning lost the replacement animation.");
                AdvanceToEnd(player);

                player.Play(normal.Animation, onDamage: () => player.Play(hit.Animation));
                AdvanceToEnd(player);
                Require(player.IsPlaying && player.ActiveAnimation == hit.Animation && player.ActiveFrame == 0,
                    "Old update advanced or cleared a callback's new animation.");
                AdvanceToEnd(player);

                player.Play(normal.Animation, onCompleted: () => player.Play(hit.Animation));
                Require(!player.Play(normal.Animation) && player.ActiveAnimation == hit.Animation,
                    "Outer Play overwrote the completion callback's animation.");
                AdvanceToEnd(player);

                int cancelled = 0;
                player.Play(normal.Animation, onDamage: () => cancelled++, onCompleted: () => cancelled++);
                root.SetActive(false);
                // Edit-mode MonoBehaviour lifecycle is not driven as in Play Mode.
                typeof(FightCharacterCombatAnimator).GetMethod("OnDisable", PrivateInstance).Invoke(player, null);
                Require(!player.IsPlaying && cancelled == 0, "Disable fired gameplay or completion callbacks.");
                Require(!player.Play(normal.Animation), "Inactive animator accepted playback.");
                root.SetActive(true);
                player.SendMessage("Update");
                Require(renderer.sprite == idle, "Idle was not restored after re-enable.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(attack);
            }
        }

        private static FightCharacterCombatAnimator.Sequence Sequence(
            FightCharacterCombatAnimator.CombatAnimation animation, Sprite sprite, int warning, int effect, int damage)
        {
            var result = new FightCharacterCombatAnimator.Sequence();
            result.Configure(animation, new[] { sprite, sprite, sprite }, 16f, warning, effect, damage);
            return result;
        }

        private static void AdvanceToEnd(FightCharacterCombatAnimator player)
        {
            typeof(FightCharacterCombatAnimator).GetField("frameElapsed", PrivateInstance).SetValue(player, 10f);
            player.SendMessage("Update");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
