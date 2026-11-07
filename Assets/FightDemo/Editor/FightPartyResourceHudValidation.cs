using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public static class FightPartyResourceHudValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightPartyResourceHudValidation() => EditorApplication.update += RunRequested;
        static void RunRequested()
        {
            const string request = "Temp/PartyResourceHud.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { Run(); File.WriteAllText("Temp/PartyResourceHud.result", "PASS"); }
            catch (Exception ex) { File.WriteAllText("Temp/PartyResourceHud.result", "FAIL: " + ex); Debug.LogException(ex); }
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Set(object obj, string field, float value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);

        [MenuItem("Rhythm Hunter/Validate Party Resource HUD")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/FightDemo/Scenes/Art2_FightScene.unity");
            try
            {
                var fight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightCombatController>(true)).Single();
                fight.RosterManager.GetType().GetField("requireBattlePreparation", Flags).SetValue(fight.RosterManager, false);
                fight.RosterManager.SpawnConfiguredRoster();
                typeof(FightCombatController).GetMethod("RebuildRosterAndResetCombat", Flags).Invoke(fight, null);
                var canvasGo = new GameObject("ResourceHudTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.GetComponent<Canvas>();
                var prefab = AssetDatabase.LoadAssetAtPath<FightPartyResourceHud>("Assets/FightDemo/Prefabs/UI/PartyResourceHud.prefab");
                var hud = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
                Set(fight, "maxPartyHp", 4); Set(fight, "maxPartyArmor", 3);
                Set(fight, "partyHp", 2.5f); Set(fight, "partyArmor", 1.5f);
                hud.Configure(fight);
                Image[] Fills(string prefix) => hud.GetComponentsInChildren<Image>().Where(i => i.name == "Full" && i.transform.parent.name.StartsWith(prefix)).ToArray();
                var hp = Fills("HP"); var armor = Fills("Armor");
                Check(hp.Select(i => i.fillAmount).SequenceEqual(new[] { 1f, 1f, .5f, 0f }), "HP half-unit mapping.");
                Check(armor.Select(i => i.fillAmount).SequenceEqual(new[] { 1f, .5f, 0f }), "Armor half-unit mapping.");
                Check(hp.All(i => i.sprite != null && i.fillOrigin == (int)Image.OriginHorizontal.Right && !i.raycastTarget), "Sprite references and right-origin clipping.");
                Check(((RectTransform)hp[1].transform.parent).anchoredPosition.x < ((RectTransform)hp[0].transform.parent).anchoredPosition.x, "Units must extend leftward.");
                Check(((RectTransform)armor[0].transform.parent).anchoredPosition.y < ((RectTransform)hp[0].transform.parent).anchoredPosition.y, "Armor must be below HP.");
                typeof(FightScene3BeatValidation).GetMethod("CaptureHud", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { canvas, "Temp/PartyResourceHud-preview.png" });
                int objects = hud.GetComponentsInChildren<Image>(true).Length;
                Set(fight, "partyHp", 0); Set(fight, "partyArmor", 3); hud.Refresh();
                Check(hp.All(i => i.fillAmount == 0) && armor.All(i => i.fillAmount == 1), "Zero HP and full armor refresh.");
                Set(fight, "maxPartyArmor", 1); Set(fight, "partyArmor", .5f); hud.Refresh();
                Check(Fills("Armor").Length == 1 && Fills("Armor")[0].fillAmount == .5f, "Formation maximum changes.");
                Set(fight, "maxPartyArmor", 0); hud.Refresh();
                Check(Fills("Armor").Length == 0, "Zero armor capacity hides row.");
                Set(fight, "maxPartyArmor", 3); Set(fight, "partyArmor", 3); hud.Refresh();
                Check(hud.GetComponentsInChildren<Image>(true).Length == objects, "Existing icons must be reused.");
                Check(FightPartyResourceHud.UnitFill(.5f, 0) == .5f && FightPartyResourceHud.UnitFill(1f, 0) == 1f, "Half/full boundaries.");
                for (int beat = 0; beat < 8; beat++)
                    for (int i = 0; i < 4; i++)
                    {
                        float jump = FightPartyResourceHud.BounceOffset(i, 4, beat, .225f, 4, .45f);
                        Check(Mathf.Abs(jump - (i == 3 - beat % 4 ? 4 : 0)) < .0001f, "Left-to-right bounce and wrap.");
                    }
                Check(FightPartyResourceHud.BounceOffset(0, 1, 0, .45f, 4, .45f) == 0 &&
                    FightPartyResourceHud.BounceOffset(0, 0, 0, .2f, 4, .45f) == 0, "Bounce ends at rest; empty row safe.");
                foreach (var capacities in new[] { new Vector2Int(4, 2), new Vector2Int(2, 4), new Vector2Int(4, 0) })
                {
                    int cycle = Mathf.Max(capacities.x, capacities.y);
                    for (int beat = 0; beat < cycle * 2; beat++)
                    {
                        int column = cycle - 1 - beat % cycle;
                        foreach (int count in new[] { capacities.x, capacities.y })
                        {
                            int jumping = 0;
                            for (int i = 0; i < count; i++)
                            {
                                float jump = FightPartyResourceHud.BounceOffset(i, cycle, beat, .225f, 4, .45f);
                                Check(Mathf.Abs(jump - (i == column ? 4 : 0)) < .0001f, "Rows must share one right-aligned bounce column.");
                                if (jump > 0) jumping++;
                            }
                            Check(jumping == (column < count ? 1 : 0), "Shorter row must wait before joining the cycle.");
                        }
                    }
                }
                var presenter = fight.GetComponent<FightScenePresenter>();
                var gaugeFlag = typeof(FightScenePresenter).GetField("showTeamSkillGauge", Flags);
                var gaugeUpdate = typeof(FightScenePresenter).GetMethod("UpdateTeamSkillHud", Flags);
                var panelField = typeof(FightScenePresenter).GetField("teamSkillPanel", Flags);
                gaugeFlag.SetValue(presenter, false); gaugeUpdate.Invoke(presenter, null);
                Check(panelField.GetValue(presenter) == null, "Hidden gauge must not be generated.");
                gaugeFlag.SetValue(presenter, true); gaugeUpdate.Invoke(presenter, null);
                var panel = (RectTransform)panelField.GetValue(presenter);
                Check(panel != null && panel.gameObject.activeSelf, "Gauge can be enabled.");
                gaugeFlag.SetValue(presenter, false); gaugeUpdate.Invoke(presenter, null);
                Check(!panel.gameObject.activeSelf, "Existing gauge can be hidden.");
                var battlefield = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FightBattlefieldPresenter>(true)).Single();
                Check(battlefield.EnemySlots.Any(e => e != null), "Enemy visibility test requires spawned enemies.");
                typeof(FightBattlefieldPresenter).GetField("uiPresenter", Flags).SetValue(battlefield, presenter);
                foreach (bool visible in new[] { false, true, false })
                {
                    typeof(FightScenePresenter).GetField("showEnemyHealthUi", Flags).SetValue(presenter, visible);
                    typeof(FightBattlefieldPresenter).GetMethod("RefreshFrontHeroPresentation", Flags).Invoke(battlefield, null);
                    foreach (var enemy in battlefield.EnemySlots)
                    {
                        if (enemy == null) continue;
                        var fill = (SpriteRenderer)typeof(FightUnitSlot).GetField("hpFill", Flags).GetValue(enemy);
                        Check(fill == null || fill.gameObject.activeSelf == visible, "Enemy world health visibility toggle.");
                    }
                }
                Debug.Log("PARTY_RESOURCE_HUD_PASS: shared resources, half units, right-to-left fill, maxima changes, reuse.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
