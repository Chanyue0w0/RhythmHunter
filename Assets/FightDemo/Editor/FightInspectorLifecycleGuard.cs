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
            // Reload can restore orphaned built-in editors even with an empty Selection.
            EditorApplication.delayCall += CleanupInvalidBuiltInInspectors;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ReleaseTransientSelection();
            else
                EditorApplication.delayCall += CleanupInvalidBuiltInInspectors;
        }

        private static void ReleaseTransientSelection()
        {
            var selection = Selection.objects;
            var persistent = selection.Where(item => item != null && EditorUtility.IsPersistent(item)).ToArray();
            if (persistent.Length != selection.Length) Selection.objects = persistent;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            CleanupInvalidBuiltInInspectors();
        }

        private static void CleanupInvalidBuiltInInspectors()
        {
            int removed = 0;
            foreach (var inspector in Resources.FindObjectsOfTypeAll<Editor>())
            {
                if (inspector == null) continue;
                string type = inspector.GetType().FullName;
                if (type != "UnityEditor.GameObjectInspector" && type != "UnityEditor.TransformInspector") continue;
                // Never read serializedObject here: constructing it is exactly what
                // fails for an editor restored with a destroyed target.
                var inspected = inspector.targets;
                if (inspected != null && inspected.Length > 0 && inspected.All(item => item != null)) continue;
                UnityEngine.Object.DestroyImmediate(inspector);
                removed++;
            }
            if (removed > 0)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                Debug.Log($"[FightInspector] Removed {removed} stale built-in inspectors with missing targets.");
            }
        }

        [MenuItem("Rhythm Hunter/Tools/Repair Inspector Selection")]
        private static void RepairInspector()
        {
            // Explicit recovery only; never automatically unlock the user's inspector.
            ActiveEditorTracker.sharedTracker.isLocked = false;
            Selection.objects = Array.Empty<UnityEngine.Object>();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            CleanupInvalidBuiltInInspectors();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
