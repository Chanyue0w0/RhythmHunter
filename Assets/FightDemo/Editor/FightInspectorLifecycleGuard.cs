using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>Do not carry transient scene/Prefab Stage selections across domain
    /// reloads: Unity's built-in inspectors can try to serialize destroyed targets.</summary>
    [InitializeOnLoad]
    public static class FightInspectorLifecycleGuard
    {
        static FightInspectorLifecycleGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseTransientSelection;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ReleaseTransientSelection();
        }

        private static void ReleaseTransientSelection()
        {
            var selection = Selection.objects;
            var persistent = selection.Where(item => item != null && EditorUtility.IsPersistent(item)).ToArray();
            if (persistent.Length == selection.Length) return;
            Selection.objects = persistent;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        [MenuItem("Rhythm Hunter/Tools/Repair Inspector Selection")]
        private static void RepairInspector()
        {
            // Explicit recovery only; never automatically unlock the user's inspector.
            ActiveEditorTracker.sharedTracker.isLocked = false;
            Selection.objects = Array.Empty<UnityEngine.Object>();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
