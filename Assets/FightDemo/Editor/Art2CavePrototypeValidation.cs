using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemo.Editor
{
    public static class Art2CavePrototypeValidation
    {
        const string ScenePath = "Assets/FightDemo/Scenes/Art2_FightScene.unity";
        [MenuItem("Rhythm Hunter/Art2/Validate Cave Layout")]
        public static void Validate()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Wrong validation scene.");
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length != 1 || cameras[0].name != "Art2_TestCamera")
                throw new InvalidOperationException("Exactly one prototype camera must be active.");
            if (UnityEngine.Object.FindObjectsByType<FightCombatController>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Exactly one battle controller must be active.");
            var environment = GameObject.Find("CaveEnvironment_2_5D");
            if (!environment) throw new InvalidOperationException("2.5D environment must be active.");
            var renderers = environment.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length != 9 || renderers.Any(r => !r.sprite || !r.sharedMaterial))
                throw new InvalidOperationException("Expected nine valid cave cards.");
            if (UnityEngine.Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None).Length != 6)
                throw new InvalidOperationException("Expected six active battle slots.");
            if (GameObject.Find("CaveEnvironment") || GameObject.Find("ForestEnvironment"))
                throw new InvalidOperationException("Original flat backgrounds must remain inactive.");
            if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one active audio listener.");
            Debug.Log("Art2 validation passed: one camera/listener, nine cave cards, six battle slots and active combat.");
        }
    }
}
