using System;
using UnityEngine;

namespace RhythmHunter.FightDemo
{
    // Scene references stay here; Stage/Chapter assets only reference stable profiles.
    // Applies before roster creation. Does not switch cameras, music or combat state.
    [DisallowMultipleComponent]
    public sealed class FightEnvironmentSelector : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public FightEnvironmentDefinition environment;
            public bool ready;
            public Light mainLight;
            public Renderer[] floors = Array.Empty<Renderer>();
            public Renderer[] occluders = Array.Empty<Renderer>();
            public GameObject[] roots = Array.Empty<GameObject>();
            public Behaviour[] controllers = Array.Empty<Behaviour>();
            [Tooltip("Preserve individually disabled authored objects when this environment is selected.")]
            public bool[] rootEnabled = Array.Empty<bool>();
            public bool[] controllerEnabled = Array.Empty<bool>();
        }
        [SerializeField] private FightEnvironmentDefinition fallbackEnvironment;
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        [SerializeField] private Art2.Art2ActorPresentation actorPresentation;
        [SerializeField] private FightPressureVisuals pressureVisuals;
        [SerializeField] private Art2.Art2FloorOcclusion floorOcclusion;
        public FightEnvironmentDefinition ActiveEnvironment { get; private set; }
        Binding Find(FightEnvironmentDefinition profile)
        {
            profile = profile != null ? profile : fallbackEnvironment;
            foreach (var binding in bindings)
                if (binding != null && binding.environment == profile && profile != null) return binding;
            return null;
        }
        public bool CanApply(FightEnvironmentDefinition profile, out string error)
        {
            var selected = Find(profile);
            if (selected == null || !selected.ready || selected.roots == null || selected.roots.Length == 0)
            { error = $"Environment '{(profile ? profile.DisplayName : "default")}' has no ready scene binding. Current scenery retained."; return false; }
            foreach (var root in selected.roots)
                if (root == null) { error = "Environment has a missing scene root."; return false; }
            error = null; return true;
        }
        public bool TryApply(FightEnvironmentDefinition profile, out string error)
        {
            if (!CanApply(profile, out error)) return false;
            var selected = Find(profile);
            // Disable outgoing controllers first so they release material/light overrides.
            foreach (var binding in bindings)
            {
                if (binding == null || binding == selected) continue;
                foreach (var controller in binding.controllers) if (controller) controller.enabled = false;
                foreach (var root in binding.roots) if (root) root.SetActive(false);
            }
            for (int i = 0; i < selected.roots.Length; i++)
                selected.roots[i].SetActive(i >= selected.rootEnabled.Length || selected.rootEnabled[i]);
            for (int i = 0; i < selected.controllers.Length; i++)
                if (selected.controllers[i]) selected.controllers[i].enabled = i >= selected.controllerEnabled.Length || selected.controllerEnabled[i];
            ActiveEnvironment = selected.environment;
            if (selected.mainLight)
            {
                if (actorPresentation) actorPresentation.SetEnvironmentLight(selected.mainLight);
                if (pressureVisuals) pressureVisuals.SetEnvironmentLight(selected.mainLight);
            }
            if (floorOcclusion)
            {
                floorOcclusion.floors = selected.floors;
                floorOcclusion.sceneryOccluders = selected.occluders;
            }
            return true;
        }
    }
}
