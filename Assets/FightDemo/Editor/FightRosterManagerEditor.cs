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
            bool locked = Application.isPlaying && ((FightRosterManager)target).FormationLocked;
            EditorGUILayout.HelpBox(locked ? "Formation is locked for this battle, including all waves."
                : "FightScene3: use the pre-battle panel to swap positions. X/Q, Y/W, B/E follow Front, Middle, Back.", MessageType.Info);
            SerializedProperty slots = serializedObject.FindProperty("heroSpawnSlots");
            SerializedProperty heroes = serializedObject.FindProperty("heroPrefabs");
            EditorGUI.BeginDisabledGroup(locked);
            for (int i = 0; i < 3; i++)
            {
                EditorGUILayout.LabelField(Positions[i], EditorStyles.boldLabel);
                if (i < slots.arraySize)
                    EditorGUILayout.PropertyField(slots.GetArrayElementAtIndex(i), new GUIContent("Spawn Slot"));
                if (i < heroes.arraySize)
                    EditorGUILayout.PropertyField(heroes.GetArrayElementAtIndex(i), new GUIContent("Character Prefab"));
            }
            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
