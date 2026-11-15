using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    internal static class FightNamedAnimationValidation
    {
        const string Request = "Temp/FightNamedAnimationValidation.request";
        static FightNamedAnimationValidation() => EditorApplication.update += Poll;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            bool pool = File.ReadAllText(Request).Trim() == "pool";
            File.Delete(Request);
            try { if (pool) ValidateKingPool(); else Run(); }
            catch(Exception e) { File.WriteAllText("Temp/FightNamedAnimationValidation.result", "FAIL: " + e); }
        }
        static void ValidateKingPool()
        {
            const string path="Assets/FightDemo/Prefabs/ArtBattle/Goblin_King.prefab";
            var bytes=File.ReadAllBytes(path);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FightCharacterDefinition>();
            var counts=new System.Collections.Generic.Dictionary<string,int>();
            for(int i=0;i<1000;i++) { var name=asset.SelectEnemyAbility((i+.5f)/1000f).name; counts[name]=counts.TryGetValue(name,out int n)?n+1:1; }
            Check(counts["Strike"]==400 && counts["HeavyStrike"]==400 && counts["EatFruit"]==200,"Incorrect 40/40/20 distribution");
            var preview=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("King pool fixture"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var fight=root.AddComponent<FightCombatController>();
                var go=new GameObject("King slot");go.transform.SetParent(root.transform);
                var slot=go.AddComponent<FightUnitSlot>();slot.SpawnCharacter(asset,null);
                var definition=slot.CharacterDefinition;
                var eat=definition.EnemyAbilities.First(a=>a.name=="EatFruit");
                var seq=slot.CombatAnimator.FindSequence(eat.Animation);
                var authored=asset.GetComponentInChildren<FightCharacterCombatAnimator>(true).FindSequence(eat.Animation);
                Check(seq!=null && seq.Name=="EatFruit" && seq.Frames.Count>0 && JsonUtility.ToJson(seq)==JsonUtility.ToJson(authored),"EatFruit authored data/reference incorrect");
                var animationJson=JsonUtility.ToJson(seq);
                foreach(var ability in definition.EnemyAbilities) ability.selectionWeight=ability==eat?1:0;
                Check(definition.SelectEnemyAbility(.5f)==eat,"Zero-weight entries selected");
                var start=typeof(FightCombatController).GetMethod("TryStartAuthoredEnemyAttack",BindingFlags.NonPublic|BindingFlags.Instance);
                Check((bool)start.Invoke(fight,new object[]{slot}),"Non-damaging ability rejected");
                Check(slot.CombatAnimator.IsPlaying && slot.CombatAnimator.ActiveAnimation==eat.Animation,"EatFruit not playing");
                Check(fight.ScheduledEnemyHitCount==0,"EatFruit scheduled damage");
                Check(eat.healOnEffectFrame==30,"EatFruit healing configuration incorrect");
                var hp=typeof(FightUnitSlot).GetField("currentHp",BindingFlags.NonPublic|BindingFlags.Instance);
                var advance=typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback",BindingFlags.NonPublic|BindingFlags.Instance);
                float seconds=0;
                int effectIndex=Enumerable.Range(0,seq.Frames.Count).First(i=>seq.EffectsAt(i)!=null && seq.EffectsAt(i).Any(e=>e!=null && e.prefab==eat.healingEffectPrefab));
                for(int i=0;i<effectIndex;i++) seconds+=seq.GetFrameBeats(i)*.5f;
                hp.SetValue(slot,1f);
                advance.Invoke(slot.CombatAnimator,new object[]{seconds-.001f});
                Check(slot.CurrentHp==1,"Healing occurred before effect frame");
                advance.Invoke(slot.CombatAnimator,new object[]{.002f});
                Check(slot.CurrentHp==Mathf.Min(slot.MaxHp,31),"Effect frame did not heal 30");
                var callback=(Action<FightCharacterCombatAnimator.FrameEffect>)typeof(FightCharacterCombatAnimator).GetField("frameEffectCallback",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(slot.CombatAnimator);
                callback(seq.EffectsAt(effectIndex).First(e=>e.prefab==eat.healingEffectPrefab)); Check(slot.CurrentHp==Mathf.Min(slot.MaxHp,31),"Healing repeated in same cast");
                slot.CombatAnimator.InterruptPlayback(); hp.SetValue(slot,slot.MaxHp-5);
                start.Invoke(fight,new object[]{slot}); advance.Invoke(slot.CombatAnimator,new object[]{seconds+.001f});
                Check(slot.CurrentHp==slot.MaxHp,"Healing exceeded maximum HP");
                slot.CombatAnimator.InterruptPlayback(); hp.SetValue(slot,1f);
                start.Invoke(fight,new object[]{slot}); slot.CombatAnimator.InterruptPlayback();
                advance.Invoke(slot.CombatAnimator,new object[]{seconds+1}); Check(slot.CurrentHp==1,"Interrupted action healed");
                start.Invoke(fight,new object[]{slot}); hp.SetValue(slot,0f);
                advance.Invoke(slot.CombatAnimator,new object[]{seconds+.001f}); Check(slot.CurrentHp==0,"Healing revived dead enemy");
                Check(JsonUtility.ToJson(seq)==animationJson,"Animation data mutated");
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)),"Validation modified authored prefab");
                File.WriteAllText("Temp/FightNamedAnimationValidation.result",$"PASS: Strike 40% / HeavyStrike 40% / EatFruit 20%; EatFruit schedules no damage; heals 30 only at effect frame, once per cast, capped at max HP; interruption/death prevents healing; {seq.Frames.Count} authored frames unchanged.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
        static void Run()
        {
            int sequences = 0, prefabs = 0;
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FightDemo/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var bytes = paths.ToDictionary(p => p, File.ReadAllBytes);
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                foreach(var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    foreach(var original in asset.GetComponentsInChildren<FightCharacterCombatAnimator>(true))
                    {
                        prefabs++;
                        // Deserialize existing data as-is: no migration, asset rewriting or default list rebuild.
                        root = new GameObject("Animation compatibility fixture");
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                        var copy = root.AddComponent<FightCharacterCombatAnimator>();
                        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original), copy);
                        Check(copy.Sequences.Count == original.Sequences.Count, path + " sequence count changed");
                        for (int i=0;i<copy.Sequences.Count;i++)
                        {
                            var before = original.Sequences[i]; var after = copy.Sequences[i];
                            Check(JsonUtility.ToJson(before) == JsonUtility.ToJson(after), path + " frame/timing/event data changed");
                            var id = after.Animation; string pose = after.AttachmentFrameId(0);
                            after.Rename("Test_" + i);
                            Check(after.Animation == id && after.AttachmentFrameId(0) == pose, "Rename changed playback/weapon identity");
                            sequences++;
                        }
                        UnityEngine.Object.DestroyImmediate(root); root=null;
                    }
                }
                root = new GameObject("Custom animation playback fixture");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var renderer = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<FightCharacterCombatAnimator>();
                var sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FightDemo/Prefabs/ArtBattle/LongSwordWarrior_Weapon.prefab")
                    .GetComponentInChildren<FightCharacterCombatAnimator>().Sequences.SelectMany(s=>s.Frames).First(s=>s!=null);
                animator.Configure(renderer, null); animator.ConfigureIdle(null,renderer,new[]{sprite});
                var custom = animator.AddCustomSequence("Boss_Spin");
                custom.Configure(FightCharacterCombatAnimator.CombatAnimation.NormalAttack,new[]{sprite,sprite},16,-1,1,-1);
                custom.ConfigureFrameBeats(new[]{.5f,.5f}); animator.EnsureAttachmentFrameIds();
                var handle=custom.Animation; string frameId=custom.AttachmentFrameId(1);
                var other=animator.AddCustomSequence("Boss_Roar");
                var so=new SerializedObject(animator); so.FindProperty("sequences").MoveArrayElement(0,1); so.ApplyModifiedPropertiesWithoutUndo();
                custom = animator.FindSequence(handle);
                Check(custom != null && custom.AttachmentFrameId(1) == frameId,"Reorder changed stable identity");
                custom.Rename("Boss_Whirlwind");
                Check(animator.FindSequence(handle)==custom && animator.FindSequence("Boss_Whirlwind")==custom,"Rename/reorder broke reference");
                int attacks=0;
                Check(animator.Play("Boss_Whirlwind",onAttackEffect:()=>attacks++),"Named playback failed");
                var advance=typeof(FightCharacterCombatAnimator).GetMethod("AdvancePlayback",BindingFlags.NonPublic|BindingFlags.Instance);
                advance.Invoke(animator,new object[]{.251f});
                Check(attacks==1 && animator.DisplayedFrame.Id==frameId && animator.DisplayedFrame.Source==(int)handle,"Beat event/frame/weapon identity failed");
                animator.SetPlaybackPaused(true); advance.Invoke(animator,new object[]{1f});
                Check(animator.ActiveFrame==1 && attacks==1,"Pause advanced custom animation");
                animator.SetPlaybackPaused(false); advance.Invoke(animator,new object[]{.251f});
                Check(!animator.IsPlaying && attacks==1,"Custom animation did not finish normally");
                string saved=EditorJsonUtility.ToJson(animator); EditorJsonUtility.FromJsonOverwrite(saved,animator);
                Check(animator.FindSequence(handle).AttachmentFrameId(1)==frameId,"Serialization changed identity");
                so=new SerializedObject(animator); so.FindProperty("sequences").DeleteArrayElementAtIndex(1); so.ApplyModifiedPropertiesWithoutUndo();
                Check(animator.FindSequence(handle)==null,"Deleted custom still resolves");
                Check((int)animator.AddCustomSequence("Boss_New").Animation>(int)other.Animation,"Deleted identity reused");
                var ability=JsonUtility.FromJson<FightCharacterDefinition.EnemyAbility>("{\"animation\":2}");
                Check(ability.Animation==FightCharacterCombatAnimator.CombatAnimation.HeavyAttack,"Legacy Boss binding changed");
                ability=JsonUtility.FromJson<FightCharacterDefinition.EnemyAbility>("{\"animation\":0,\"animationSourceId\":1001}");
                Check((int)ability.Animation==1001,"Custom Boss binding failed");
                foreach(var path in paths) Check(bytes[path].SequenceEqual(File.ReadAllBytes(path)),"Asset modified: "+path);
                File.WriteAllText("Temp/FightNamedAnimationValidation.result",$"PASS: {prefabs} animator components, {sequences} existing sequences preserved; rename/reorder stable IDs; named playback, half-beat event, pause/resume, completion, serialization, deletion/nonreuse; legacy/custom Boss references; all original prefab bytes unchanged.");
            }
            finally { if(root!=null) UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
