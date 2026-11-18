using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossRhythmValidation
    {
        const string Request = "Temp/BossRhythmValidation.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static double deadline;
        static int step;
        static string mode, progress;
        static FightBossRhythmChart chart;
        static HashSet<int> submitted = new();
        static bool captured, pauseChecked;
        static double pauseUntil;
        static double pausedBeat;
        static float initialArmor, initialHp;
        static FightBossRhythmValidation() => EditorApplication.update += Tick;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            { if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; return; }
            try
            {
                Application.runInBackground = true;
                if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 160; mode = File.ReadAllText(Request).Trim(); }
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout: " + progress);
                var fight = UnityEngine.Object.FindFirstObjectByType<FightCombatController>();
                if (!fight || fight.AwaitingBattleStart) return;
                var battle = fight.BossRhythm;
                Check(battle, "Boss rhythm component missing");
                progress = $"{mode} step={step}, beat={battle.CurrentBeat:F2}, hits={battle.Hits}, missed={battle.MissedNotes}, ended={fight.BattleEnded}, HP={fight.PartyHp}, chart={battle.ChartError}";
                File.WriteAllText("Temp/BossRhythmValidation.progress", progress);
                if (step == 0)
                {
                    chart = AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>("Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset");
                    Check(chart && chart.Validate(out _), "Chart invalid");
                    Check(chart.notes.Select(n => n.arrivalBeat).Distinct().Count() == chart.notes.Count, "Simultaneous notes");
                    var duplicate = UnityEngine.Object.Instantiate(chart); duplicate.notes.Add(duplicate.notes[0]);
                    Check(!duplicate.Validate(out _), "Duplicate beat allowed"); UnityEngine.Object.Destroy(duplicate);
                    if (mode == "offset")
                    {
                        chart = UnityEngine.Object.Instantiate(chart); // Never modify the user's asset.
                        double ms = fight.BeatClock.MillisecondsPerBeat;
                        for (int i = 0; i < chart.notes.Count; i++)
                        {
                            var n = chart.notes[i]; n.offsetMs = i < 4 ? (i % 2 == 0 ? 180 : -180) : (i % 2 == 0 ? 50 : -50);
                            if (i % 3 == 0) { n.useFreeTime = true; n.timeSeconds = n.arrivalBeat * ms / 1000; }
                            chart.notes[i] = n;
                        }
                        Check(chart.ValidateTiming(ms, 0, 0, fight.BossRhythmJudge.PerfectWindowMs, out _), "Offset/free chart invalid");
                        Set(battle, "chart", chart);
                    }
                    Set(fight, "endBattleOnZeroHp", true);
                    fight.GetComponent<FightBossStageDebug>().RequestPhaseTwoSkip();
                    step = 1;
                }
                else if (step == 1 && fight.GetComponent<FightBossPhaseTwoTransition>().Ready)
                {
                    Check(string.IsNullOrEmpty(battle.ChartError), battle.ChartError);
                    Check(battle.SkippedTransitionNotes == 0, "Default chart started before transition completed");
                    Check(battle.CurrentBeat < chart.notes[0].arrivalBeat - chart.travelBeats, "No reaction time for first note");
                    var presenter = UnityEngine.Object.FindFirstObjectByType<FightScenePresenter>();
                    var timeline = (RectTransform)typeof(FightScenePresenter).GetField("timelineRoot", Flags).GetValue(presenter);
                    Check(timeline && !timeline.gameObject.activeSelf, "Gem/BeatUI not hidden");
                    var bar = (UnityEngine.UI.Image)typeof(FightScenePresenter).GetField("stageCountdownFill", Flags).GetValue(presenter);
                    Check(bar && bar.color.r > .9f && bar.color.g < .3f, "Boss timer is not red");
                    if (mode == "lose") { Set(fight, "partyArmor", 0f); Set(fight, "partyHp", 1f); }
                    initialArmor = fight.PartyArmor; initialHp = fight.PartyHp;
                    step = 2;
                }
                else if (step == 2)
                {
                    if (!pauseChecked && battle.CurrentBeat > 12 && battle.CurrentBeat < 15)
                    {
                        if (pauseUntil == 0) { fight.BeatClock.SetPaused(true); pauseUntil = EditorApplication.timeSinceStartup + .6; }
                        if (EditorApplication.timeSinceStartup < pauseUntil - .3) pausedBeat = battle.CurrentBeat;
                        if (EditorApplication.timeSinceStartup >= pauseUntil)
                        { Check(Math.Abs(battle.CurrentBeat - pausedBeat) < .01, "Chart advanced during pause"); fight.BeatClock.SetPaused(false); pauseChecked = true; }
                        return;
                    }
                    if (mode != "lose" && battle.Running)
                    {
                        // First note deliberately missed; every later note uses the production command/judge route.
                        fight.BeatClock.TryGetRawTimelinePositionMs(out int raw);
                        fight.BeatClock.TryGetMarkerPosition("Normal_End", out int start);
                        foreach (var note in chart.notes.Skip(1))
                        {
                            double target = note.EffectiveBeat(fight.BeatClock.MillisecondsPerBeat, battle.CurrentBeat, raw - start);
                            if (!submitted.Contains(note.arrivalBeat) && battle.CurrentBeat >= target - .14 && battle.CurrentBeat < target + .15)
                            { submitted.Add(note.arrivalBeat); fight.SubmitHeroCommand((FightInputRouter.HeroCommand)note.lane); }
                        }
                        if (battle.CurrentBeat >= 14 && battle.CurrentBeat < 14.2 && !submitted.Contains(-1))
                        {
                            submitted.Add(-1); fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Middle);
                            Check(battle.BadInputs == 1, "Empty timing did not apply Miss");
                            fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Back);
                            Check(battle.BadInputs == 1, "Miss lock did not suppress repeated input");
                        }
                    }
                    if (!captured && battle.CurrentBeat > 22 && battle.CurrentBeat < 23)
                    { ScreenCapture.CaptureScreenshot("Temp/BossRhythmGameplay.png"); captured = true; }
                    if (!fight.BattleEnded) return;
                    var results = UnityEngine.Object.FindFirstObjectByType<FightStageResults>();
                    Check(results.Completed, "Results did not complete");
                    if (mode == "lose")
                    { Check(!fight.BattleWon && fight.PartyHp == 0 && battle.MissedNotes == 1, "Miss did not cause existing defeat sequence"); }
                    else
                    {
                        Check(fight.BattleWon, "Survival did not win");
                        Check(battle.MissedNotes == 1 && battle.Hits == chart.notes.Count - 1, "Wrong hit/miss totals: " + progress);
                        Check(Math.Abs(fight.PartyArmor - (initialArmor - 1)) < .001 && fight.PartyHp == initialHp, "Miss was not exactly one armor-first damage");
                        Check(fight.RosterManager.ActiveEnemies.All(x => !x || x.ActorInstance == null), "Boss not removed after outro");
                        Check(battle.CreatedVisuals <= 16, "Visual pool grew unexpectedly");
                    }
                    File.WriteAllText("Temp/BossRhythmValidation_" + mode + ".result", "PASS: " + progress + "; actual native FMOD chart, calibrated input route, unique arrivals, pause, hidden gem/BeatUI, red timer, shared damage, pooled effects, result panel.");
                    Finish();
                }
            }
            catch (Exception e) { File.WriteAllText("Temp/BossRhythmValidation_" + mode + ".result", "FAIL: " + e); Finish(); }
        }
        static void Finish()
        {
            File.Delete(Request); step = 0; deadline = pauseUntil = 0; captured = pauseChecked = false; submitted.Clear();
            EditorApplication.isPlaying = false;
        }
    }
}

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossChartAuthoringValidation
    {
        const string Request = "Temp/BossChartAuthoring.request";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static FightBossRhythmChartWindow window;
        static FightBossRhythmChart scratch;
        static int step;
        static double next, before, previewDeadline;
        static object Get(string name) => typeof(FightBossRhythmChartWindow).GetField(name, Flags).GetValue(window);
        static void Set(string name, object value) => typeof(FightBossRhythmChartWindow).GetField(name, Flags).SetValue(window, value);
        static void Call(string name, params object[] args) => typeof(FightBossRhythmChartWindow).GetMethod(name, Flags).Invoke(window, args);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static FightBossChartAuthoringValidation() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (step == 0)
                {
                    scratch = ScriptableObject.CreateInstance<FightBossRhythmChart>();
                    scratch.GeneratePrototype();
                    Check(scratch.ValidateTiming(60000d/121, 0, 0, 120, out _), "Legacy chart invalid");
                    scratch.notes.Clear();
                    scratch.notes.Add(new FightBossRhythmChart.Note { arrivalBeat=20, offsetMs=100 });
                    scratch.notes.Add(new FightBossRhythmChart.Note { useFreeTime=true, timeSeconds=20*60d/121+.1 });
                    Check(!scratch.ValidateTiming(60000d/121, 0, 0, 120, out _), "Mixed-mode collision not detected");
                    scratch.notes.Clear();
                    window = EditorWindow.GetWindow<FightBossRhythmChartWindow>();
                    Set("chart", scratch); Call("LoadSong");
                    Check((double)Get("tempo") > 0, "No FMOD tempo");
                    Set("cursor", 10d); Set("snap", false); Call("Play"); next = EditorApplication.timeSinceStartup + 1; previewDeadline = next + 10; step++;
                }
                else if (step == 1)
                {
                    Call("Tick");
                    var previewDebug=(FMOD.Studio.EventInstance)Get("preview");
                    previewDebug.getTimelinePosition(out int rawDebug); previewDebug.getPlaybackState(out var stateDebug);
                    if ((double)Get("cursor") <= 10 && EditorApplication.timeSinceStartup < previewDeadline) return;
                    Check((double)Get("cursor") > 10, $"Native preview did not advance: cursor={Get("cursor")}, raw={rawDebug}, state={stateDebug}, valid={previewDebug.isValid()}, start={Get("startMs")}");
                    Call("Record", 0); Check(scratch.notes.Count == 1 && scratch.notes[0].useFreeTime, "Free recording failed");
                    Call("Play"); next = EditorApplication.timeSinceStartup + .4; step++;
                }
                else if (step == 2)
                { Call("Tick"); before = (double)Get("cursor"); next = EditorApplication.timeSinceStartup + .4; step++; }
                else if (step == 3)
                {
                    Call("Tick"); Check(Math.Abs((double)Get("cursor") - before) < .01, "Preview pause advanced");
                    var instance=(FMOD.Studio.EventInstance)Get("preview");
                    instance.setTimelinePosition((int)Get("startMs") + 15000); next=EditorApplication.timeSinceStartup+.3; step++;
                }
                else if (step == 4)
                {
                    Call("Tick"); Check(Math.Abs((double)Get("cursor") - 15) < .02, "Preview seek failed");
                    Set("snap", true); Call("Play"); next = EditorApplication.timeSinceStartup + .3; step++;
                }
                else
                {
                    Call("Tick"); Call("Record", 2);
                    Check(scratch.notes.Count == 2 && !scratch.notes[1].useFreeTime, "Snapped recording failed");
                    Call("Stop"); Check(!((FMOD.Studio.EventInstance)Get("preview")).isValid(), "Preview handle leaked");
                    File.WriteAllText("Temp/BossChartAuthoring.result", "PASS: legacy timing; mixed-mode overlap rejection; native FMOD preview/play/pause/seek; free and snapped recording; stop/release.");
                    Finish();
                }
            }
            catch(Exception e) { File.WriteAllText("Temp/BossChartAuthoring.result", "FAIL: " + e); Finish(); }
        }
        static void Finish()
        {
            if(window) { Call("Stop"); window.Close(); }
            if(scratch) UnityEngine.Object.DestroyImmediate(scratch);
            step=0; next=0; File.Delete(Request);
        }
    }
}

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossDenseJudgementValidation
    {
        static FightBossDenseJudgementValidation() => EditorApplication.update += Tick;
        static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
        static void Tick()
        {
            const string request="Temp/BossDenseJudgement.request";
            if(!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            GameObject go=null; FightBossRhythmChart chart=null;
            try
            {
                chart=ScriptableObject.CreateInstance<FightBossRhythmChart>();
                chart.notes.Add(new FightBossRhythmChart.Note { useFreeTime=true,timeSeconds=10,lane=FightBossRhythmChart.Lane.Left });
                chart.notes.Add(new FightBossRhythmChart.Note { useFreeTime=true,timeSeconds=10.1,lane=FightBossRhythmChart.Lane.Left });
                chart.notes.Add(new FightBossRhythmChart.Note { useFreeTime=true,timeSeconds=10.15,lane=FightBossRhythmChart.Lane.Right });
                Check(chart.ValidateTiming(500,0,0,120,out _),"Dense chart rejected");
                var user=AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>("Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset");
                Check(user.Validate(out _),"Current user chart rejected");
                go=new GameObject("DenseJudgeValidation"); var battle=go.AddComponent<FightBossRhythmBattle>();
                var type=typeof(FightBossRhythmBattle);
                const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var noteType=type.GetNestedType("Note",BindingFlags.NonPublic);
                var list=(System.Collections.IList)type.GetField("notes",flags).GetValue(battle);
                object Make(double beat,int lane)
                { var n=Activator.CreateInstance(noteType); noteType.GetField("beat").SetValue(n,beat); noteType.GetField("lane").SetValue(n,lane); list.Add(n); return n; }
                var first=Make(20,0); var second=Make(20.2,0); var right=Make(20.3,2);
                object Pick(int lane,double beat)=>type.GetMethod("FindNearestNote",flags).Invoke(battle,new object[]{lane,beat,.24d});
                Check(ReferenceEquals(Pick(0,20.18),second),"Nearest later note not selected");
                Check(ReferenceEquals(Pick(0,20.1),first),"Tie did not prefer earlier note");
                Check(ReferenceEquals(Pick(2,20.18),right),"Wrong lane selection");
                Check(Pick(1,20.18)==null,"Other lane consumed note");
                noteType.GetField("accepted").SetValue(second,true);
                Check(ReferenceEquals(Pick(0,20.18),first),"Accepted note consumed twice");
                noteType.GetField("resolved").SetValue(first,true);
                Check(Pick(0,20.18)==null,"Missed/resolved note was rescued");
                Check((float)type.GetField("missLockMilliseconds",flags).GetValue(battle)==80,"Wrong lock default");
                File.WriteAllText("Temp/BossDenseJudgement.result","PASS: user chart valid; 100ms/50ms overlaps allowed; nearest same-lane selection; earlier tie; no cross-lane/double consumption; missed notes excluded; 80ms default.");
            }
            catch(Exception e){File.WriteAllText("Temp/BossDenseJudgement.result","FAIL: "+e);}
            finally {if(go) UnityEngine.Object.DestroyImmediate(go); if(chart) UnityEngine.Object.DestroyImmediate(chart); File.Delete(request);}
        }
    }
}

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightBossAdvancedEditorValidation
    {
        static FightBossRhythmChartWindow window;
        static FightBossRhythmChart scratch;
        static double deadline;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Get(string n)=>typeof(FightBossRhythmChartWindow).GetField(n,Flags).GetValue(window);
        static void Set(string n,object v)=>typeof(FightBossRhythmChartWindow).GetField(n,Flags).SetValue(window,v);
        static object Call(string n,params object[] a)=>typeof(FightBossRhythmChartWindow).GetMethod(n,Flags).Invoke(window,a);
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static FightBossAdvancedEditorValidation()=>EditorApplication.update+=Tick;
        static void Tick()
        {
            const string request="Temp/BossAdvancedEditor.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try
            {
                if(!window)
                {
                    scratch=ScriptableObject.CreateInstance<FightBossRhythmChart>();
                    scratch.notes.Add(new FightBossRhythmChart.Note{useFreeTime=true,timeSeconds=10});
                    scratch.notes.Add(new FightBossRhythmChart.Note{useFreeTime=true,timeSeconds=10.1,lane=FightBossRhythmChart.Lane.Middle});
                    scratch.notes.Add(new FightBossRhythmChart.Note{useFreeTime=true,timeSeconds=10.4,lane=FightBossRhythmChart.Lane.Right});
                    window=EditorWindow.GetWindow<FightBossRhythmChartWindow>(); Set("chart",scratch); Call("LoadSong"); deadline=EditorApplication.timeSinceStartup+25;return;
                }
                Call("PollWaveform");
                var peaks=(float[])Get("peaks");
                if(peaks==null && EditorApplication.timeSinceStartup<deadline)return;
                Check(peaks!=null && peaks.Length>10000 && peaks.Max()>.01f,"Waveform failed: "+Get("waveformStatus"));
                var original=new Dictionary<int,double>{{0,10},{1,10.1},{2,10.4}};
                Call("TranslateSelection",original,.25d);
                Check(Math.Abs(scratch.notes[0].timeSeconds-10.25)<1e-6 && Math.Abs(scratch.notes[1].timeSeconds-scratch.notes[0].timeSeconds-.1)<1e-6,"Translation changed gap");
                Call("TranslateSelection",original,-20d);
                Check(scratch.notes[0].timeSeconds==0 && Math.Abs(scratch.notes[2].timeSeconds-.4)<1e-6,"Group boundary changed gap");
                Call("TranslateSelection",original,0d);
                var selection=(HashSet<int>)Get("selection");selection.Clear();selection.UnionWith(new[]{0,1,2}); Set("spacingBeats",.5f);
                Call("SpaceSelection");
                double spacing=30000/(double)Get("tempo")/1000;
                Check(Math.Abs(scratch.notes[2].timeSeconds-(10+spacing*2))<1e-6,"Equal spacing incorrect");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(Math.Abs(scratch.notes[2].timeSeconds-10.4)<1e-6,"Spacing undo failed");
                File.WriteAllText("Temp/BossAdvancedEditor.result","PASS: original MP3 waveform decoded, nonzero 2ms peaks; rigid group translation; boundary clamp preserves spacing; half-beat equal spacing; Undo restores timestamps.");
                Finish(request);
            }
            catch(Exception e){File.WriteAllText("Temp/BossAdvancedEditor.result","FAIL: "+e);Finish(request);}
        }
        static void Finish(string request)
        {
            if(window){Set("chart",AssetDatabase.LoadAssetAtPath<FightBossRhythmChart>("Assets/FightDemo/Levels/GoblinKing_PhaseTwo_Chart.asset"));Set("selected",-1);((HashSet<int>)Get("selection")).Clear(); window.position=new Rect(220,90,1050,850);window.Repaint();}
            if(scratch)UnityEngine.Object.DestroyImmediate(scratch); window=null;scratch=null;File.Delete(request);
        }
    }
}
