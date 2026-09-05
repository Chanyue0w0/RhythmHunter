using RhythmHunter.RhythmArena;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.TopDownBeatCombatEditor
{
    [CustomEditor(typeof(RhythmClock))]
    public sealed class RhythmClockTimingInspector : Editor
    {
        private SerializedProperty perfectWindow;
        private SerializedProperty goodWindow;
        private SerializedProperty bpm;

        private void OnEnable()
        {
            perfectWindow = serializedObject.FindProperty("perfectWindowBeats");
            goodWindow = serializedObject.FindProperty("goodWindowBeats");
            bpm = serializedObject.FindProperty("bpm");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "perfectWindowBeats", "goodWindowBeats");

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Attack Timing Windows (Manual)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(perfectWindow, new GUIContent("Perfect Window (± beats)"));
            EditorGUILayout.PropertyField(goodWindow, new GUIContent("Good Window (± beats)"));
            serializedObject.ApplyModifiedProperties();

            float secondsPerBeat = 60f / Mathf.Max(1f, bpm.floatValue);
            float perfectMs = perfectWindow.floatValue * secondsPerBeat * 1000f;
            float goodMs = goodWindow.floatValue * secondsPerBeat * 1000f;
            EditorGUILayout.HelpBox(
                $"At {bpm.floatValue:0.#} BPM: Perfect ±{perfectMs:0} ms, Good ±{goodMs:0} ms. " +
                "Adjust both sliders here; Good is automatically kept at least as wide as Perfect.",
                MessageType.Info);
        }
    }
}
