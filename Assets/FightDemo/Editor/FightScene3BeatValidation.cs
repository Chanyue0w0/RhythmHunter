using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    // Runs against an isolated scene copy; never enters Play Mode or saves the user's scene.
    [InitializeOnLoad]
    public static class FightScene3BeatValidation
    {
        private const string Request = "Temp/FightScene3BeatValidation.request";
        private const string Result = "Temp/FightScene3BeatValidation.result";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static FightScene3BeatValidation()
        {
            EditorApplication.update += CheckRequest;
        }

        private static void CheckRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Rhythm Hunter/Validate FightScene3 Equal Beats")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/FightScene3.unity");
            bool passed = false;
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var fight = components.OfType<FightCombatController>().Single();
                var hud = components.OfType<FightScenePresenter>().Single();
                Require(fight.UsesEqualBeats && fight.UsesFrontHeroControls, "FightScene3 must use equal-beat party controls.");
                for (int beat = 1; beat <= 8; beat++)
                    Require(fight.GetHeroActionForBeat(beat) == FightCombatController.ActionType.LightAttack, "Every musical beat must select Basic.");
                fight.SetCombatMode(FightCombatController.CombatMode.FrontHero);
                Require(fight.GetHeroActionForBeat(4) == FightCombatController.ActionType.Skill, "Older scenes must retain fourth-beat skills.");
                fight.SetCombatMode(FightCombatController.CombatMode.EqualBeat);

                ValidateFormation(components.OfType<FightRosterManager>().Single(), fight);
                ValidateDefense(fight);
                FightBasicGuardValidation.Run(fight);

                Invoke(hud, "BuildEqualBeatTimeline");
                var points = Field<Image[]>(hud, "approachingPoints");
                var center = Field<Image>(hud, "timelineCenter");
                var root = Field<RectTransform>(hud, "timelineRoot");
                Require(points.Length == 6 && root.anchorMin == new Vector2(0.5f, 0f), "Expected three pairs anchored at bottom center.");
                Require(!Field<Slider>(hud, "beatProgress").gameObject.activeInHierarchy, "Old four-beat HUD must be hidden.");
                Invoke(hud, "BuildEqualBeatTimeline");
                Require(Field<RectTransform>(hud, "timelineRoot") == root, "HUD must not be duplicated.");

                foreach (double interval in new[] { 1000.0, 500.0, 333.333 })
                {
                    // Sweep across multiple bars, including negative phase (early input offsets).
                    for (int beat = -1; beat < 9; beat++)
                    {
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.1) * interval, 2000.0, interval);
                        float initial = Mathf.Abs(points[0].rectTransform.anchoredPosition.x);
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.9) * interval, 2000.0, interval);
                        Require(Mathf.Abs(points[0].rectTransform.anchoredPosition.x) < initial, "Points must move inward throughout each beat.");
                        for (int pair = 0; pair < points.Length; pair += 2)
                            Require(Mathf.Approximately(points[pair].rectTransform.anchoredPosition.x,
                                -points[pair + 1].rectTransform.anchoredPosition.x), "Left/right points must arrive together.");
                        Invoke(hud, "RenderEqualBeatTimeline", 2000 + (beat + 0.99999) * interval, 2000.0, interval);
                        Require(Mathf.Abs(points[0].rectTransform.anchoredPosition.x) < 0.01f, "Points must reach the center immediately before the beat rollover.");
                    }
                }
                Invoke(hud, "RenderEqualBeatTimeline", 2000.0, 2000.0, 500.0);
                Require(center.rectTransform.localScale.x > 1f, "Center must pulse at the beat.");
                Invoke(hud, "UpdateEqualBeatTimeline");
                Require(points.All(point => !point.enabled), "No moving points before music is ready.");
                Require(center.rectTransform.localScale == Vector3.one, "Waiting state must clear the pulse.");
                Require(root.GetComponentsInChildren<Image>().All(graphic => !graphic.raycastTarget), "Timeline must not intercept input.");
                ValidateDefenseHud(fight, hud);
                FightTimingCalibrationValidation.Run(fight, hud, canvas => CaptureHud(canvas, "Temp/FightTimingCalibration-preview.png"));
                File.WriteAllText(Result, "PASS: 5 shared HP; frontline DEF 1:1; armor-first damage and overflow; recovery delay/interval/reset/cap; healing does not restore armor; zero-HP input remains enabled; live defense/enemy HUD and developer toggle; all six party permutations; empty positions keep bindings; character-owned Basic data; equal-beat timeline regressions.\nLive FMOD/input and visual playtesting are separate checks.");
                Debug.Log("FIGHT_SCENE3_BEAT_VALIDATION_PASS");
                passed = true;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Result, "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        private static void ValidateFormation(FightRosterManager roster, FightCombatController fight)
        {
            var heroes = roster.HeroPrefabs.ToArray();
            var enemies = roster.EnemyPrefabs.ToArray();
            int[][] orders =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
                new[] { -1, 1, 2 }, new[] { 0, -1, 2 }, new[] { 0, 1, -1 },
                new[] { -1, -1, -1 }
            };
            foreach (int[] order in orders)
            {
                var assigned = order.Select(index => index < 0 ? null : heroes[index]).ToArray();
                roster.SetRoster(assigned, enemies);
                Invoke(fight, "RebuildRosterAndResetCombat");
                Require(Mathf.Approximately(fight.MaxPartyHp, assigned.Where(hero => hero != null).Sum(hero => hero.MaxHp)), "Shared HP must sum occupied positions.");
                Require(Mathf.Approximately(fight.MaxPartyArmor, assigned[0] != null ? fight.ArmorForDefense(assigned[0].Defense) : 0), "Only the authored frontline grants armor.");
                for (int i = 0; i < 3; i++)
                {
                    FightUnitSlot slot = fight.GetHeroForCommand((FightInputRouter.HeroCommand)i).UnitSlot;
                    Require(slot == roster.GetHeroAtPosition((FightRosterManager.PartyPosition)i), "Input must resolve its authored position.");
                    if (assigned[i] == null)
                    {
                        Require(slot == null, "An empty position must not shift another character's binding.");
                        continue;
                    }
                    Require(slot != null && slot.CharacterDefinition.CharacterId == assigned[i].CharacterId, "Any hero must be assignable to any position.");
                    Require(slot.NormalAbilityBehavior == assigned[i].BasicAbilityType &&
                        Mathf.Approximately(slot.NormalAbilityPower, assigned[i].BasicAbilityPower), "Basic behavior/power must follow the character, not the slot.");
                }
            }
            Require(fight.GetHeroForCommand(FightInputRouter.HeroCommand.Ultimate) == null, "Ultimate must not resolve to a Basic slot.");
            roster.SetRoster(heroes, enemies);
            Invoke(fight, "RebuildRosterAndResetCombat");
        }

        private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

        private static void ValidateDefense(FightCombatController fight)
        {
            int[] defense = { 0, 2, 3, 5, 6, 8, 9, 10 };
            float[] armor = { 0, 2, 3, 5, 6, 8, 9, 10 };
            for (int i = 0; i < defense.Length; i++)
                Require(Mathf.Approximately(fight.ArmorForDefense(defense[i]), armor[i]), "DEF must convert to armor 1:1.");
            Require(fight.FrontHero.UnitSlot.MaxHp == 3 && fight.SecondHero.UnitSlot.MaxHp == 1 && fight.ThirdHero.UnitSlot.MaxHp == 1, "Default hero HP must be 3 / 1 / 1.");
            Require(fight.FrontHero.UnitSlot.CharacterDefinition.Defense == 3 && fight.SecondHero.UnitSlot.CharacterDefinition.Defense == 1 && fight.ThirdHero.UnitSlot.CharacterDefinition.Defense == 1, "Default hero DEF must be 3 / 1 / 1.");
            Require(fight.MaxPartyHp == 5 && fight.MaxPartyArmor == 3, "Default party must have 5 HP and 3 frontline armor.");
            float fullHp = fight.MaxPartyHp;
            float fullArmor = fight.MaxPartyArmor;
            fight.ApplyPartyDamage(fullArmor + 1, 10);
            Require(fight.PartyArmor == 0 && fight.PartyHp == fullHp - 1, "Armor must absorb damage before HP, including overflow.");
            fight.AdvanceArmorRecovery(13);
            Require(fight.PartyArmor == 0, "Armor recovered before the delay.");
            fight.AdvanceArmorRecovery(14);
            Require(fight.PartyArmor == 0.5f, "First recovery must occur at damage beat + delay.");
            fight.AdvanceArmorRecovery(14);
            fight.AdvanceArmorRecovery(15);
            Require(fight.PartyArmor == 0.5f, "Recovery must not duplicate on the same beat or occur before the interval.");
            fight.AdvanceArmorRecovery(16);
            Require(fight.PartyArmor == 1f, "Recovery interval mismatch.");
            fight.ApplyPartyDamage(0.5f, 17);
            fight.AdvanceArmorRecovery(20);
            Require(fight.PartyArmor == 0.5f, "New armor damage must reset the entire delay.");
            fight.ApplyPartyDamage(0f, 20);
            fight.AdvanceArmorRecovery(21);
            Require(fight.PartyArmor == 1f, "Zero/blocked damage must not reset recovery.");
            fight.AdvanceArmorRecovery(100);
            Require(fight.PartyArmor == fullArmor && fight.PartyHp == fullHp - 1, "Recovery must cap armor and never regenerate HP.");
            fight.ApplyPartyDamage(fullArmor, 110);
            Invoke(fight, "HealPartyHealth", fight.SecondHero.UnitSlot, 100f);
            Require(fight.PartyHp == fullHp && fight.PartyArmor == 0, "Healing must cap HP without restoring armor.");
            typeof(FightCombatController).GetField("pendingEnemyAttack", Private).SetValue(fight, true);
            fight.AdvanceArmorRecovery(114);
            Require(fight.PartyArmor == 0, "Recovery must wait for pending damage resolution.");
            typeof(FightCombatController).GetField("pendingEnemyAttack", Private).SetValue(fight, false);
            fight.AdvanceArmorRecovery(114);
            Require(fight.PartyArmor == 0.5f, "Recovery did not resume after resolution.");
            fight.ApplyPartyDamage(999f, 115);
            Require(fight.PartyHp == 0 && fight.PartyArmor == 0 && !fight.BattleEnded, "Zero team HP must not lock prototype combat.");
            bool inputDispatched = false;
            Action<FightCombatController.HeroCallResult> onInput = result => inputDispatched = true;
            fight.HeroCalled += onInput;
            fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
            fight.HeroCalled -= onInput;
            Require(inputDispatched, "Basic input must still reach judgement at zero HP.");
            fight.AdvanceArmorRecovery(200);
            Invoke(fight, "HealPartyHealth", fight.SecondHero.UnitSlot, 100f);
            Require(fight.PartyHp == fullHp && fight.PartyArmor == fullArmor, "Zero-HP parties must still heal and recover armor.");
            Invoke(fight, "RebuildRosterAndResetCombat");
        }

        private static void ValidateDefenseHud(FightCombatController fight, FightScenePresenter presenter)
        {
            Text health = Field<Text>(presenter, "healthText");
            var hud = fight.gameObject.AddComponent<FightDefenseHud>();
            hud.Configure(fight, health.canvas, health.font);
            Require(Field<Text[]>(hud, "values").Length == 6, "Developer panel must cover both teams.");
            fight.ApplyPartyDamage(fight.MaxPartyArmor + 0.5f, 1);
            hud.Refresh();
            Require(Field<Text>(hud, "hpText").text.Contains($"{fight.PartyHp:0.#} / {fight.MaxPartyHp:0.#}"), "HP HUD is stale.");
            Require(Field<Text>(hud, "armorText").text.Contains($"0 / {fight.MaxPartyArmor:0.#}"), "Armor HUD is stale.");
            var enemy = fight.RosterManager.ActiveEnemies[0];
            enemy.TakeDamage(0.5f);
            hud.Refresh();
            Require(Field<Text[]>(hud, "enemyLabels")[0].text.Contains($"{enemy.CurrentHp:0.#} / {enemy.MaxHp:0.#}"), "Enemy health label must update after damage.");
            Require(Field<Image[]>(hud, "enemyFills")[0].rectTransform.sizeDelta.x < 508, "Enemy health bar must shrink after damage.");
            var dev = Field<GameObject>(hud, "developerPanel");
            var root = Field<RectTransform>(hud, "root");
            root.GetComponentInChildren<Button>().onClick.Invoke();
            Require(!dev.activeSelf, "Developer panel must be collapsible.");
            root.GetComponentInChildren<Button>().onClick.Invoke();
            Require(dev.activeSelf, "Developer panel must reopen.");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                CaptureHud(health.canvas);
            UnityEngine.Object.DestroyImmediate(hud);
            Invoke(fight, "RebuildRosterAndResetCombat");
        }

        private static void CaptureHud(Canvas source, string outputPath = "Temp/FightDefenseHud-preview.png")
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture previousTarget = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                SceneManager.SetActiveScene(stage);
                var canvas = UnityEngine.Object.Instantiate(source.gameObject).GetComponent<Canvas>();
                var camera = new GameObject("HUD Preview Camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.backgroundColor = new Color(0.07f, 0.1f, 0.14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.targetTexture = target;
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = short.MaxValue;
                canvas.worldCamera = camera;
                var rect = (RectTransform)canvas.transform;
                rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = new Vector2(1920, 1080);
                rect.position = Vector3.zero;
                rect.rotation = Quaternion.identity;
                rect.localScale = Vector3.one * 0.01f;
                Canvas.ForceUpdateCanvases();
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
                {
                    graphic.SetAllDirty();
                    graphic.Rebuild(CanvasUpdate.PreRender);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(stage, true);
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
