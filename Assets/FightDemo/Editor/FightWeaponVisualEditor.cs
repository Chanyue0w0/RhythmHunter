using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    [CustomEditor(typeof(FightWeaponVisual))]
    public sealed class FightWeaponVisualEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var weapon = (FightWeaponVisual)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", weapon.CurrentStateId ?? "Not started");
            EditorGUILayout.HelpBox("每張 Sprite 的 Beats 可使用小數。Play 時選取勇者 WeaponAnchor 下的武器，即可用下列按鈕測試；Edit 模式可用 Weapon Attachment 預覽視窗檢查圖片與定位。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || EditorUtility.IsPersistent(weapon)))
                foreach (var state in weapon.AnimationStates)
                    if (state != null && GUILayout.Button(state.stateId)) weapon.SetWeaponAnimationState(state.stateId);
        }
    }
}
