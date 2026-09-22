using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightRosterManager)), CanEditMultipleObjects]
    public sealed class FightRosterManagerEditor : Editor
    {
        private static readonly string[] Positions = { "Front — X / Q", "Middle — Y / W", "Back — B / E" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "heroSpawnSlots", "heroPrefabs");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Formation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("FightScene3: any hero can occupy any position. Empty positions keep their input binding. Changes take effect on the next roster spawn.", MessageType.Info);
            SerializedProperty slots = serializedObject.FindProperty("heroSpawnSlots");
            SerializedProperty heroes = serializedObject.FindProperty("heroPrefabs");
            for (int i = 0; i < 3; i++)
            {
                EditorGUILayout.LabelField(Positions[i], EditorStyles.boldLabel);
                if (i < slots.arraySize)
                    EditorGUILayout.PropertyField(slots.GetArrayElementAtIndex(i), new GUIContent("Spawn Slot"));
                if (i < heroes.arraySize)
                    EditorGUILayout.PropertyField(heroes.GetArrayElementAtIndex(i), new GUIContent("Character Prefab"));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
