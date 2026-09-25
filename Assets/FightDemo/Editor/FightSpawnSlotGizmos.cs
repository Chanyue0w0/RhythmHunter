using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    public static class FightSpawnSlotGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void Draw(FightUnitSlot slot, GizmoType type)
        {
            if (slot.gameObject.scene.name != "FightScene3") return;
            var position = slot.ActorRoot != null ? slot.ActorRoot.position : slot.transform.position;
            var previous = Handles.color;
            Handles.color = slot.Team == FightUnitSlot.UnitTeam.Hero ? Color.cyan : new Color(1, .45f, .25f);
            Handles.DrawWireDisc(position, Vector3.forward, .22f);
            Handles.Label(position + Vector3.down * .35f, slot.name + " (Spawn)");
            Handles.color = previous;
        }
    }
}
