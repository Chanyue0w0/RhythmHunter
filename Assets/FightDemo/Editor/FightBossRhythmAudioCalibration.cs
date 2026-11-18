using System;
using System.Collections.Generic;
using FMODUnity;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public sealed partial class FightBossRhythmChartWindow
    {
        [SerializeField] private bool autoAudition, repeatAudition;
        [SerializeField] private float auditionStartBeat=16, auditionEndBeat=32, auditionSoundOffsetMs;
        private FightBeatSoundScheduler auditionSounds;
        private readonly HashSet<int> auditionSent=new();
        private double auditionSeekTarget=-1;
        private string auditionEvent;
        private float auditionVolume;
        private void ClearAuditionSounds()
        {auditionSounds?.Dispose();auditionSounds=null;auditionSent.Clear();auditionSeekTarget=-1;}
        private void SeekPreview(double seconds)
        {
            cursor=Math.Clamp(seconds,0,duration);ClearAuditionSounds();
            if(preview.isValid()){preview.setTimelinePosition(startMs+(int)Math.Round(cursor*1000));auditionSeekTarget=cursor;}
        }
        private void UpdateAutoAudition()
        {
            if(!autoAudition || !preview.isValid() || !chart || !previewStarted)return;
            if(auditionSeekTarget>=0)
            {if(Math.Abs(cursor-auditionSeekTarget)>.2)return;auditionSeekTarget=-1;}
            double from=auditionStartBeat*MsPerBeat/1000,to=Math.Min(duration,auditionEndBeat*MsPerBeat/1000);
            if(cursor>=to && !paused)
            {if(repeatAudition)SeekPreview(from);else Stop();return;}
            if(auditionSounds==null)
            {
                var fight=FindFirstObjectByType<FightCombatController>();
                auditionEvent=fight?fight.BossHitSoundPath:"event:/SoundEffects/BeatTapping";
                auditionVolume=fight?fight.BossHitSoundVolume:.5f;
                auditionSounds=new FightBeatSoundScheduler(EditorUtils.System,auditionEvent);
                for(int i=0;i<chart.notes.Count;i++)if(Seconds(chart.notes[i])<cursor-.015)auditionSent.Add(i);
            }
            auditionSounds.Tick(cursor*1000,1,paused);
            if(paused || !auditionSounds.Ready)return;
            for(int i=0;i<chart.notes.Count;i++)
            {
                double arrival=Seconds(chart.notes[i]);double target=arrival*1000+auditionSoundOffsetMs;
                if(auditionSent.Contains(i)||arrival<from||arrival>=to||target>cursor*1000+250)continue;
                auditionSent.Add(i);
                if(target<cursor*1000-100)continue;
                auditionSounds.PlayAt(target,cursor*1000,1,auditionVolume,true,25);
            }
        }
        private void DrawAudioCalibration()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("音訊對拍測試（不參與戰鬥）",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("譜面時間決定打擊點；玩家輸入校正只影響判定；下列音效偏移只影響聲音；波形來源偏移只影響波形。四者不互相改寫。",MessageType.Info);
            bool enabled=EditorGUILayout.Toggle("自動對拍",autoAudition);
            if(enabled!=autoAudition){ClearAuditionSounds();autoAudition=enabled;recording=false;}
            auditionStartBeat=EditorGUILayout.FloatField("測試起拍",auditionStartBeat);
            auditionEndBeat=Mathf.Max(auditionStartBeat+.1f,EditorGUILayout.FloatField("測試迄拍",auditionEndBeat));
            EditorGUI.BeginChangeCheck();
            float offset=EditorGUILayout.FloatField("試聽音效偏移 ms（負值提前）",auditionSoundOffsetMs);
            if(EditorGUI.EndChangeCheck()){auditionSoundOffsetMs=offset;ClearAuditionSounds();}
            repeatAudition=EditorGUILayout.Toggle("循環此段",repeatAudition);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying || tempo<=0))
                if(GUILayout.Button("開始此段自動對拍"))
                {Stop();autoAudition=true;recording=false;cursor=Math.Clamp(auditionStartBeat*MsPerBeat/1000,0,duration);Play();}
            if(auditionSounds!=null)EditorGUILayout.LabelField($"{auditionEvent} · 時間軸排程 {auditionSounds.ScheduledCount} / 即時 {auditionSounds.ImmediateCount} · {auditionSounds.LastResult}",EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox("此處音效偏移只供試聽。確認數值後，手動填入 Fight Boss Rhythm Battle 的 Hit Sound Offset Ms；不會自動改譜面或場景。切換回普通試聽請取消自動對拍。",MessageType.Info);
        }
    }
    [CustomEditor(typeof(FightBossRhythmBattle))]
    public sealed class FightBossRhythmBattleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Hit Sound Offset Ms：只調音效，正值延後／負值提前。Minimum Sound Lead Ms：不足此提前量時立即播放。玩家輸入校正仍沿用 FmodRhythmJudge，只套用一次。",MessageType.Info);
            var battle=(FightBossRhythmBattle)target;
            var fight=battle.GetComponent<FightCombatController>();
            if(!Application.isPlaying || !fight)return;
            EditorGUILayout.LabelField("音效",fight.BossHitSoundPath);
            EditorGUILayout.LabelField("輸入誤差（負＝早／正＝晚）",$"{battle.LastInputErrorMs:F1} ms");
            EditorGUILayout.LabelField("成功樣本／平均／標準差",$"{battle.InputSampleCount} / {battle.MeanInputErrorMs:F1} / {battle.InputDeviationMs:F1} ms");
            EditorGUILayout.LabelField("既有輸入校正",$"{fight.BossRhythmJudge.JudgementOffsetMs:F1} ms");
            EditorGUILayout.LabelField("音效結果",fight.AudioPlayer.BeatSoundStatus);
            EditorGUILayout.LabelField("時間軸排程／即時",$"{fight.AudioPlayer.ScheduledBeatSounds} / {fight.AudioPlayer.ImmediateBeatSounds}");
            EditorGUILayout.HelpBox("此處是排程與成功輸入統計，不是喇叭實測延遲。Miss 不納入平均，避免把漏接配給錯誤音符。",MessageType.Info);
            Repaint();
        }
    }
}

