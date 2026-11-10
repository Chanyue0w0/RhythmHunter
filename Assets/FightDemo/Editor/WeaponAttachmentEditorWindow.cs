using System;
using System.Collections.Generic;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public sealed class WeaponAttachmentEditorWindow : EditorWindow
    {
        private GameObject characterPrefab, contents;
        private FightCharacterCombatAnimator animator;
        private WeaponAttachmentData data;
        private FightWeaponVisual weaponPrefab;
        private WeaponAttachmentData.Pose pose;
        private int sourceChoice, frame, pointTool, previewDirection = 1;
        private bool snap = true, playing, unsavedPose, mirrorX, mirrorY;
        private float zoom = 100, previewBpm = 120;
        private double lastTime, elapsed;
        private PreviewRenderUtility preview;
        private SpriteRenderer bodyPreview, weaponPreview;
        private Material previewMaterial;
        private readonly List<int> sources = new();
        private readonly List<string> labels = new();
        private int Source => sources.Count > 0 ? sources[Mathf.Clamp(sourceChoice, 0, sources.Count - 1)] : -1;
        private IReadOnlyList<Sprite> Frames => animator == null ? Array.Empty<Sprite>() : Source < 0 ? animator.Frames : animator.FindSequence((FightCharacterCombatAnimator.CombatAnimation)Source).Frames;
        private Sprite CurrentSprite => frame >= 0 && frame < Frames.Count ? Frames[frame] : null;

        [MenuItem("Rhythm Hunter/Weapons/Weapon Attachment Editor")]
        public static void Open() => GetWindow<WeaponAttachmentEditorWindow>("Weapon Attachment");
        private void OnEnable() { minSize = new Vector2(540, 650); lastTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; Undo.undoRedoPerformed += RefreshUndo; }
        private void RefreshUndo() { LoadPose(); Repaint(); }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= RefreshUndo;
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            preview?.Cleanup(); preview = null;
            if (previewMaterial != null) DestroyImmediate(previewMaterial);
        }
        private void Load(GameObject value)
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            contents = null; animator = null; data = null; weaponPrefab = null;
            characterPrefab = value; sources.Clear(); labels.Clear(); playing = false; unsavedPose = false;
            string path = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) return;
            contents = PrefabUtility.LoadPrefabContents(path);
            animator = contents.GetComponentInChildren<FightCharacterCombatAnimator>(true);
            if (animator == null) return;
            animator.EnsureAttachmentFrameIds(); // Saved only by the explicit binding/save button.
            if (animator.FrameCount > 0) { sources.Add(-1); labels.Add("Legacy / Drunk Idle"); }
            foreach (var sequence in animator.Sequences)
                if (sequence != null && sequence.Frames.Count > 0 && !sources.Contains((int)sequence.Animation))
                { sources.Add((int)sequence.Animation); labels.Add(sequence.Animation.ToString()); }
            var binding = contents.GetComponentInChildren<FightWeaponAttachment>(true);
            if (binding != null) { data = binding.Data; weaponPrefab = binding.Prefab; }
            sourceChoice = Mathf.Max(0, sources.IndexOf((int)FightCharacterCombatAnimator.CombatAnimation.OpeningIdle));
            frame = 0; pose = new WeaponAttachmentData.Pose { visible = true, rotation = 90 };
            LoadPose();
        }
        private void LoadPose()
        {
            if (animator == null || Frames.Count == 0) return;
            string id = animator.AttachmentFrameId(Source, frame);
            if (data != null && data.TryGet(id, out var saved)) pose = saved;
            // Missing frames inherit the previous pose, without overwriting any saved frame.
            pose.frameId = id; unsavedPose = false;
        }
        private void SavePose()
        {
            if (data == null || animator == null || playing) return;
            Undo.RecordObject(data, "Set weapon attachment pose");
            pose.frameId = animator.AttachmentFrameId(Source, frame);
            data.Set(pose); EditorUtility.SetDirty(data); unsavedPose = false;
        }
        private void ChangeFrame(int index)
        {
            if (Frames.Count == 0) return;
            if (unsavedPose && !playing) SavePose();
            frame = (index % Frames.Count + Frames.Count) % Frames.Count; elapsed = 0; previewDirection = 1; LoadPose(); Repaint();
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup, delta = now - lastTime; lastTime = now;
            if (!playing || animator == null || Frames.Count == 0) return;
            elapsed += delta;
            var sequence = Source < 0 ? null : animator.FindSequence((FightCharacterCombatAnimator.CombatAnimation)Source);
            var serialized = new SerializedObject(animator);
            float Duration()
            {
                if (sequence != null) return sequence.UseFrameBeatDurations ? sequence.GetFrameBeats(frame) * 60 / previewBpm : 1 / sequence.FramesPerSecond;
                if (serialized.FindProperty("useIdleFrameBeatDurations").boolValue)
                {
                    var durations = serialized.FindProperty("idleFrameBeatDurations");
                    return (frame < durations.arraySize ? durations.GetArrayElementAtIndex(frame).floatValue : .5f) * 60 / previewBpm;
                }
                int length = animator.PingPong && Frames.Count > 2 ? Frames.Count * 2 - 2 : Frames.Count;
                return 60 / previewBpm / Mathf.Max(.01f, animator.CyclesPerBeat) / length;
            }
            int steps = 0;
            while (elapsed >= Mathf.Max(.001f, Duration()) && steps++ < 1000)
            {
                elapsed -= Mathf.Max(.001f, Duration());
                if (Source < 0 && animator.PingPong && Frames.Count > 2)
                {
                    if (frame == Frames.Count - 1) previewDirection = -1;
                    else if (frame == 0) previewDirection = 1;
                    frame += previewDirection;
                }
                else frame = (frame + 1) % Frames.Count;
                LoadPose(); Repaint();
            }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("第一階段：靜態武器掛載。預覽不呼叫戰鬥事件；BPM 僅供編輯器預覽。切換影格會保留本幀修改，最後請按『儲存掛載與角色』。", MessageType.Info);
            var selected = (GameObject)EditorGUILayout.ObjectField("角色 Prefab", characterPrefab, typeof(GameObject), false);
            if (selected != characterPrefab) Load(selected);
            if (animator == null || sources.Count == 0) { EditorGUILayout.LabelField("請選擇含 FightCharacterCombatAnimator 的角色 Prefab。"); return; }
            var chosenData = (WeaponAttachmentData)EditorGUILayout.ObjectField("定位資料", data, typeof(WeaponAttachmentData), false);
            if (chosenData != data) { data = chosenData; LoadPose(); }
            if (data == null && GUILayout.Button("建立定位資料"))
            {
                string path = EditorUtility.SaveFilePanelInProject("建立定位資料", characterPrefab.name + "_WeaponAttachment", "asset", "選擇存放位置");
                if (!string.IsNullOrEmpty(path)) { data = CreateInstance<WeaponAttachmentData>(); AssetDatabase.CreateAsset(data, path); LoadPose(); }
            }
            weaponPrefab = (FightWeaponVisual)EditorGUILayout.ObjectField("武器 Prefab", weaponPrefab, typeof(FightWeaponVisual), false);
            int nextSource = EditorGUILayout.Popup("角色動畫", sourceChoice, labels.ToArray());
            if (nextSource != sourceChoice) { if (unsavedPose) SavePose(); sourceChoice = nextSource; frame = 0; elapsed = 0; LoadPose(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一幀")) { playing = false; ChangeFrame(frame - 1); }
                GUILayout.Label($"{frame + 1} / {Frames.Count}");
                if (GUILayout.Button("下一幀")) { playing = false; ChangeFrame(frame + 1); }
                bool play = GUILayout.Toggle(playing, "預覽", "Button");
                if (play != playing) { if (unsavedPose) SavePose(); playing = play; elapsed = 0; }
            }
            previewBpm = Mathf.Max(1, EditorGUILayout.FloatField("預覽 BPM", previewBpm));
            zoom = EditorGUILayout.Slider("顯示倍率（不改武器尺寸）", zoom, 20, 400);
            using (new EditorGUILayout.HorizontalScope())
            {
                mirrorX = EditorGUILayout.Toggle("預覽水平翻面", mirrorX);
                mirrorY = EditorGUILayout.Toggle("預覽垂直翻面", mirrorY);
            }
            using (new EditorGUI.DisabledScope(playing))
            {
                pointTool = GUILayout.Toolbar(pointTool, new[] { "Grip Point：放置握點", "Direction Point：設定朝向" });
                snap = EditorGUILayout.Toggle("Pixel Snap（角色 PPU）", snap);
                EditorGUI.BeginChangeCheck();
                pose.position = EditorGUILayout.Vector2Field("武器握點（角色圖片本地座標）", pose.position);
                pose.rotation = EditorGUILayout.FloatField("朝向角度", pose.rotation);
                pose.visible = EditorGUILayout.Toggle("本幀顯示武器", pose.visible);
                if (EditorGUI.EndChangeCheck()) unsavedPose = true;
            }
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreview(rect);
            using (new EditorGUI.DisabledScope(data == null || weaponPrefab == null || playing || EditorApplication.isPlaying))
                if (GUILayout.Button("儲存掛載與角色（只修改所選 Prefab）"))
                {
                    SavePose();
                    var binding = contents.GetComponentInChildren<FightWeaponAttachment>(true);
                    if (binding == null) binding = contents.AddComponent<FightWeaponAttachment>();
                    binding.Configure(animator, data, weaponPrefab);
                    PrefabUtility.SaveAsPrefabAsset(contents, AssetDatabase.GetAssetPath(characterPrefab));
                    AssetDatabase.SaveAssets();
                }
        }
        private void EnsurePreview()
        {
            if (preview != null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.transform.position = new Vector3(0, 0, -10);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.13f, .14f, .16f);
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
            SpriteRenderer Create(string name)
            {
                var go = new GameObject(name); var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = previewMaterial; preview.AddSingleGO(go); return renderer;
            }
            bodyPreview = Create("Body preview"); weaponPreview = Create("Weapon preview");
            weaponPreview.sortingOrder = 1;
        }
        private void DrawPreview(Rect rect)
        {
            if (rect.width < 1 || rect.height < 1) return;
            EnsurePreview();
            Vector2 origin = rect.center;
            Vector2 ToScreen(Vector2 p) => origin + new Vector2(p.x * (mirrorX ? -1 : 1), -p.y * (mirrorY ? -1 : 1)) * zoom;
            if (Event.current.type == EventType.Repaint)
            {
                bodyPreview.sprite = CurrentSprite;
                bodyPreview.flipX = mirrorX; bodyPreview.flipY = mirrorY;
                weaponPreview.sprite = weaponPrefab != null && pose.visible ? weaponPrefab.Renderer?.sprite : null;
                if (weaponPrefab != null)
                {
                    var rotation = Quaternion.Euler(0, 0, pose.rotation - weaponPrefab.ForwardAngle);
                    // Preview the complete renderer-to-root transform, including its authored pivot and scale.
                    var wr = weaponPrefab.Renderer;
                    if (wr != null)
                    {
                        Matrix4x4 local = weaponPrefab.transform.worldToLocalMatrix * wr.transform.localToWorldMatrix;
                        Vector3 scale = weaponPrefab.transform.localScale;
                        Vector3 grip = Vector3.Scale(weaponPrefab.GripPoint, scale);
                        weaponPreview.transform.localPosition = (Vector3)pose.position + rotation * (Vector3.Scale(local.MultiplyPoint3x4(Vector3.zero), scale) - grip);
                        weaponPreview.transform.localRotation = rotation * local.rotation;
                        weaponPreview.transform.localScale = Vector3.Scale(local.lossyScale, scale);
                        weaponPreview.flipX = wr.flipX; weaponPreview.flipY = wr.flipY;
                        var reflection = new Vector3(mirrorX ? -1 : 1, mirrorY ? -1 : 1, 1);
                        weaponPreview.transform.localPosition = Vector3.Scale(weaponPreview.transform.localPosition, reflection);
                        // Mirror in the parent plane, not around the weapon's own rotated axis.
                        float angle = weaponPreview.transform.localEulerAngles.z;
                        weaponPreview.transform.localRotation = Quaternion.Euler(0, 0, mirrorX != mirrorY ? -angle : angle);
                        weaponPreview.transform.localScale = Vector3.Scale(weaponPreview.transform.localScale, reflection);
                    }
                }
                preview.camera.orthographicSize = rect.height / (2 * zoom);
                preview.BeginPreview(rect, GUIStyle.none); preview.camera.Render(); GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
                Handles.BeginGUI(); Handles.color = Color.green;
                Vector2 gripScreen = ToScreen(pose.position);
                Vector2 direction = new Vector2(Mathf.Cos(pose.rotation * Mathf.Deg2Rad), Mathf.Sin(pose.rotation * Mathf.Deg2Rad));
                Handles.DrawLine(gripScreen, ToScreen(pose.position + direction * .5f));
                Handles.DrawWireDisc(gripScreen, Vector3.forward, 5);
                Handles.EndGUI();
            }
            var e = Event.current;
            if (!playing && rect.Contains(e.mousePosition) && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                Vector2 p = (e.mousePosition - origin) / zoom; p.y = -p.y;
                if (mirrorX) p.x = -p.x;
                if (mirrorY) p.y = -p.y;
                if (snap && CurrentSprite != null)
                {
                    Vector2 pixel = p * CurrentSprite.pixelsPerUnit + CurrentSprite.pivot;
                    p = (new Vector2(Mathf.Round(pixel.x), Mathf.Round(pixel.y)) - CurrentSprite.pivot) / CurrentSprite.pixelsPerUnit;
                }
                if (pointTool == 0) pose.position = p;
                else if ((p - pose.position).sqrMagnitude > .000001f) pose.rotation = Mathf.Atan2(p.y - pose.position.y, p.x - pose.position.x) * Mathf.Rad2Deg;
                unsavedPose = true; e.Use(); Repaint();
            }
        }
    }
}
