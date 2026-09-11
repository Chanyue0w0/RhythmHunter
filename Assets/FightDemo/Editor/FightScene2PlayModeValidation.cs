using RhythmHunter.FightDemo;
using RhythmHunter.RhythmDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>
    /// Batch-mode smoke test for FightScene2 three-hero input, fourth-beat skills,
    /// enemy scheduling, prefab data, and nullable rosters.
    /// </summary>
    [InitializeOnLoad]
    public static class FightScene2PlayModeValidation
    {
        private const double TimeoutSeconds = 20.0;
        private const string Prefix = "FightScene2.Validation.";
        private const string ActiveKey = Prefix + "Active";
        private const string StartedAtKey = Prefix + "StartedAt";
        private const string Hero1AttemptedKey = Prefix + "Hero1Attempted";
        private const string Hero2AttemptedKey = Prefix + "Hero2Attempted";
        private const string Hero3AttemptedKey = Prefix + "Hero3Attempted";
        private const string PaladinSkillAttemptedKey = Prefix + "PaladinSkillAttempted";
        private const string BardSkillAttemptedKey = Prefix + "BardSkillAttempted";
        private const string MageSkillAttemptedKey = Prefix + "MageSkillAttempted";
        private const string MissAttemptedKey = Prefix + "MissAttempted";
        private const string LastInputBeatKey = Prefix + "LastInputBeat";
        private const string BardHpBeforeHealKey = Prefix + "BardHpBeforeHeal";
        private const string InitialEnemyHpKey = Prefix + "InitialEnemyHp";
        private const string PassedKey = Prefix + "Passed";
        private const string FailureKey = Prefix + "Failure";

        static FightScene2PlayModeValidation()
        {
            if (SessionState.GetBool(ActiveKey, false))
                RegisterCallbacks();
        }

        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FightSceneBuilder.Scene2Path) == null)
            {
                Debug.LogError("FIGHT_SCENE2_SMOKE_TEST_FAIL: FightScene2.unity is missing.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(FightSceneBuilder.Scene2Path);
            if (!ValidateAllCharacterPrefabs(out string prefabFailure))
            {
                Debug.LogError($"FIGHT_SCENE2_SMOKE_TEST_FAIL: {prefabFailure}");
                EditorApplication.Exit(1);
                return;
            }

            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            if (slots.Length != 6)
            {
                Debug.LogError($"FIGHT_SCENE2_SMOKE_TEST_FAIL: Expected six unit slots, found {slots.Length}.");
                EditorApplication.Exit(1);
                return;
            }

            foreach (FightUnitSlot slot in slots)
            {
                if (slot.ActorRoot != null &&
                    slot.ActorRoot.GetComponentsInChildren<FightCharacterCombatAnimator>(true).Length > 0)
                {
                    Debug.LogError(
                        $"FIGHT_SCENE2_SMOKE_TEST_FAIL: {slot.name} still contains a scene-authored character.");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetBool(Hero1AttemptedKey, false);
            SessionState.SetBool(Hero2AttemptedKey, false);
            SessionState.SetBool(Hero3AttemptedKey, false);
            SessionState.SetBool(PaladinSkillAttemptedKey, false);
            SessionState.SetBool(BardSkillAttemptedKey, false);
            SessionState.SetBool(MageSkillAttemptedKey, false);
            SessionState.SetBool(MissAttemptedKey, false);
            SessionState.SetInt(LastInputBeatKey, -1);
            SessionState.SetInt(BardHpBeforeHealKey, 0);
            SessionState.SetInt(InitialEnemyHpKey, 0);
            SessionState.SetBool(PassedKey, false);
            SessionState.SetString(FailureKey, "FightScene2 validation timed out.");
            RegisterCallbacks();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            CleanupCallbacks();
            bool passed = SessionState.GetBool(PassedKey, false);
            string failure = SessionState.GetString(FailureKey, "Unknown FightScene2 validation failure.");
            ClearSessionState();

            if (passed)
            {
                Debug.Log("FIGHT_SCENE2_SMOKE_TEST_PASS");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"FIGHT_SCENE2_SMOKE_TEST_FAIL: {failure}");
                EditorApplication.Exit(1);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedAtKey, 0f);
            FmodBeatClock clock = Object.FindFirstObjectByType<FmodBeatClock>();
            FightInputRouter input = Object.FindFirstObjectByType<FightInputRouter>();
            FightCombatController fight = Object.FindFirstObjectByType<FightCombatController>();
            FightRosterManager roster = Object.FindFirstObjectByType<FightRosterManager>();

            if (clock != null && !string.IsNullOrEmpty(clock.LastError))
            {
                FailAndExit(clock.LastError);
                return;
            }

            if (input != null && !input.IsConfigured)
            {
                FailAndExit("FightInputRouter is not configured.");
                return;
            }

            if (fight != null && !fight.UsesFrontHeroControls)
            {
                FailAndExit("FightScene2 front-hero mode did not activate.");
                return;
            }

            if (fight != null && roster == null)
            {
                FailAndExit("FightRosterManager is missing.");
                return;
            }

            if (clock != null && clock.ReceivedBeatCount > 0 && clock.LatestBeat.TimeSignatureUpper != 4)
            {
                FailAndExit($"FightScene2 requires a four-beat measure, received {clock.LatestBeat.TimeSignatureUpper}.");
                return;
            }

            if (roster != null &&
                (roster.ActiveHeroes.Count < 1 || roster.ActiveHeroes.Count > 3 ||
                 roster.ActiveEnemies.Count < 1 || roster.ActiveEnemies.Count > 3 ||
                 !HasValidPrefabData(roster) || !HasExpectedHeroSkills(roster)))
            {
                FailAndExit(
                    $"Default prefab roster is invalid. Heroes={roster.ActiveHeroes.Count}, Enemies={roster.ActiveEnemies.Count}.");
                return;
            }

            if (fight != null && SessionState.GetInt(InitialEnemyHpKey, 0) == 0)
                SessionState.SetInt(InitialEnemyHpKey, TotalEnemyHp());

            if (clock != null && fight != null && clock.ReceivedBeatCount >= 1 &&
                !SessionState.GetBool(Hero1AttemptedKey, false) &&
                clock.LatestBeat.Beat != 4 &&
                clock.TryGetBeatPhase(out float hero1Phase) && hero1Phase < 0.08f)
            {
                SessionState.SetBool(Hero1AttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(Hero1AttemptedKey, false) &&
                !SessionState.GetBool(Hero2AttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat > SessionState.GetInt(LastInputBeatKey, -1) &&
                clock.LatestBeat.Beat != 4 &&
                clock.TryGetBeatPhase(out float hero2Phase) && hero2Phase < 0.08f)
            {
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Support);
                SessionState.SetBool(Hero2AttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(Hero2AttemptedKey, false) &&
                !SessionState.GetBool(Hero3AttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat > SessionState.GetInt(LastInputBeatKey, -1) &&
                clock.LatestBeat.Beat != 4 &&
                clock.TryGetBeatPhase(out float hero3Phase) && hero3Phase < 0.08f)
            {
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Damage);
                SessionState.SetBool(Hero3AttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(Hero3AttemptedKey, false) &&
                !SessionState.GetBool(PaladinSkillAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat > SessionState.GetInt(LastInputBeatKey, -1) &&
                clock.LatestBeat.Beat == 4 &&
                clock.TryGetBeatPhase(out float skillPhase) && skillPhase < 0.12f)
            {
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Tank);
                SessionState.SetBool(PaladinSkillAttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(PaladinSkillAttemptedKey, false) &&
                !SessionState.GetBool(BardSkillAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat > SessionState.GetInt(LastInputBeatKey, -1) &&
                clock.LatestBeat.Beat == 4 &&
                clock.TryGetBeatPhase(out float bardSkillPhase) && bardSkillPhase < 0.12f)
            {
                FightUnitSlot bard = fight.SecondHero.UnitSlot;
                bard.TakeDamage(10);
                SessionState.SetInt(BardHpBeforeHealKey, bard.CurrentHp);
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Support);
                SessionState.SetBool(BardSkillAttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(BardSkillAttemptedKey, false) &&
                !SessionState.GetBool(MageSkillAttemptedKey, false) &&
                clock.LatestBeat.GlobalBeat > SessionState.GetInt(LastInputBeatKey, -1) &&
                clock.LatestBeat.Beat == 4 &&
                clock.TryGetBeatPhase(out float mageSkillPhase) && mageSkillPhase < 0.12f)
            {
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Damage);
                SessionState.SetBool(MageSkillAttemptedKey, true);
                SessionState.SetInt(LastInputBeatKey, (int)clock.LatestBeat.GlobalBeat);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(MageSkillAttemptedKey, false) &&
                !SessionState.GetBool(MissAttemptedKey, false) &&
                clock.TryGetBeatPhase(out float missPhase) && missPhase > 0.4f && missPhase < 0.6f)
            {
                fight.SubmitHeroCommand(FightInputRouter.HeroCommand.Damage);
                SessionState.SetBool(MissAttemptedKey, true);
            }

            if (clock != null && fight != null &&
                SessionState.GetBool(Hero1AttemptedKey, false) &&
                SessionState.GetBool(Hero2AttemptedKey, false) &&
                SessionState.GetBool(Hero3AttemptedKey, false) &&
                SessionState.GetBool(PaladinSkillAttemptedKey, false) &&
                SessionState.GetBool(BardSkillAttemptedKey, false) &&
                SessionState.GetBool(MageSkillAttemptedKey, false) &&
                SessionState.GetBool(MissAttemptedKey, false) &&
                fight.FrontHero.UnitSlot != null &&
                fight.FrontHero.UnitSlot.GuardPlayCount >= 1 &&
                fight.SecondHero.UnitSlot != null &&
                fight.SecondHero.UnitSlot.LightAttackPlayCount >= 1 &&
                fight.SecondHero.UnitSlot.SkillAttackPlayCount >= 1 &&
                fight.ThirdHero.UnitSlot != null &&
                fight.ThirdHero.UnitSlot.LightAttackPlayCount >= 1 &&
                fight.ThirdHero.UnitSlot.SkillAttackPlayCount >= 1 &&
                fight.ThirdHero.UnitSlot.MissFeedbackPlayCount >= 1 &&
                fight.ActiveEnemySlot != null &&
                fight.EnemyCurrentMana >= 1 &&
                TotalEnemyNormalAttacks() >= 3 &&
                !fight.HasPendingEnemyAttack)
            {
                int initialEnemyHp = SessionState.GetInt(InitialEnemyHpKey, 0);
                int currentEnemyHp = TotalEnemyHp();
                int hero1Normal = fight.FrontHero.UnitSlot?.LightAttackPlayCount ?? 0;
                int hero2Normal = fight.SecondHero.UnitSlot?.LightAttackPlayCount ?? 0;
                int hero3Normal = fight.ThirdHero.UnitSlot?.LightAttackPlayCount ?? 0;
                int bardHpBeforeHeal = SessionState.GetInt(BardHpBeforeHealKey, 0);
                int enemyNormalAttacks = TotalEnemyNormalAttacks();
                int enemySkillAttacks = TotalEnemySkillAttacks();
                int enemyFrameDamageEvents = TotalEnemyDamageEvents();
                int enemyMana = fight.EnemyCurrentMana;
                bool scheduledBeatsAligned = fight.LastEnemyAttackGlobalBeat >= 0 &&
                                             (fight.LastEnemyAttackGlobalBeat + 1) % fight.EnemyAttackIntervalBeats == 0 &&
                                             !fight.HasPendingEnemyAttack;
                bool combatFlowPassed = fight.FrontHero.UnitSlot != null &&
                                        fight.SecondHero.UnitSlot != null &&
                                        fight.ThirdHero.UnitSlot != null &&
                                        fight.HealthSystemEnabled &&
                                        !fight.BattleEnded &&
                                        hero1Normal >= 1 &&
                                        fight.FrontHero.SkillActivationCount >= 1 &&
                                        fight.FrontHero.UnitSlot.GuardPlayCount >= 1 &&
                                        hero2Normal >= 1 &&
                                        fight.SecondHero.SkillActivationCount >= 1 &&
                                        fight.SecondHero.UnitSlot.SkillAttackPlayCount >= 1 &&
                                        fight.SecondHero.UnitSlot.CurrentHp > bardHpBeforeHeal &&
                                        hero3Normal >= 1 &&
                                        fight.ThirdHero.SkillActivationCount >= 1 &&
                                        fight.ThirdHero.UnitSlot.SkillAttackPlayCount >= 1 &&
                                        fight.FrontHero.UnitSlot.OnBeatFeedbackPlayCount >= 2 &&
                                        fight.SecondHero.UnitSlot.OnBeatFeedbackPlayCount >= 2 &&
                                        fight.ThirdHero.UnitSlot.OnBeatFeedbackPlayCount >= 2 &&
                                        fight.ThirdHero.UnitSlot.MissFeedbackPlayCount >= 1 &&
                                        fight.FrontHero.UnitSlot.HeavyAttackPlayCount == 0 &&
                                        enemyNormalAttacks >= 3 &&
                                        enemyFrameDamageEvents >= 3 &&
                                        enemySkillAttacks == 0 &&
                                        enemyMana >= 1 &&
                                        fight.BlockedAttackCount >= 1 &&
                                        scheduledBeatsAligned &&
                                        currentEnemyHp < initialEnemyHp &&
                                        clock.IsPlaying;
                FightCharacterDefinition[] singleEnemyRoster =
                {
                    roster.EnemyPrefabs[0],
                    null,
                    null
                };
                roster.SetRoster(roster.HeroPrefabs, singleEnemyRoster);
                bool nullableRosterPassed = roster.ActiveHeroes.Count == 3 &&
                                            roster.ActiveEnemies.Count == 1 &&
                                            fight.ActiveEnemySlot != null &&
                                            fight.ActiveEnemySlot.HasCharacter;
                bool passed = combatFlowPassed && nullableRosterPassed;

                SessionState.SetBool(PassedKey, passed);
                SessionState.SetString(
                    FailureKey,
                    passed
                        ? string.Empty
                        : $"Invalid flow. Hero1Normal={hero1Normal}, Guards={fight.FrontHero.UnitSlot.GuardPlayCount}, " +
                          $"Hero2Normal={hero2Normal}, Hero3Normal={hero3Normal}, " +
                          $"BardHP={fight.SecondHero.UnitSlot.CurrentHp}/{bardHpBeforeHeal}, " +
                          $"EnemyNormal={enemyNormalAttacks}, EnemySkills={enemySkillAttacks}, EnemyMana={enemyMana}, " +
                          $"EnemyFrameDamageEvents={enemyFrameDamageEvents}, " +
                          $"ScheduledBeatsAligned={scheduledBeatsAligned}, " +
                          $"EnemyHP={currentEnemyHp}/{initialEnemyHp}, Blocks={fight.BlockedAttackCount}, " +
                          $"SingleEnemyRoster={nullableRosterPassed}, " +
                          $"HealthEnabled={fight.HealthSystemEnabled}, BattleEnded={fight.BattleEnded}, " +
                          $"ClockPlaying={clock.IsPlaying}.");
                EditorApplication.ExitPlaymode();
                return;
            }

            if (elapsed >= TimeoutSeconds)
            {
                FightUnitSlot enemy = fight != null ? fight.ActiveEnemySlot : null;
                SessionState.SetString(
                    FailureKey,
                    $"FightScene2 validation timed out. " +
                    $"Hero1Attempted={SessionState.GetBool(Hero1AttemptedKey, false)}, " +
                    $"Hero2Attempted={SessionState.GetBool(Hero2AttemptedKey, false)}, " +
                    $"Hero3Attempted={SessionState.GetBool(Hero3AttemptedKey, false)}, " +
                    $"PaladinSkill={SessionState.GetBool(PaladinSkillAttemptedKey, false)}, " +
                    $"BardSkill={SessionState.GetBool(BardSkillAttemptedKey, false)}, " +
                    $"MageSkill={SessionState.GetBool(MageSkillAttemptedKey, false)}, " +
                    $"MissAttempted={SessionState.GetBool(MissAttemptedKey, false)}, " +
                    $"Pending={fight?.HasPendingEnemyAttack ?? false}, " +
                    $"EnemyNormal={enemy?.NormalAttackPlayCount ?? -1}, " +
                    $"EnemyFrameDamage={enemy?.CombatAnimator?.DamageEventCount ?? -1}, " +
                    $"Hero1Normal={fight?.FrontHero.UnitSlot?.LightAttackPlayCount ?? -1}, " +
                    $"Guards={fight?.FrontHero.UnitSlot?.GuardPlayCount ?? -1}, " +
                    $"Hero2Normal={fight?.SecondHero.UnitSlot?.LightAttackPlayCount ?? -1}, " +
                    $"Hero3Normal={fight?.ThirdHero.UnitSlot?.LightAttackPlayCount ?? -1}, " +
                    $"ReceivedBeats={clock?.ReceivedBeatCount ?? -1}.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static bool HasValidPrefabData(FightRosterManager roster)
        {
            foreach (FightUnitSlot slot in roster.ActiveHeroes)
            {
                if (!HasValidPrefabData(slot))
                    return false;
            }

            foreach (FightUnitSlot slot in roster.ActiveEnemies)
            {
                if (!HasValidPrefabData(slot))
                    return false;
            }

            return true;
        }

        private static bool HasExpectedHeroSkills(FightRosterManager roster)
        {
            return roster.HeroPrefabs.Count >= 3 &&
                   roster.HeroPrefabs[0] != null &&
                   roster.HeroPrefabs[0].SkillType == FightCharacterDefinition.SkillBehavior.Guard &&
                   roster.HeroPrefabs[1] != null &&
                   roster.HeroPrefabs[1].SkillType == FightCharacterDefinition.SkillBehavior.HealParty &&
                   roster.HeroPrefabs[2] != null &&
                   roster.HeroPrefabs[2].SkillType == FightCharacterDefinition.SkillBehavior.Damage;
        }

        private static bool ValidateAllCharacterPrefabs(out string failure)
        {
            string[] folders =
            {
                "Assets/FightDemo/Prefabs/Heroes",
                "Assets/FightDemo/Prefabs/Enemies"
            };
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", folders);
            int characterCount = 0;
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                FightCharacterDefinition definition = prefab != null
                    ? prefab.GetComponent<FightCharacterDefinition>()
                    : null;
                if (definition == null)
                    continue;

                characterCount++;
                if (definition.AttackIntervalBeats < 1)
                {
                    failure = $"{path} has invalid attack interval or idle frames.";
                    return false;
                }

                FightCharacterCombatAnimator combatAnimator = prefab.GetComponent<FightCharacterCombatAnimator>();
                if (combatAnimator == null || combatAnimator.FrameCount == 0 || combatAnimator.TargetRenderer == null)
                {
                    failure = $"{path} is missing FightCharacterCombatAnimator.";
                    return false;
                }

                foreach (FightCharacterCombatAnimator.CombatAnimation animation in
                         System.Enum.GetValues(typeof(FightCharacterCombatAnimator.CombatAnimation)))
                {
                    if (!combatAnimator.HasSequence(animation))
                    {
                        failure = $"{path} has no frames for {animation}.";
                        return false;
                    }
                }

                foreach (FightCharacterCombatAnimator.Sequence sequence in combatAnimator.Sequences)
                {
                    int frameCount = sequence.Frames.Count;
                    bool invalidEventFrame = sequence.DamageFrame < 0 || sequence.DamageFrame >= frameCount ||
                                             sequence.WarningFrame >= frameCount ||
                                             sequence.AttackEffectFrame >= frameCount;
                    if (frameCount == 0 || invalidEventFrame)
                    {
                        failure = $"{path} has invalid frame events for {sequence.Animation}.";
                        return false;
                    }
                }
            }

            if (characterCount != 6)
            {
                failure = $"Expected six character prefabs, found {characterCount}.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool HasValidPrefabData(FightUnitSlot slot)
        {
            FightCharacterDefinition definition = slot != null ? slot.CharacterDefinition : null;
            return slot != null &&
                   slot.HasCharacter &&
                   slot.ActorInstance != null &&
                   definition != null &&
                   definition.MaxHp > 0 &&
                   definition.AttackPower >= 0 &&
                   definition.SkillDamage >= 0 &&
                   definition.SkillEffectPrefab != null &&
                   definition.AttackIntervalBeats > 0 &&
                   slot.CombatAnimator != null &&
                   slot.CombatAnimator.FrameCount > 0;
        }

        private static int TotalEnemyHp()
        {
            int total = 0;
            FightUnitSlot[] slots = Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None);
            foreach (FightUnitSlot slot in slots)
            {
                if (slot.Team == FightUnitSlot.UnitTeam.Enemy)
                    total += slot.CurrentHp;
            }

            return total;
        }

        private static int TotalEnemyNormalAttacks()
        {
            int total = 0;
            foreach (FightUnitSlot slot in Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None))
            {
                if (slot.Team == FightUnitSlot.UnitTeam.Enemy)
                    total += slot.NormalAttackPlayCount;
            }

            return total;
        }

        private static int TotalEnemySkillAttacks()
        {
            int total = 0;
            foreach (FightUnitSlot slot in Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None))
            {
                if (slot.Team == FightUnitSlot.UnitTeam.Enemy)
                    total += slot.SkillAttackPlayCount;
            }

            return total;
        }

        private static int TotalEnemyDamageEvents()
        {
            int total = 0;
            foreach (FightUnitSlot slot in Object.FindObjectsByType<FightUnitSlot>(FindObjectsSortMode.None))
            {
                if (slot.Team == FightUnitSlot.UnitTeam.Enemy && slot.CombatAnimator != null)
                    total += slot.CombatAnimator.DamageEventCount;
            }

            return total;
        }

        private static void FailAndExit(string failure)
        {
            SessionState.SetString(FailureKey, failure);
            EditorApplication.ExitPlaymode();
        }

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void CleanupCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void ClearSessionState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseBool(Hero1AttemptedKey);
            SessionState.EraseBool(Hero2AttemptedKey);
            SessionState.EraseBool(Hero3AttemptedKey);
            SessionState.EraseBool(PaladinSkillAttemptedKey);
            SessionState.EraseBool(BardSkillAttemptedKey);
            SessionState.EraseBool(MageSkillAttemptedKey);
            SessionState.EraseBool(MissAttemptedKey);
            SessionState.EraseInt(LastInputBeatKey);
            SessionState.EraseInt(BardHpBeforeHealKey);
            SessionState.EraseInt(InitialEnemyHpKey);
            SessionState.EraseBool(PassedKey);
            SessionState.EraseString(FailureKey);
        }
    }
}
