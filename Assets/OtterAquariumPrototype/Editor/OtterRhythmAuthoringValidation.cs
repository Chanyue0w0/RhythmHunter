using System.Collections.Generic;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEngine;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterRhythmAuthoringValidation
    {
        [MenuItem("Rhythm Hunter/Otter Aquarium/Validate Selected Rhythm Level")]
        public static void ValidateSelected()
        {
            OtterRhythmLevelData level = Selection.activeObject as OtterRhythmLevelData;
            if (level == null)
            {
                Debug.LogError("[OtterRhythmAuthoring] Select an OtterRhythmLevelData asset first.");
                return;
            }
            ValidateLevel(level, true);
        }

        [MenuItem("Rhythm Hunter/Otter Aquarium/Validate All Rhythm Levels")]
        public static bool ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:OtterRhythmLevelData", new[] { "Assets/OtterAquariumPrototype" });
            bool passed = guids.Length > 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                OtterRhythmLevelData level = AssetDatabase.LoadAssetAtPath<OtterRhythmLevelData>(path);
                passed &= ValidateLevel(level, false);
            }

            if (passed)
                Debug.Log($"OTTER_RHYTHM_AUTHORING_VALIDATION_PASS: {guids.Length} level asset(s).");
            else
                Debug.LogError("OTTER_RHYTHM_AUTHORING_VALIDATION_FAIL");
            return passed;
        }

        private static bool ValidateLevel(OtterRhythmLevelData level, bool logSuccess)
        {
            List<string> errors = new();
            List<string> warnings = new();
            OtterRhythmLevelExchange.Validate(level, errors, warnings);
            foreach (string warning in warnings)
                Debug.LogWarning($"[OtterRhythmAuthoring] {level?.name}: {warning}", level);
            if (errors.Count > 0)
            {
                Debug.LogError($"[OtterRhythmAuthoring] {level?.name}:\n{string.Join("\n", errors)}", level);
                return false;
            }
            if (logSuccess)
                Debug.Log($"[OtterRhythmAuthoring] '{level.DisplayName}' passed validation.", level);
            return true;
        }
    }
}
