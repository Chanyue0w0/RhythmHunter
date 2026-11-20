using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmHunter.FightDemo;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    static class FightPauseGuideSetup
    {
        static int previewFrames;
        static FightPauseGuideSetup() => EditorApplication.update += Poll;
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists("Temp/PauseGuideSetup.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/PauseGuideSetup.request");
                try { Configure(); File.WriteAllText("Temp/PauseGuideSetup.result", "PASS: Six portraits and character descriptions saved."); }
                catch (Exception e) { File.WriteAllText("Temp/PauseGuideSetup.result", "FAIL: " + e); }
            }
            if (!File.Exists("Temp/PauseGuidePreview.request")) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                var pause = UnityEngine.Object.FindFirstObjectByType<FightPauseController>();
                if (!pause || !pause.MenuReady) return;
                var fight = pause.GetComponent<FightCombatController>();
                if (fight.AwaitingBattleStart) { fight.TryBeginBattle(); return; }
                if (fight.BattleEntranceRunning || fight.WaveEntranceRunning || fight.WaveTransitionActive) return;
                pause.SetPaused(true);
                if (++previewFrames < 25) return;
                if (previewFrames == 25)
                {
                    var content = GameObject.Find("PauseContent");
                    if (!content || content.GetComponentsInChildren<Image>().Count(i => i.name == "Portrait" && i.sprite) != 6)
                        throw new Exception("Expected six visible portraits");
                    if (content.GetComponentsInChildren<Text>().Count(t => t.text == "???") != 12)
                        throw new Exception("Expected four unknown fields on each of the final three cards");
                    ScreenCapture.CaptureScreenshot("Temp/Pause-hero-guide.png"); return;
                }
                if (previewFrames < 30) return;
                pause.SetPaused(false);
                File.WriteAllText("Temp/PauseGuidePreview.result", "PASS: Six portraits, twelve unknown fields, pause/resume; screenshot saved.");
            }
            catch (Exception e) { File.WriteAllText("Temp/PauseGuidePreview.result", "FAIL: " + e); }
            File.Delete("Temp/PauseGuidePreview.request"); previewFrames = 0; EditorApplication.isPlaying = false;
        }
        [MenuItem("Rhythm Hunter/Pause/Configure Hero Guide")]
        static void Configure()
        {
            const string folder = "Assets/FightDemo/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/FightDemo", "Resources");
            const string path = folder + "/FightHeroGuide.asset";
            var guide = AssetDatabase.LoadAssetAtPath<FightHeroGuide>(path);
            if (!guide) { guide = ScriptableObject.CreateInstance<FightHeroGuide>(); AssetDatabase.CreateAsset(guide, path); }
            string[] files = { "hero", "mage", "skelenton", "herbalist", "minotaur", "dwarf" };
            string[] names = { "艾力克斯", "瑞緹絲與路克", "死亡比利", "???", "???", "???" };
            string[] jobs = { "戰士", "法師", "遊俠", "???", "???", "???" };
            string[] roles = { "主輸出", "防禦／削弱", "遠程輸出", "???", "???", "???" };
            string[] abilities = { "斬擊、連續充能斬擊", "格擋一次傷害\n黏液降低敵人攻擊", "瞄準射擊\n標記與追擊", "???", "???", "???" };
            guide.heroes = new FightHeroGuide.Entry[6];
            for (int i = 0; i < 6; i++)
            {
                string portraitPath = "Assets/FightDemo/Arts/全角色頭像/" + files[i] + ".PNG";
                var portrait = AssetDatabase.LoadAllAssetsAtPath(portraitPath).OfType<Sprite>().FirstOrDefault();
                if (!portrait) throw new Exception("Missing sprite: " + portraitPath);
                guide.heroes[i] = new FightHeroGuide.Entry { portrait = portrait, characterName = names[i], profession = jobs[i], role = roles[i], abilities = abilities[i] };
            }
            EditorUtility.SetDirty(guide); AssetDatabase.SaveAssets();
        }
    }
}
