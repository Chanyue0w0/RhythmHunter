using System.IO;
using UnityEditor;

namespace RhythmHunter.FightDemoEditor
{
    // Preserve the old request entry point while validating the current three-hero rules.
    [InitializeOnLoad]
    public static class FightTeamSkillValidation
    {
        const string Request = "Temp/FightTeamSkillValidation.request";
        static FightTeamSkillValidation() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Request);
            FightPartySkillValidation.Run();
            FightPartyLifecycleValidation.Run();
            string skills = File.ReadAllText("Temp/FightPartySkillValidation.result");
            string lifecycle = File.ReadAllText("Temp/FightPartyLifecycleValidation.result");
            File.WriteAllText("Temp/FightTeamSkillValidation.result",
                (skills.StartsWith("PASS:") && lifecycle.StartsWith("PASS:") ? "PASS" : "FAIL") +
                ": migrated to current 60 MP / two-beat party skills and lifecycle suites.\n" + skills + lifecycle);
        }
    }
}
